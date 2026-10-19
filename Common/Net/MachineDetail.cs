using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Why a subscription ended. Written to the client in MachineDetailClosed; the client closes its machine UI for every reason.
    public enum DetailCloseReason : byte
    {
        Closed = 0,       // reserved: the client asked
        Killed = 1,       // the machine was broken or removed
        OutOfReach = 2,   // the player walked too far away
        Dead = 3,         // the player died
        Full = 4,         // the machine already has the maximum number of subscribers
        Rejected = 5      // no machine at that position, or not in reach when the request arrived
    }

    // Machine detail channel (roadmap patch 5.3, findings M3 channel 3, M4 and U3 server half, and the part of M6 left over from 4.4).
    //
    // Authority: the server (or single-player) owns every machine's state. A client replica is read-only. The light channel (5.2) keeps
    // the VISIBLE state of machines near a player current but rounds progress and tank levels and never sends slots. A player who has a
    // machine's UI open needs the exact state, so the server keeps a SUBSCRIBER LIST per machine and sends that player the full detail
    // (BaseMachine.BuildDetail: flags, exact progress, crafts, use fraction, manual group and running recipe, every input and output
    // slot, exact tank amounts, fuel) when they subscribe and then again whenever it changes, whatever changed it (a recipe finishing,
    // a conveyor feeding the machine, another player's edit once 6.2 exists). 6.2 can also call MachineDetailSystem.Push(machine) to
    // send an accepted edit at once instead of waiting for the next scan.
    //
    // Split with Phase 6.1: this patch adds the TRANSPORT and the server list. Packet MachineSubscribeRequest (client -> server: x, y,
    // open or close) is sent by MachineUISystem when a multiplayer client opens or closes a machine UI; it is deliberately thin (6.1
    // extends it: EnsureEntity, one validation helper for the whole request family, the UI re-resolving the live replica every frame,
    // client-side reach and death closing). Nothing here edits a machine; slot edits, recipe picks and toggles are 6.2 to 6.4.
    //
    // Subscription rules: a player is subscribed to at most one machine (the UI is single); a machine takes at most
    // MaxSubscribersPerMachine players; a request must come from an active, living player inside reach of the machine's footprint.
    // The server ends a subscription when the player leaves (PlayerDisconnect, or the slot is no longer active), dies, walks further than
    // reach + KeepReachExtraTiles away, or the machine is killed (BaseMachine.OnKill, which covers every kill path, including the
    // Robotic Arm). Every end except leaving sends MachineDetailClosed, and the client closes the open machine UI.
    //
    // Single-player (and Host & Play's own player) never subscribes: the entity IS the state, so nothing is sent and the system idles.
    //
    // Changes are found by polling (every ScanIntervalTicks) on subscribed machines only: the payload is built, compared with the last
    // one, and sent only to subscribers whose copy is older. An unchanged machine and a machine nobody has open cost nothing.
    public class MachineDetailSystem : ModSystem
    {
        // Tuning knobs. Instance fields on purpose, so hot reload can change them.
        public int ScanIntervalTicks = 3;            // how often subscribed machines are compared (3 = at most 20 detail packets per second per subscriber)
        public int MaxSubscribersPerMachine = 8;     // cap per machine
        public float KeepReachExtraTiles = 3f;       // an open subscription survives this much further than a new one may start

        sealed class Subscriber
        {
            public int Player;
            public uint SentVersion;
        }

        sealed class Record
        {
            public BaseMachine Machine;
            public Point16 Corner;
            public byte[] Last;
            public uint Version;
            public readonly List<Subscriber> Subscribers = new List<Subscriber>();
        }

        readonly Dictionary<int, Record> records = new Dictionary<int, Record>();   // machine id -> record
        readonly int[] playerMachine = new int[Main.maxPlayers];                    // machine id the player is subscribed to, -1 = none
        readonly bool[] wasActive = new bool[Main.maxPlayers];
        readonly List<Record> scratch = new List<Record>();

        public override void Load()
        {
            Array.Fill(playerMachine, -1);

            FactorrariaNet.Register(FactorrariaPacket.MachineSubscribeRequest, PacketDirection.ToServer, MachineDetail.HandleSubscribe);
            FactorrariaNet.Register(FactorrariaPacket.MachineDetail, PacketDirection.ToClient, MachineDetail.HandleDetail);
            FactorrariaNet.Register(FactorrariaPacket.MachineDetailClosed, PacketDirection.ToClient, MachineDetail.HandleClosed);
        }

        // ---------------------------------------------------------------- server: requests

        public void OnSubscribeRequest(int player, int x, int y, bool open)
        {
            if (Main.netMode != NetmodeID.Server || player < 0 || player >= Main.maxPlayers)
                return;

            if (!open)
            {
                // Only end the subscription the player really has at that position (a close for a machine they already left is ignored).
                if (playerMachine[player] >= 0 && records.TryGetValue(playerMachine[player], out Record current) && current.Corner == FindCorner(x, y))
                    Unsubscribe(player, "closed by the player");
                return;
            }

            // Patch 6.1: the shared validation (also creates and shares a missing entity for a machine tile).
            RequestCheck check = MachineRequest.Validate(player, x, y, 0f, out BaseMachine machine, out Point16 corner);
            if (check == RequestCheck.Ignored)
                return;

            if (check == RequestCheck.Refused)
            {
                MachineDetail.Rejected++;
                SendClosed(player, corner, DetailCloseReason.Rejected);
                return;
            }

            Player who = Main.player[player];

            // Subscribing again to the machine the player already has just refreshes their copy.
            if (playerMachine[player] != machine.ID)
                Unsubscribe(player, "switched machine");

            if (!records.TryGetValue(machine.ID, out Record record))
                records[machine.ID] = record = new Record { Machine = machine, Corner = machine.Position };

            Subscriber subscriber = record.Subscribers.Find(s => s.Player == player);
            if (subscriber == null)
            {
                if (record.Subscribers.Count >= Math.Max(1, MaxSubscribersPerMachine))
                {
                    MachineDetail.Rejected++;
                    if (record.Subscribers.Count == 0)
                        records.Remove(machine.ID);
                    SendClosed(player, corner, DetailCloseReason.Full);
                    return;
                }

                subscriber = new Subscriber { Player = player };
                record.Subscribers.Add(subscriber);
                playerMachine[player] = machine.ID;
                MachineDetail.Opened++;
                FactorrariaNet.Log("[Detail] " + machine.GetType().Name + " at (" + corner.X + ", " + corner.Y + ") subscribed by " + who.name + " (" + record.Subscribers.Count + " subscriber(s))");
            }

            Refresh(record);
            SendDetail(player, record);
            subscriber.SentVersion = record.Version;
        }

        static Point16 FindCorner(int x, int y)
        {
            return WorldGen.InWorld(x, y, 1) && TileEntityHelper.TryGetEntityFromTile(x, y, out _, out Point16 corner) ? corner : new Point16((short)x, (short)y);
        }

        /// <summary>Server only. Sends the current detail of this machine to every subscriber now (6.2 calls it after an accepted edit).</summary>
        public void Push(BaseMachine machine)
        {
            if (Main.netMode != NetmodeID.Server || machine == null || !records.TryGetValue(machine.ID, out Record record))
                return;

            Refresh(record);
            SendToSubscribers(record);
        }

        /// <summary>Server only (patch 6.2): true when this player currently has this machine's UI open, that is, they are on its subscriber list.</summary>
        public bool IsSubscribed(int player, BaseMachine machine)
        {
            return player >= 0 && player < Main.maxPlayers && machine != null && playerMachine[player] == machine.ID
                && records.TryGetValue(machine.ID, out Record record) && record.Subscribers.Exists(s => s.Player == player);
        }

        // ---------------------------------------------------------------- server: lifetime

        /// <summary>Called from BaseMachine.OnKill (server only, every kill path): closes the UI of everyone who has this machine open.</summary>
        public static void NotifyMachineKilled(BaseMachine machine)
        {
            if (Main.netMode != NetmodeID.Server || machine == null)
                return;

            ModContent.GetInstance<MachineDetailSystem>().EndAll(machine.ID, DetailCloseReason.Killed);
        }

        /// <summary>Called from FactorrariaPlayerSync.PlayerDisconnect (server): the player left, nothing to tell them.</summary>
        public static void OnPlayerLeft(int player)
        {
            if (Main.netMode != NetmodeID.Server || player < 0 || player >= Main.maxPlayers)
                return;

            ModContent.GetInstance<MachineDetailSystem>().Unsubscribe(player, "left the game");
        }

        void EndAll(int machineId, DetailCloseReason reason)
        {
            if (!records.TryGetValue(machineId, out Record record))
                return;

            foreach (Subscriber subscriber in record.Subscribers)
            {
                playerMachine[subscriber.Player] = -1;
                SendClosed(subscriber.Player, record.Corner, reason);
                MachineDetail.Ended++;
            }

            FactorrariaNet.Log("[Detail] " + record.Machine.GetType().Name + " at (" + record.Corner.X + ", " + record.Corner.Y + ") ended for " + record.Subscribers.Count + " subscriber(s): " + reason);
            records.Remove(machineId);
        }

        void Unsubscribe(int player, string why)
        {
            int id = playerMachine[player];
            if (id < 0)
                return;

            playerMachine[player] = -1;
            if (!records.TryGetValue(id, out Record record))
                return;

            record.Subscribers.RemoveAll(s => s.Player == player);
            MachineDetail.Ended++;
            FactorrariaNet.Log("[Detail] " + record.Machine.GetType().Name + " at (" + record.Corner.X + ", " + record.Corner.Y + ") unsubscribed player " + player + " (" + why + ")");

            if (record.Subscribers.Count == 0)
                records.Remove(id);
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            uint interval = (uint)Math.Max(1, ScanIntervalTicks);
            if (Main.GameUpdateCount % interval != 0)
                return;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                bool active = Main.player[i].active;
                if (wasActive[i] && !active)
                    Unsubscribe(i, "slot no longer active");
                wasActive[i] = active;
            }

            if (records.Count == 0)
                return;

            scratch.Clear();
            scratch.AddRange(records.Values);

            foreach (Record record in scratch)
            {
                // A machine that is no longer the live entity (killed without OnKill reaching us, or replaced): end for everyone.
                if (!(TileEntity.ByID.TryGetValue(record.Machine.ID, out TileEntity live) && ReferenceEquals(live, record.Machine)))
                {
                    EndAll(record.Machine.ID, DetailCloseReason.Killed);
                    continue;
                }

                for (int s = record.Subscribers.Count - 1; s >= 0; s--)
                {
                    int player = record.Subscribers[s].Player;
                    Player who = Main.player[player];

                    if (!who.active)
                    {
                        Unsubscribe(player, "player gone");
                    }
                    else if (who.dead)
                    {
                        Unsubscribe(player, "player died");
                        SendClosed(player, record.Corner, DetailCloseReason.Dead);
                    }
                    else if (!WithinReach(who, record.Machine, KeepReachExtraTiles))
                    {
                        Unsubscribe(player, "out of reach");
                        SendClosed(player, record.Corner, DetailCloseReason.OutOfReach);
                    }
                }

                if (record.Subscribers.Count == 0)
                {
                    records.Remove(record.Machine.ID);
                    continue;
                }

                Refresh(record);
                SendToSubscribers(record);
            }
        }

        // ---------------------------------------------------------------- server: sending

        static void Refresh(Record record)
        {
            byte[] payload = record.Machine.BuildDetail();
            if (record.Last == null || !payload.AsSpan().SequenceEqual(record.Last))
            {
                record.Last = payload;
                record.Version++;
            }
        }

        void SendToSubscribers(Record record)
        {
            foreach (Subscriber subscriber in record.Subscribers)
            {
                if (subscriber.SentVersion == record.Version)
                    continue;

                SendDetail(subscriber.Player, record);
                subscriber.SentVersion = record.Version;
            }
        }

        static void SendDetail(int player, Record record)
        {
            byte[] payload = record.Last;
            Point16 corner = record.Corner;
            FactorrariaNet.SendToClient(player, FactorrariaPacket.MachineDetail, w =>
            {
                w.Write(corner.X);
                w.Write(corner.Y);
                w.Write7BitEncodedInt(payload.Length);
                w.Write(payload);
            });

            Interlocked.Increment(ref MachineDetail.Sent);
            Interlocked.Add(ref MachineDetail.SentBytes, payload.Length);
        }

        static void SendClosed(int player, Point16 corner, DetailCloseReason reason)
        {
            FactorrariaNet.SendToClient(player, FactorrariaPacket.MachineDetailClosed, w =>
            {
                w.Write(corner.X);
                w.Write(corner.Y);
                w.Write((byte)reason);
            });

            Interlocked.Increment(ref MachineDetail.ClosedSent);
        }

        // Reach to the nearest tile of the machine's footprint (a 3x3 machine is used from its edge, not its centre). Same rules as
        // FactorrariaNet.InReach plus extraTiles.
        internal static bool WithinReach(Player player, BaseMachine machine, float extraTiles)
        {
            if (player == null || !player.active || player.dead)
                return false;

            int width = Math.Max(1, machine.MachineWidth);
            int height = Math.Max(1, machine.MachineHeight);
            float px = player.Center.X / 16f;
            float py = player.Center.Y / 16f;
            float nearestX = Math.Clamp(px, machine.Position.X + 0.5f, machine.Position.X + width - 0.5f);
            float nearestY = Math.Clamp(py, machine.Position.Y + 0.5f, machine.Position.Y + height - 0.5f);

            float extra = FactorrariaNet.ReachSlackTiles + player.blockRange + Math.Max(0, player.HeldItem?.tileBoost ?? 0) + extraTiles;
            return Math.Abs(px - nearestX) <= Player.tileRangeX + extra && Math.Abs(py - nearestY) <= Player.tileRangeY + extra;
        }

        // Test aid (temporary, used by /fsnap): who is subscribed to what, on the server.
        public string DescribeSubscriptions()
        {
            if (records.Count == 0)
                return "none";

            var sb = new StringBuilder();
            foreach (Record record in records.Values)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(record.Machine.GetType().Name).Append('@').Append(record.Corner.X).Append(',').Append(record.Corner.Y).Append(" <-");
                foreach (Subscriber subscriber in record.Subscribers)
                    sb.Append(' ').Append(Main.player[subscriber.Player].name);
            }
            return sb.ToString();
        }

        public override void OnWorldUnload()
        {
            ResetState();
        }

        public override void Unload()
        {
            ResetState();
        }

        void ResetState()
        {
            records.Clear();
            Array.Fill(playerMachine, -1);
            Array.Clear(wasActive, 0, wasActive.Length);
            scratch.Clear();
        }
    }

    // Packet handlers and the client side of the detail channel (see the system above for the design).
    public static class MachineDetail
    {
        // How long (ticks) after a detail packet the light updates leave that replica alone. The detail carries the same fields with
        // exact values, and a light update (rounded progress) arriving in between would make a progress bar step backwards.
        public static int FreshTicks = 30;

        // Temporary test counters for /fsnap (remove in Phase 10 with the command).
        public static int Opened;          // server: subscriptions started
        public static int Ended;           // server: subscriptions ended (closed, left, dead, out of reach, killed, switched)
        public static int Rejected;        // server: requests refused (no machine, out of reach, full)
        public static int Sent;            // server: detail packets
        public static long SentBytes;
        public static int ClosedSent;      // server: MachineDetailClosed packets
        public static int Received;        // client: detail packets
        public static int Applied;
        public static int Missed;          // client: detail for a position with no machine
        public static int Failed;
        public static int ClosedReceived;  // client: close notices

        // ---------------------------------------------------------------- client -> server

        /// <summary>Multiplayer client only: tells the server this player opened or closed the UI of the machine whose corner is at that position.</summary>
        public static void SendSubscribe(Point16 corner, bool open)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;   // single-player and the host: the entity is the real state, nothing to subscribe to

            FactorrariaNet.SendRequest(FactorrariaPacket.MachineSubscribeRequest, w =>
            {
                w.Write(corner.X);
                w.Write(corner.Y);
                w.Write(open);
            });
        }

        public static void HandleSubscribe(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            bool open = reader.ReadBoolean();
            ModContent.GetInstance<MachineDetailSystem>().OnSubscribeRequest(sender, x, y, open);
        }

        // ---------------------------------------------------------------- server -> client

        // Writes the exact state into the live replica in place. Never replaces the entity, never re-shares, never sends anything.
        public static void HandleDetail(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            int length = reader.Read7BitEncodedInt();
            byte[] bytes = length > 0 ? reader.ReadBytes(length) : Array.Empty<byte>();
            Interlocked.Increment(ref Received);

            if (!TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TileEntity entity) || !(entity is BaseMachine machine))
            {
                Interlocked.Increment(ref Missed);
                return;
            }

            try
            {
                using var stream = new MemoryStream(bytes);
                using var payloadReader = new BinaryReader(stream);
                machine.ApplyDetail(payloadReader);
                MachineSlotEdit.ReapplyPendingEdit(machine);   // patch 6.2: a slot edit still waiting for its answer stays visible
                machine.LastDetailTick = Main.GameUpdateCount;
                Interlocked.Increment(ref Applied);
            }
            catch (Exception e)
            {
                Interlocked.Increment(ref Failed);
                FactorrariaNet.Log("[Machines] detail for " + machine.GetType().Name + " at (" + x + ", " + y + ") could not be read: " + e.Message);
            }
        }

        // The UI is closed from the main thread (MachineUISystem.UpdateUI), not from here: this may run on the network thread.
        public static void HandleClosed(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            var reason = (DetailCloseReason)reader.ReadByte();
            Interlocked.Increment(ref ClosedReceived);
            // Walking out of reach closes like clicking the machine again (sound, inventory); the other reasons close silently.
            ModContent.GetInstance<MachineUISystem>().RequestCloseFromServer(x, y, reason == DetailCloseReason.OutOfReach);
        }

        // Test aid (temporary, used by /fsnap).
        public static string Describe()
        {
            string text = "detail: ";
            if (Main.netMode == NetmodeID.Server)
            {
                text += "subscriptions started " + Opened + ", ended " + Ended + ", rejected " + Rejected + ", packets sent " + Sent + " (" + SentBytes + " B), close notices " + ClosedSent
                    + ", now: " + ModContent.GetInstance<MachineDetailSystem>().DescribeSubscriptions();
            }
            else
            {
                text += "received " + Received + " (applied " + Applied + ", missed " + Missed + ", failed " + Failed + "), close notices " + ClosedReceived;
            }
            return text;
        }
    }
}