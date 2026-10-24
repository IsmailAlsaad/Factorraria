using Factorraria.Common.Machines;
using System;
using System.Collections.Concurrent;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Which slot array of a machine an edit targets.
    public enum MachineSlotKind : byte
    {
        Input = 0,    // fuel slots first, then the ingredient slots (BaseMachine.InputSlots)
        Output = 1    // BaseMachine.OutputSlots
    }

    // Names one machine slot for UIItemSlotWrapper: which machine (resolved every frame, so a replaced replica is never held), which
    // array and which index. A wrapper that has one sends its clicks to the server on a multiplayer client (patch 6.2).
    public sealed class MachineSlotBinding
    {
        public readonly Func<BaseMachine> Machine;
        public readonly MachineSlotKind Kind;
        public readonly int Index;

        public MachineSlotBinding(Func<BaseMachine> machine, MachineSlotKind kind, int index)
        {
            Machine = machine;
            Kind = kind;
            Index = index;
        }
    }

    // Slot edits (roadmap patch 6.2, findings U1 Option A, U8 and U10).
    //
    // Authority: the server (or single-player) owns every machine slot. On a multiplayer client a click on a machine slot is PREDICTED
    // with vanilla's own ItemSlot code (so shift, right-click hold and every other click behaviour stay exactly as they are), then sent as
    // a request: machine corner, slot kind and index, the EXPECTED OLD item and the NEW item. The server accepts it only when the slot
    // still holds the expected old item (compare and swap on type, stack and prefix), the sender is a subscriber of that machine (UI open,
    // in reach), the new item is a real item within its stack limit and the machine's own filter (BaseMachine.AcceptsSlotItem) takes it.
    // On accept it writes the slot, calls MachineDetailSystem.Push (every subscriber, including the sender, gets the exact slots at
    // once) and answers the sender with an accepted result. On reject it answers with the authoritative slot.
    //
    // Rollback on a reject: the client restores the slot from the result and, when the cursor still holds exactly what the click left
    // there, puts the cursor item back to what it was before the click. Only one edit is in flight at a time (clicks on a machine slot are
    // ignored until the answer arrives, one round trip), so the saved before and after states cannot overlap.
    //
    // Shift-click is the one click that moves items somewhere that cannot be undone (the player's inventory), so it is NOT predicted: the
    // client sends "take this whole stack" and gives the items to the player only when the server accepts.
    //
    // Server conveyor intake, output release and crafting change the same slots on the server's tick. An edit is applied between ticks, so
    // either it matches the slot and wins, or the machine changed the slot first and the edit is rejected by the compare and swap and rolled
    // back. Items are never created or lost by the race: the server only ever holds the state it accepted.
    //
    // Single-player and the host do not send anything: the entity is the state and the click edits it directly.
    public static class MachineSlotEdit
    {
        [Flags]
        enum EditFlags : byte
        {
            None = 0,
            QuickTake = 1   // shift-click: new item must be empty, the client gives the stack to the player on accept
        }

        sealed class Pending
        {
            public int Seq;
            public Point16 Corner;
            public MachineSlotKind Kind;
            public int Index;
            public Item SlotBefore;
            public Item SlotAfter;
            public Item CursorBefore;
            public Item CursorAfter;
            public bool QuickTake;
            public bool Overlay;        // a detail packet that arrives while this is pending must not undo the predicted slot
            public uint Tick;
        }

        sealed class Result
        {
            public Point16 Corner;
            public int Seq;
            public bool Accepted;
            public MachineSlotKind Kind;
            public int Index;
            public Item Slot;           // the authoritative slot on a reject, null when the server had none
        }

        static readonly object pendingLock = new object();
        static Pending pending;
        static int nextSeq;
        static readonly ConcurrentQueue<Result> results = new ConcurrentQueue<Result>();

        // Test aid (temporary, set by /fslot reject, remove in Phase 10): the next request is sent with a wrong expected item, so the
        // server rejects it and the rollback path can be tested on demand.
        public static bool CorruptNextRequest;

        // Temporary test counters (remove in Phase 10).
        public static int ServerAccepted;
        public static int ServerRejected;
        public static int ClientSent;
        public static int ClientConfirmed;
        public static int ClientRolledBack;

        // ---------------------------------------------------------------- shared helpers

        /// <summary>Same item for the compare and swap: both empty, or same type, stack and prefix.</summary>
        public static bool SameStack(Item a, Item b)
        {
            bool airA = a == null || a.IsAir || a.stack <= 0;
            bool airB = b == null || b.IsAir || b.stack <= 0;
            if (airA || airB)
                return airA && airB;

            return a.type == b.type && a.stack == b.stack && a.prefix == b.prefix;
        }

        /// <summary>
        /// Slot caps for hand deposits (BaseMachine.GetSlotCap). Runs right after vanilla's click, on the client that clicked (single-player,
        /// host or predicted client), so the cap is applied in the same frame: only the allowed amount stays in the slot and the rest goes
        /// back to the cursor. If the cursor cannot take the rest back (a swap with a different item), the whole click is undone and
        /// false is returned. Taking items out, and a stack already over the cap, are never touched.
        /// </summary>
        public static bool ClampAfterClick(BaseMachine machine, MachineSlotKind kind, int index, ref Item item, Item slotBefore, Item cursorBefore)
        {
            if (machine == null || item == null || item.IsAir)
                return true;

            bool sameItem = slotBefore != null && !slotBefore.IsAir && slotBefore.type == item.type && slotBefore.prefix == item.prefix;
            int before = sameItem ? slotBefore.stack : 0;
            if (item.stack <= before)
                return true;   // nothing was added

            int limit = Math.Max(machine.GetSlotCap(kind, index, item), before);
            if (item.stack <= limit)
                return true;

            int excess = item.stack - limit;
            Item cursor = Main.mouseItem;

            if (cursor == null || cursor.IsAir)
            {
                Item back = item.Clone();
                back.stack = excess;
                item.stack = limit;
                Main.mouseItem = back;
                return true;
            }

            if (cursor.type == item.type && cursor.prefix == item.prefix && cursor.stack + excess <= cursor.maxStack)
            {
                cursor.stack += excess;
                item.stack = limit;
                return true;
            }

            item = slotBefore != null ? slotBefore.Clone() : new Item();
            Main.mouseItem = cursorBefore != null ? cursorBefore.Clone() : new Item();
            return false;
        }

        static void WriteSlotItem(BinaryWriter w, Item item)
        {
            bool present = item != null && !item.IsAir && item.stack > 0;
            w.Write(present);
            if (present)
                FactorrariaNet.WriteItem(w, item);
        }

        static Item ReadSlotItem(BinaryReader r)
        {
            return r.ReadBoolean() ? FactorrariaNet.ReadItem(r) : new Item();
        }

        static string Describe(Item item)
        {
            return item == null || item.IsAir ? "empty" : item.stack + "x" + MachineSnapshot.SafeName(item.type);
        }

        static BaseMachine FindMachine(Point16 corner)
        {
            return TileEntity.ByPosition.TryGetValue(corner, out TileEntity entity) ? entity as BaseMachine : null;
        }

        // ---------------------------------------------------------------- client: sending

        /// <summary>True while an edit is waiting for the server's answer. UIItemSlotWrapper ignores machine slot clicks meanwhile.</summary>
        public static bool IsBusy
        {
            get
            {
                lock (pendingLock)
                    return pending != null;
            }
        }

        /// <summary>
        /// Multiplayer client only. Called by UIItemSlotWrapper right after vanilla's click changed a machine slot (the local copy already
        /// holds slotAfter and the cursor holds cursorAfter). Remembers the states needed to undo it and sends the request.
        /// </summary>
        public static void SendEdit(BaseMachine machine, MachineSlotKind kind, int index, Item slotBefore, Item slotAfter, Item cursorBefore, Item cursorAfter)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || machine == null)
                return;

            var p = new Pending
            {
                Corner = machine.Position,
                Kind = kind,
                Index = index,
                SlotBefore = slotBefore.Clone(),
                SlotAfter = slotAfter.Clone(),
                CursorBefore = cursorBefore.Clone(),
                CursorAfter = cursorAfter.Clone(),
                Overlay = true
            };
            Begin(p, p.SlotBefore, p.SlotAfter, EditFlags.None);
        }

        /// <summary>Multiplayer client only. Shift-click: asks the server for the whole stack; nothing changes locally until it accepts.</summary>
        public static void SendQuickTake(BaseMachine machine, MachineSlotKind kind, int index, Item slot)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || machine == null || slot == null || slot.IsAir)
                return;

            var p = new Pending
            {
                Corner = machine.Position,
                Kind = kind,
                Index = index,
                SlotBefore = slot.Clone(),
                SlotAfter = new Item(),
                CursorBefore = new Item(),
                CursorAfter = new Item(),
                QuickTake = true,
                Overlay = false
            };
            Begin(p, p.SlotBefore, p.SlotAfter, EditFlags.QuickTake);
        }

        static void Begin(Pending p, Item expected, Item next, EditFlags flags)
        {
            nextSeq = (nextSeq + 1) & 0xFFFFFF;
            p.Seq = nextSeq;
            p.Tick = Main.GameUpdateCount;
            lock (pendingLock)
                pending = p;

            Item sentExpected = expected;
            if (CorruptNextRequest)
            {
                CorruptNextRequest = false;
                sentExpected = expected.Clone();
                if (sentExpected.IsAir)
                {
                    sentExpected.SetDefaults(ItemID.DirtBlock);
                    sentExpected.stack = 1;
                }
                else
                {
                    sentExpected.stack++;
                }
                FactorrariaNet.Log("[SlotEdit] test: edit " + p.Seq + " is sent with a wrong expected item on purpose");
            }

            ClientSent++;
            Point16 corner = p.Corner;
            MachineSlotKind kind = p.Kind;
            int index = p.Index;
            int seq = p.Seq;
            FactorrariaNet.SendRequest(FactorrariaPacket.MachineSlotEditRequest, w =>
            {
                w.Write(corner.X);
                w.Write(corner.Y);
                w.Write7BitEncodedInt(seq);
                w.Write((byte)kind);
                w.Write7BitEncodedInt(index);
                w.Write((byte)flags);
                WriteSlotItem(w, sentExpected);
                WriteSlotItem(w, next);
            });
        }

        // ---------------------------------------------------------------- server: request

        public static void HandleRequest(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            int seq = reader.Read7BitEncodedInt();
            byte kindRaw = reader.ReadByte();
            int index = reader.Read7BitEncodedInt();
            var flags = (EditFlags)reader.ReadByte();
            Item expected = ReadSlotItem(reader);
            Item next = ReadSlotItem(reader);

            if (Main.netMode != NetmodeID.Server)
                return;   // single-player and the host: the click already edited the real slot

            ApplyOnServer(sender, x, y, seq, kindRaw, index, flags, expected, next);
        }

        static void ApplyOnServer(int player, short x, short y, int seq, byte kindRaw, int index, EditFlags flags, Item expected, Item next)
        {
            // The shared machine request validation (patch 6.1): player alive, machine resolved, in reach. A slot edit may come from
            // a UI the player kept open at the edge of reach, so it gets the same extra reach an open subscription survives with.
            float keepReach = ModContent.GetInstance<MachineDetailSystem>().KeepReachExtraTiles;
            RequestCheck check = MachineRequest.Validate(player, x, y, keepReach, out BaseMachine machine, out Point16 corner);
            if (check == RequestCheck.Ignored)
                return;

            MachineSlotKind kind = kindRaw == (byte)MachineSlotKind.Output ? MachineSlotKind.Output : MachineSlotKind.Input;

            if (kindRaw > (byte)MachineSlotKind.Output)
            {
                Reject(player, corner, seq, kind, index, null, "unknown slot kind " + kindRaw);
                return;
            }

            if (check == RequestCheck.Refused)
            {
                Reject(player, corner, seq, kind, index, Authoritative(machine, kind, index), machine == null ? "no machine there" : "out of reach");
                return;
            }

            if (!ModContent.GetInstance<MachineDetailSystem>().IsSubscribed(player, machine))
            {
                Reject(player, corner, seq, kind, index, Authoritative(machine, kind, index), "the player does not have this machine open");
                return;
            }

            Item[] slots = machine.SlotsOf(kind);
            if (index < 0 || index >= slots.Length)
            {
                Reject(player, corner, seq, kind, index, null, "slot index out of range");
                return;
            }

            Item current = slots[index] ?? new Item();
            if (!SameStack(current, expected))
            {
                Reject(player, corner, seq, kind, index, current.Clone(), "slot changed: expected " + Describe(expected) + ", found " + Describe(current));
                return;
            }

            if (!next.IsAir && (next.type <= ItemID.None || next.type >= ItemLoader.ItemCount || next.stack < 1 || next.stack > Math.Max(1, next.maxStack)))
            {
                Reject(player, corner, seq, kind, index, current.Clone(), "not a valid item or stack: " + Describe(next));
                return;
            }

            if ((flags & EditFlags.QuickTake) != 0 && !next.IsAir)
            {
                Reject(player, corner, seq, kind, index, current.Clone(), "a quick take must empty the slot");
                return;
            }

            if (SameStack(current, next))
            {
                SendResult(player, corner, seq, true, kind, index, null);
                return;
            }

            // Taking items out is always allowed (also from a slot that holds something the filter would not take today); putting
            // something in must pass the machine's own filter, the same one the UI uses to refuse the cursor item.
            bool removal = next.IsAir || (!current.IsAir && next.type == current.type && next.prefix == current.prefix && next.stack <= current.stack);
            if (!removal && !machine.AcceptsSlotItem(kind, index, next))
            {
                Reject(player, corner, seq, kind, index, current.Clone(), "the machine does not accept " + Describe(next) + " in this slot");
                return;
            }

            // Slot caps: a hand deposit may not put more in than BaseMachine.GetSlotCap allows (the client clamps first, so this only
            // refuses a stale prediction or a bad packet). A stack that only shrinks or stays is a removal and never reaches this.
            if (!removal && next.stack > machine.GetSlotCap(kind, index, next))
            {
                Reject(player, corner, seq, kind, index, current.Clone(), "over the slot cap: " + Describe(next) + " (cap " + machine.GetSlotCap(kind, index, next) + ")");
                return;
            }

            slots[index] = next.IsAir ? new Item() : next;
            ServerAccepted++;
            FactorrariaNet.Log("[SlotEdit] " + Main.player[player].name + " edited " + machine.GetType().Name + " at (" + corner.X + ", " + corner.Y + ") "
                + kind + " slot " + index + ": " + Describe(current) + " -> " + Describe(next) + ((flags & EditFlags.QuickTake) != 0 ? " (quick take)" : ""));

            // Detail to every subscriber (the sender too) first, then the answer, so the sender's replica already shows the new slot when it arrives.
            ModContent.GetInstance<MachineDetailSystem>().Push(machine);
            SendResult(player, corner, seq, true, kind, index, null);
        }

        static Item Authoritative(BaseMachine machine, MachineSlotKind kind, int index)
        {
            if (machine == null)
                return null;

            Item[] slots = machine.SlotsOf(kind);
            return index >= 0 && index < slots.Length ? (slots[index] ?? new Item()).Clone() : null;
        }

        static void Reject(int player, Point16 corner, int seq, MachineSlotKind kind, int index, Item authoritative, string why)
        {
            ServerRejected++;
            FactorrariaNet.Log("[SlotEdit] rejected edit " + seq + " from " + Main.player[player].name + " at (" + corner.X + ", " + corner.Y + ") "
                + kind + " slot " + index + ": " + why);
            SendResult(player, corner, seq, false, kind, index, authoritative);
        }

        static void SendResult(int player, Point16 corner, int seq, bool accepted, MachineSlotKind kind, int index, Item authoritative)
        {
            FactorrariaNet.SendToClient(player, FactorrariaPacket.MachineSlotEditResult, w =>
            {
                w.Write(corner.X);
                w.Write(corner.Y);
                w.Write7BitEncodedInt(seq);
                w.Write(accepted);
                w.Write((byte)kind);
                w.Write7BitEncodedInt(index);
                w.Write(authoritative != null);
                if (authoritative != null)
                    WriteSlotItem(w, authoritative);
            });
        }

        // ---------------------------------------------------------------- client: the answer

        // May run on the network thread: it only queues. Update (main thread) does the work.
        public static void HandleResult(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            int seq = reader.Read7BitEncodedInt();
            bool accepted = reader.ReadBoolean();
            var kind = (MachineSlotKind)reader.ReadByte();
            int index = reader.Read7BitEncodedInt();
            Item slot = reader.ReadBoolean() ? ReadSlotItem(reader) : null;

            results.Enqueue(new Result { Corner = new Point16(x, y), Seq = seq, Accepted = accepted, Kind = kind, Index = index, Slot = slot });
        }

        /// <summary>Client main thread, every tick: handles the queued answers and rejects an edit the server never answered.</summary>
        public static void Update(int timeoutTicks)
        {
            while (results.TryDequeue(out Result result))
                ProcessResult(result);

            Pending p;
            lock (pendingLock)
                p = pending;

            if (p != null && Main.GameUpdateCount - p.Tick > (uint)Math.Max(1, timeoutTicks))
            {
                // The server answers every edit it processes and the connection does not lose packets, so an edit with no answer was dropped
                // (rate limit, an exception, a player the server did not accept): treat it as rejected, which undoes the click.
                FactorrariaNet.Log("[SlotEdit] no answer for edit " + p.Seq + " after " + timeoutTicks + " ticks: treating it as rejected");
                ProcessResult(new Result { Corner = p.Corner, Seq = p.Seq, Accepted = false, Kind = p.Kind, Index = p.Index, Slot = null });
            }
        }

        static void ProcessResult(Result r)
        {
            Pending p;
            lock (pendingLock)
            {
                if (pending == null || pending.Seq != r.Seq)
                    return;   // an answer for an edit that was already dropped

                p = pending;
                pending = null;
            }

            BaseMachine machine = FindMachine(p.Corner);
            Item[] slots = machine?.SlotsOf(p.Kind);
            bool slotOk = slots != null && p.Index >= 0 && p.Index < slots.Length;

            if (r.Accepted)
            {
                ClientConfirmed++;
                if (p.QuickTake)
                {
                    if (slotOk && SameStack(slots[p.Index], p.SlotBefore))
                        slots[p.Index] = new Item();
                    GiveToPlayer(p.SlotBefore);
                }
                return;
            }

            ClientRolledBack++;
            if (slotOk)
                slots[p.Index] = r.Slot != null ? r.Slot : p.SlotBefore.Clone();

            if (p.QuickTake)
            {
                FactorrariaNet.Log("[SlotEdit] quick take " + p.Seq + " was rejected: nothing had changed locally, the slot now shows the server's copy");
                return;
            }

            if (SameStack(Main.mouseItem, p.CursorAfter))
            {
                Main.mouseItem = p.CursorBefore.Clone();
                FactorrariaNet.Log("[SlotEdit] edit " + p.Seq + " was rejected: the slot and the cursor item were rolled back");
            }
            else
            {
                FactorrariaNet.Log("[SlotEdit] edit " + p.Seq + " was rejected but the cursor item had already changed (" + Describe(Main.mouseItem) + "): only the slot was restored");
            }
        }

        static void GiveToPlayer(Item taken)
        {
            if (taken == null || taken.IsAir)
                return;

            Player player = Main.LocalPlayer;
            Item leftover = player.GetItem(player.whoAmI, taken.Clone(), new GetItemSettings(NoText: true));
            SoundEngine.PlaySound(SoundID.Grab);

            // The inventory can have filled up during the round trip: whatever does not fit drops next to the player instead of vanishing.
            if (leftover != null && !leftover.IsAir)
                player.QuickSpawnItem(player.GetSource_Misc("FactorrariaMachineSlot"), leftover, leftover.stack);
        }

        /// <summary>
        /// Network thread. A detail packet has just overwritten the replica's slots; while an edit is pending the predicted slot is put back
        /// so the item does not flicker out of the slot (and into view twice) until the answer arrives.
        /// </summary>
        public static void ReapplyPendingEdit(BaseMachine machine)
        {
            Pending p;
            lock (pendingLock)
                p = pending;

            if (p == null || !p.Overlay || machine == null || p.Corner != machine.Position)
                return;

            Item[] slots = machine.SlotsOf(p.Kind);
            if (p.Index >= 0 && p.Index < slots.Length)
                slots[p.Index] = p.SlotAfter.Clone();
        }

        public static void Reset()
        {
            lock (pendingLock)
                pending = null;

            while (results.TryDequeue(out _)) { }
            CorruptNextRequest = false;
        }

        // Test aid (temporary, used by /fslot).
        public static string Describe()
        {
            return Main.netMode == NetmodeID.Server
                ? "slot edits: accepted " + ServerAccepted + ", rejected " + ServerRejected
                : "slot edits: sent " + ClientSent + ", confirmed " + ClientConfirmed + ", rolled back " + ClientRolledBack + ", busy " + IsBusy;
        }
    }

    public class MachineSlotEditSystem : ModSystem
    {
        // Tuning knob (instance field on purpose, so hot reload can change it): how long a client waits for the answer to a slot edit.
        public int AnswerTimeoutTicks = 120;

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.MachineSlotEditRequest, PacketDirection.ToServer, MachineSlotEdit.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.MachineSlotEditResult, PacketDirection.ToClient, MachineSlotEdit.HandleResult);
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                MachineSlotEdit.Update(AnswerTimeoutTicks);
        }

        public override void OnWorldUnload()
        {
            MachineSlotEdit.Reset();
        }

        public override void Unload()
        {
            MachineSlotEdit.Reset();
        }
    }
}