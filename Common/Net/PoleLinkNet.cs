using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    public enum PoleLinkRequestKind : byte
    {
        Link = 0,
        Cut = 1
    }

    // The outcome carried by PoleLinkChanged. Values are part of the wire format: append, never renumber.
    public enum PoleLinkResult : byte
    {
        Update = 0,         // a plain state update (somebody else's change, a pole kill, or a refusal's state for a spectator): nothing to tell the player
        Linked = 1,         // to the sender only: your link was made, take the wire (item type and count follow)
        Cut = 2,            // to the sender only: your cut was made
        NotAPole = 10,      // refusals (to the sender only): everything from here on changes nothing
        SamePole = 11,
        BadWire = 12,
        NoFreePoint = 13,
        SlotTaken = 14,
        TooLong = 15,
        OutOfReach = 16,
        NotEnoughWire = 17,
        NoLink = 18
    }

    // Pole links (power poles patch P3). The server owns the link graph (PoleLinkSystem): it validates, changes and saves it.
    //  - the owner client sends PoleLinkRequest: Link (pole A tile, pole B tile, wire type, optional connection point hints, the tile clicked on
    //    pole B) or Cut (pole tile, slot, clicked tile). Any tile of a pole's footprint names the pole; the server finds its entity.
    //  - the server checks the sender, both poles, the wire, the click (on pole B; there is NO reach check since P7c), the connection points (the
    //    hints when they are free, else the closest free ones), the span, and that the sender holds enough wire; then it changes the graph
    //    (which flags the power grid), answers the sender with PoleLinkChanged (Linked + the wire to take, or a refusal) and broadcasts
    //    PoleLinkChanged (Update) to everyone else
    //  - PoleLinkChanged always carries the COMPLETE slot list of each affected pole, so a repeated or out-of-order packet cannot drift
    //  - the wire items are taken by the owning client when the answer arrives (the server's copy of the inventory only mirrors it, same
    //    as BucketPour and CartInputNet): the cursor stack first, then the selected item, then the rest, each changed slot synced with
    //    vanilla's SyncEquipment (slot 58 is the cursor item)
    //  - a refused request is answered to the sender alone with the current state of the poles, so the client never stays out of sync
    //  - a killed pole removes its links (MachineRemoval.OnTileKilled): the server drops each link's wire at the pole and broadcasts
    //  - a joining client gets every link from the "pole-links" join provider
    // No client prediction: a link appears when the server confirms. Single-player runs the same server code through SendRequest and
    // applies the answer's feedback directly (nothing is sent).
    public class PoleLinkNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.PoleLinkRequest, PacketDirection.ToServer, PoleLinkNet.HandleRequest, 2);
            FactorrariaNet.Register(FactorrariaPacket.PoleLinkChanged, PacketDirection.ToClient, PoleLinkNet.HandleChanged);
            JoinSync.Register("pole-links", PoleLinkNet.WriteAll, PoleLinkNet.ReadAll);
        }

        public override void OnWorldUnload()
        {
            PoleLinkNet.ResetClient();
        }

        // PostUpdateWorld does not run on a multiplayer client; PostUpdateEverything runs on every side.
        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                PoleLinkNet.ClientTick();
        }
    }

    public static class PoleLinkNet
    {
        // Sanity limits on what a packet may claim (a bad packet must not make the client allocate without end).
        public static int MaxJoinLinks = 200000;
        public static int MaxPolesPerPacket = 32;
        public static int RefusalRepeatTicks = 90;       // the same refusal line is shown at most once per this many ticks (a held autoReuse cutter can be refused every few ticks)
        static PoleLinkResult lastRefusal;
        static uint lastRefusalTick;

        sealed class Incoming
        {
            public PoleLinkResult Result;
            public int ItemType;
            public int Count;
            public List<KeyValuePair<Point16, List<PoleLinkSystem.SlotState>>> States;   // a PoleLinkChanged packet
            public List<PoleLink> ReplaceAll;                                            // the join provider
        }

        static readonly ConcurrentQueue<Incoming> incoming = new ConcurrentQueue<Incoming>();

        // TEMPORARY (patch P3, remove in Phase 10 together with every call): the pole link log lines.
        public static void Trace(string message) => FactorrariaNet.Log("[PoleLinks] " + message);

        public static byte AutoSlot => byte.MaxValue;

        public static void ResetClient()
        {
            while (incoming.TryDequeue(out _)) { }
        }

        // ---------------------------------------------------------------- owner side: requests

        /// <summary>
        /// Owner side: ask for a link between two poles. ax/ay and bx/by are any footprint tile of each pole. hintSlotA / hintSlotB are the
        /// connection points the player saw in the preview (AutoSlot lets the server choose). clickX/clickY is the tile that was clicked on
        /// pole B; the server checks that it is on that pole (no reach check since P7c).
        /// </summary>
        public static void RequestLink(int ax, int ay, int bx, int by, CustomWireType wire, int hintSlotA, int hintSlotB, int clickX, int clickY)
        {
            FactorrariaNet.SendRequest(FactorrariaPacket.PoleLinkRequest, writer =>
            {
                writer.Write((byte)PoleLinkRequestKind.Link);
                writer.Write((short)ax);
                writer.Write((short)ay);
                writer.Write((short)bx);
                writer.Write((short)by);
                writer.Write((byte)wire);
                writer.Write((byte)hintSlotA);
                writer.Write((byte)hintSlotB);
                writer.Write((short)clickX);
                writer.Write((short)clickY);
            });
        }

        /// <summary>Owner side: ask to cut the link on one connection point of a pole. clickX/clickY is the clicked tile (on a pole or on the wire).</summary>
        public static void RequestCut(int poleX, int poleY, int slot, int clickX, int clickY)
        {
            FactorrariaNet.SendRequest(FactorrariaPacket.PoleLinkRequest, writer =>
            {
                writer.Write((byte)PoleLinkRequestKind.Cut);
                writer.Write((short)poleX);
                writer.Write((short)poleY);
                writer.Write((byte)slot);
                writer.Write((short)clickX);
                writer.Write((short)clickY);
            });
        }

        // ---------------------------------------------------------------- server: the request

        // Server (or single-player, where sender is the local player).
        public static void HandleRequest(BinaryReader reader, int sender)
        {
            var kind = (PoleLinkRequestKind)reader.ReadByte();

            if (sender < 0 || sender >= Main.maxPlayers)
                return;

            Player player = Main.player[sender];
            if (player == null || !player.active || player.dead)
                return;

            if (kind == PoleLinkRequestKind.Link)
                ServerLink(reader, player, sender);
            else if (kind == PoleLinkRequestKind.Cut)
                ServerCut(reader, player, sender);
        }

        static void Refuse(int sender, Player player, PoleLinkResult result, string reason, params Point16[] poles)
        {
            Trace("refused " + player.name + ": " + reason);
            Answer(sender, result, 0, 0, poles);
        }

        static void ServerLink(BinaryReader reader, Player player, int sender)
        {
            int ax = reader.ReadInt16();
            int ay = reader.ReadInt16();
            int bx = reader.ReadInt16();
            int by = reader.ReadInt16();
            var wire = (CustomWireType)reader.ReadByte();
            int hintA = reader.ReadByte();
            int hintB = reader.ReadByte();
            int clickX = reader.ReadInt16();
            int clickY = reader.ReadInt16();

            PoleLinkSystem links = PoleLinkSystem.Instance;

            if (!CustomWireNet.IsSingleKnownType(wire))
            {
                Refuse(sender, player, PoleLinkResult.BadWire, "unknown wire type " + (byte)wire);
                return;
            }

            bool hasA = PoleLinkSystem.TryGetPoleAtTile(ax, ay, out PowerPoleTileEntityBase poleA, out Point16 cornerA);
            bool hasB = PoleLinkSystem.TryGetPoleAtTile(bx, by, out PowerPoleTileEntityBase poleB, out Point16 cornerB);

            if (!hasA || !hasB)
            {
                var known = new List<Point16>();
                if (hasA)
                    known.Add(cornerA);
                if (hasB)
                    known.Add(cornerB);

                Refuse(sender, player, PoleLinkResult.NotAPole, "(" + ax + ", " + ay + ") or (" + bx + ", " + by + ") is not a power pole", known.ToArray());
                return;
            }

            if (cornerA == cornerB)
            {
                Refuse(sender, player, PoleLinkResult.SamePole, "both ends are the pole at (" + cornerA.X + ", " + cornerA.Y + ")", cornerA);
                return;
            }

            if (!WorldGen.InWorld(clickX, clickY, 1) || !links.IsClickZone(poleB, clickX, clickY))
            {
                Refuse(sender, player, PoleLinkResult.OutOfReach, "the click (" + clickX + ", " + clickY + ") is not on the pole at (" + cornerB.X + ", " + cornerB.Y + ")", cornerA, cornerB);
                return;
            }

            int slotA;
            if (hintA != AutoSlot)
            {
                if (hintA >= links.SlotCount(poleA) || !links.IsSlotFree(cornerA, hintA))
                {
                    Refuse(sender, player, PoleLinkResult.SlotTaken, "point " + hintA + " of the pole at (" + cornerA.X + ", " + cornerA.Y + ") is taken or does not exist", cornerA, cornerB);
                    return;
                }

                slotA = hintA;
            }
            else
            {
                slotA = links.ChooseFreePoint(poleA, PoleLinkSystem.FootprintTopWorld(cornerB));
                if (slotA < 0)
                {
                    Refuse(sender, player, PoleLinkResult.NoFreePoint, "the pole at (" + cornerA.X + ", " + cornerA.Y + ") has no free point", cornerA, cornerB);
                    return;
                }
            }

            int slotB;
            if (hintB != AutoSlot)
            {
                if (hintB >= links.SlotCount(poleB) || !links.IsSlotFree(cornerB, hintB))
                {
                    Refuse(sender, player, PoleLinkResult.SlotTaken, "point " + hintB + " of the pole at (" + cornerB.X + ", " + cornerB.Y + ") is taken or does not exist", cornerA, cornerB);
                    return;
                }

                slotB = hintB;
            }
            else
            {
                slotB = links.ChooseFreePoint(poleB, poleA.GetLinkPointWorld(slotA));
                if (slotB < 0)
                {
                    Refuse(sender, player, PoleLinkResult.NoFreePoint, "the pole at (" + cornerB.X + ", " + cornerB.Y + ") has no free point", cornerA, cornerB);
                    return;
                }
            }

            float span = PoleLinkSystem.SpanTiles(poleA, slotA, poleB, slotB);
            float maxSpan = links.MaxSpan(wire);
            if (span > maxSpan)
            {
                Refuse(sender, player, PoleLinkResult.TooLong, "span " + span.ToString("0.0") + " tiles, " + wire + " wire reaches " + maxSpan.ToString("0.0"), cornerA, cornerB);
                return;
            }

            int cost = links.CostFor(wire, span);
            int itemType = CustomWireNet.WireItemType(wire);
            int held = CountWire(player, itemType);
            if (held < cost)
            {
                Refuse(sender, player, PoleLinkResult.NotEnoughWire, "needs " + cost + " " + wire + " wire, has " + held, cornerA, cornerB);
                return;
            }

            var link = new PoleLink { A = cornerA, B = cornerB, SlotA = (byte)slotA, SlotB = (byte)slotB, Wire = wire, Cost = (ushort)cost };
            if (!links.TryAdd(link))
            {
                Refuse(sender, player, PoleLinkResult.SlotTaken, "a connection point was taken just now", cornerA, cornerB);
                return;
            }

            Trace(player.name + " linked (" + cornerA.X + ", " + cornerA.Y + ")#" + slotA + " to (" + cornerB.X + ", " + cornerB.Y + ")#" + slotB + " with " + wire + " wire, span "
                + span.ToString("0.0") + ", cost " + cost + "; links now " + links.Links.Count);

            Answer(sender, PoleLinkResult.Linked, itemType, cost, cornerA, cornerB);
            BroadcastUpdate(sender, cornerA, cornerB);
        }

        static void ServerCut(BinaryReader reader, Player player, int sender)
        {
            int poleX = reader.ReadInt16();
            int poleY = reader.ReadInt16();
            int slot = reader.ReadByte();
            int clickX = reader.ReadInt16();
            int clickY = reader.ReadInt16();

            PoleLinkSystem links = PoleLinkSystem.Instance;

            if (!PoleLinkSystem.TryGetPoleAtTile(poleX, poleY, out _, out Point16 corner))
            {
                Refuse(sender, player, PoleLinkResult.NotAPole, "(" + poleX + ", " + poleY + ") is not a power pole");
                return;
            }

            PoleLink link = links.GetLink(corner, slot);
            if (link == null)
            {
                Refuse(sender, player, PoleLinkResult.NoLink, "point " + slot + " of the pole at (" + corner.X + ", " + corner.Y + ") has no link", corner);
                return;
            }

            if (!WorldGen.InWorld(clickX, clickY, 1) || !links.IsClickNearLink(link, clickX, clickY))
            {
                Refuse(sender, player, PoleLinkResult.OutOfReach, "the click (" + clickX + ", " + clickY + ") is not on that wire", link.A, link.B);
                return;
            }

            Point16 a = link.A;
            Point16 b = link.B;
            links.Remove(link);

            // The refund is spawned here, once. NewItem on a server tells every client about the item itself.
            Item.NewItem(player.GetSource_Misc("FactorrariaPoleLinkCut"), clickX * 16, clickY * 16, 16, 16, CustomWireNet.WireItemType(link.Wire), link.Cost);

            Trace(player.name + " cut (" + a.X + ", " + a.Y + ")#" + link.SlotA + " to (" + b.X + ", " + b.Y + ")#" + link.SlotB + ", refunded " + link.Cost + " " + link.Wire + " wire; links now " + links.Links.Count);

            Answer(sender, PoleLinkResult.Cut, 0, 0, a, b);
            BroadcastUpdate(sender, a, b);
        }

        // ---------------------------------------------------------------- kill cleanup (called from MachineRemoval.OnTileKilled)

        /// <summary>
        /// A machine tile was killed. Server and single-player: a pole loses its links, each link's wire is dropped at the pole, and every
        /// client is told the state of the affected poles. A multiplayer client only drops its replica of the links (the refund and the
        /// broadcast come from the server). Idempotent: a pole without links does nothing.
        /// </summary>
        public static void OnMachineKilled(BaseMachine machine, Point16 corner)
        {
            if (machine is not PowerPoleTileEntityBase pole)
                return;

            PoleLinkSystem links = PoleLinkSystem.Instance;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                links.RemoveAllFor(corner);
                return;
            }

            List<PoleLink> removed = links.RemoveAllFor(corner);
            if (removed.Count == 0)
                return;

            var affected = new List<Point16> { corner };
            int refunded = 0;
            int dropX = corner.X * 16 + 8;
            int dropY = (corner.Y + PoleLinkSystem.FootprintHeight(pole) - 1) * 16;

            foreach (PoleLink link in removed)
            {
                Point16 other = link.Other(corner);
                if (!affected.Contains(other))
                    affected.Add(other);

                Item.NewItem(new EntitySource_TileEntity(pole), dropX, dropY, 16, 16, CustomWireNet.WireItemType(link.Wire), link.Cost);
                refunded += link.Cost;
            }

            Trace("pole at (" + corner.X + ", " + corner.Y + ") was killed: removed " + removed.Count + " link(s), dropped " + refunded + " wire item(s); links now " + links.Links.Count);

            BroadcastUpdate(-1, affected.ToArray());
        }

        // ---------------------------------------------------------------- server: answers

        // Tells the sender the outcome. A multiplayer server sends the state of the poles (the sender's replica may be out of date); in
        // single-player the feedback is applied right here, because the request ran inline.
        static void Answer(int sender, PoleLinkResult result, int itemType, int count, params Point16[] poles)
        {
            if (Main.netMode == NetmodeID.Server)
                FactorrariaNet.SendToClient(sender, FactorrariaPacket.PoleLinkChanged, writer => WriteChanged(writer, result, itemType, count, poles));
            else if (Main.netMode == NetmodeID.SinglePlayer)
                ApplyFeedback(result, itemType, count);
        }

        /// <summary>Server only (no-op elsewhere): tells every client except ignoreClient the complete state of these poles.</summary>
        static void BroadcastUpdate(int ignoreClient, params Point16[] poles)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            FactorrariaNet.Broadcast(FactorrariaPacket.PoleLinkChanged, writer => WriteChanged(writer, PoleLinkResult.Update, 0, 0, poles), ignoreClient);
        }

        static void WriteChanged(BinaryWriter writer, PoleLinkResult result, int itemType, int count, Point16[] poles)
        {
            PoleLinkSystem links = PoleLinkSystem.Instance;

            var distinct = new List<Point16>();
            foreach (Point16 pole in poles)
            {
                if (!distinct.Contains(pole))
                    distinct.Add(pole);
            }

            writer.Write((byte)result);
            if (result == PoleLinkResult.Linked)
            {
                writer.Write(itemType);
                writer.Write((ushort)count);
            }

            writer.Write((byte)distinct.Count);
            foreach (Point16 pole in distinct)
            {
                List<PoleLinkSystem.SlotState> state = links.GetPoleState(pole);

                writer.Write(pole.X);
                writer.Write(pole.Y);
                writer.Write((byte)state.Count);

                foreach (PoleLinkSystem.SlotState slot in state)
                {
                    writer.Write(slot.Slot);
                    writer.Write(slot.Other.X);
                    writer.Write(slot.Other.Y);
                    writer.Write(slot.OtherSlot);
                    writer.Write((byte)slot.Wire);
                    writer.Write(slot.Cost);
                }
            }
        }

        // ---------------------------------------------------------------- client: the answer

        // May run on the network thread: it only parses and queues. ClientTick applies it on the main thread.
        public static void HandleChanged(BinaryReader reader, int sender)
        {
            var result = (PoleLinkResult)reader.ReadByte();
            int itemType = 0;
            int count = 0;

            if (result == PoleLinkResult.Linked)
            {
                itemType = reader.ReadInt32();
                count = reader.ReadUInt16();
            }

            int poleCount = reader.ReadByte();
            if (poleCount > MaxPolesPerPacket)
                throw new InvalidDataException("pole link packet claims " + poleCount + " poles");

            int maxSlots = PoleLinkSystem.Instance.MaxSlotsPerPole;
            var states = new List<KeyValuePair<Point16, List<PoleLinkSystem.SlotState>>>(poleCount);

            for (int i = 0; i < poleCount; i++)
            {
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                int used = reader.ReadByte();
                if (used > maxSlots)
                    throw new InvalidDataException("pole link packet claims " + used + " used slots on one pole");

                var slots = new List<PoleLinkSystem.SlotState>(used);
                for (int s = 0; s < used; s++)
                {
                    byte slot = reader.ReadByte();
                    short otherX = reader.ReadInt16();
                    short otherY = reader.ReadInt16();
                    byte otherSlot = reader.ReadByte();
                    var wire = (CustomWireType)reader.ReadByte();
                    ushort cost = reader.ReadUInt16();

                    if (slot >= maxSlots || otherSlot >= maxSlots || !CustomWireNet.IsSingleKnownType(wire))
                        continue;

                    slots.Add(new PoleLinkSystem.SlotState { Slot = slot, Other = new Point16(otherX, otherY), OtherSlot = otherSlot, Wire = wire, Cost = cost });
                }

                states.Add(new KeyValuePair<Point16, List<PoleLinkSystem.SlotState>>(new Point16(x, y), slots));
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            incoming.Enqueue(new Incoming { Result = result, ItemType = itemType, Count = count, States = states });
        }

        /// <summary>Main thread, every tick on a client: applies what arrived, in order.</summary>
        public static void ClientTick()
        {
            PoleLinkSystem links = PoleLinkSystem.Instance;

            while (incoming.TryDequeue(out Incoming packet))
            {
                if (packet.ReplaceAll != null)
                {
                    links.ReplaceAll(packet.ReplaceAll);
                    Trace("join provider pole-links applied: " + packet.ReplaceAll.Count + " link(s)");
                    continue;
                }

                links.ApplyPoleStates(packet.States);
                ApplyFeedback(packet.Result, packet.ItemType, packet.Count);
            }
        }

        // Owner side, main thread: a made link takes its wire, a refusal says why.
        static void ApplyFeedback(PoleLinkResult result, int itemType, int count)
        {
            if (result == PoleLinkResult.Linked)
                ConsumeWire(Main.LocalPlayer, itemType, count);
            else if ((byte)result >= (byte)PoleLinkResult.NotAPole)
            {
                string text = RefusalText(result);
                if (text == null)
                    return;   // no chat line for this refusal (OutOfReach: since P7c it only means 'the click was not on that pole or wire')

                if (result == lastRefusal && Main.GameUpdateCount - lastRefusalTick < (uint)RefusalRepeatTicks)
                    return;

                lastRefusal = result;
                lastRefusalTick = Main.GameUpdateCount;
                Main.NewText(text, Color.OrangeRed);
            }
        }

        static string RefusalText(PoleLinkResult result)
        {
            return result switch
            {
                PoleLinkResult.NotAPole => "Pole link: that is not a power pole.",
                PoleLinkResult.SamePole => "Pole link: both ends are the same pole.",
                PoleLinkResult.BadWire => "Pole link: that wire cannot be used.",
                PoleLinkResult.NoFreePoint => "Pole link: a pole has no free connection point.",
                PoleLinkResult.SlotTaken => "Pole link: that connection point is already used.",
                PoleLinkResult.TooLong => "Pole link: the wire is too long.",
                PoleLinkResult.OutOfReach => null,
                PoleLinkResult.NotEnoughWire => "Pole link: not enough wire.",
                PoleLinkResult.NoLink => "Pole link: there is no wire there.",
                _ => "Pole link: refused."
            };
        }

        // ---------------------------------------------------------------- wire items

        // How much of one wire item the player holds, from this process' copy of the inventory: the cursor item, then slots 0-57. On the
        // server the cursor item is slot 58 of the mirrored inventory; where the player is local it is Main.mouseItem.
        public static int CountWire(Player player, int itemType)
        {
            int total = 0;

            Item cursor = FactorrariaNet.IsOwner(player) ? Main.mouseItem : player.inventory[58];
            if (cursor != null && !cursor.IsAir && cursor.type == itemType)
                total += cursor.stack;

            for (int i = 0; i < 58; i++)
            {
                Item item = player.inventory[i];
                if (item != null && !item.IsAir && item.type == itemType)
                    total += item.stack;
            }

            return total;
        }

        /// <summary>
        /// Owner side: takes count items of one type, the cursor stack first, then the selected item, then the rest of the inventory (the
        /// order the server counted them in). On a multiplayer client each changed slot is sent to the server (slot 58 is the cursor).
        /// </summary>
        public static void ConsumeWire(Player player, int itemType, int count)
        {
            bool sync = Main.netMode == NetmodeID.MultiplayerClient;
            int left = count;

            left = TakeFrom(player, Main.mouseItem, 58, itemType, left, sync);
            left = TakeFrom(player, player.inventory[player.selectedItem], player.selectedItem, itemType, left, sync);

            for (int i = 0; i < 58 && left > 0; i++)
            {
                if (i != player.selectedItem)
                    left = TakeFrom(player, player.inventory[i], i, itemType, left, sync);
            }

            if (left > 0)
                Trace("the link cost " + count + " wire item(s) but this inventory was short by " + left);
        }

        static int TakeFrom(Player player, Item item, int slot, int itemType, int left, bool sync)
        {
            if (left <= 0 || item == null || item.IsAir || item.type != itemType)
                return left;

            int take = Math.Min(item.stack, left);
            item.stack -= take;
            if (item.stack <= 0)
                item.TurnToAir();

            if (sync)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot, item.prefix);

            return left - take;
        }

        // ---------------------------------------------------------------- join provider ("pole-links")

        public static void WriteAll(BinaryWriter writer)
        {
            IReadOnlyList<PoleLink> all = PoleLinkSystem.Instance.Links;
            writer.Write(all.Count);

            foreach (PoleLink link in all)
            {
                writer.Write(link.A.X);
                writer.Write(link.A.Y);
                writer.Write(link.SlotA);
                writer.Write(link.B.X);
                writer.Write(link.B.Y);
                writer.Write(link.SlotB);
                writer.Write((byte)link.Wire);
                writer.Write(link.Cost);
            }
        }

        public static void ReadAll(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxJoinLinks)
                throw new InvalidDataException("pole link count " + count);

            var all = new List<PoleLink>(count);
            for (int i = 0; i < count; i++)
            {
                short ax = reader.ReadInt16();
                short ay = reader.ReadInt16();
                byte slotA = reader.ReadByte();
                short bx = reader.ReadInt16();
                short by = reader.ReadInt16();
                byte slotB = reader.ReadByte();
                var wire = (CustomWireType)reader.ReadByte();
                ushort cost = reader.ReadUInt16();

                if (CustomWireNet.IsSingleKnownType(wire))
                    all.Add(new PoleLink { A = new Point16(ax, ay), SlotA = slotA, B = new Point16(bx, by), SlotB = slotB, Wire = wire, Cost = cost });
            }

            // Queued, not applied here: draw code reads the graph on the main thread, and the queue keeps this in order with later updates.
            incoming.Enqueue(new Incoming { ReplaceAll = all });
        }
    }
}