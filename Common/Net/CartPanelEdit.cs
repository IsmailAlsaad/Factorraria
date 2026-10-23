using Factorraria.Common.Carts;
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
    // Cart panel edits (roadmap patch 9.5, finding C5). The cart twin of MachineSlotEdit (patch 6.2), same rules:
    //
    // Slot click: predicted on the client with vanilla's own click code, then sent with the EXPECTED OLD item and the NEW item. The server
    // accepts only when the slot still holds the expected item (compare and swap on type, stack, prefix), the sender has this cart's panel
    // open (CartPanelSystem subscriber) and is in reach, the new item is a real item within its stack limit and, for the fuel slot, is fuel
    // within Cart.FuelSlotCap. A chest slot takes any item like the single-player panel does (the slot filter only steers conveyors and
    // pickups). The same filter rule as the single-player panel applies: a filtered slot that loses items goes back to unfiltered.
    // On accept the server writes the slot, pushes the exact detail to every subscriber and answers; on reject it answers with the
    // authoritative slot and filter and the client rolls the click back (slot, filter and, if it still holds what the click left, the cursor).
    // One edit is in flight at a time.
    //
    // Shift-click out of a slot is not predicted (the items go to the inventory): the client asks for the whole stack and gets it when the
    // server accepts.
    //
    // Insert (shift-click of an inventory item while the panel is open): the client sends the item; the server checks the player's own
    // inventory copy holds that many, adds as much as fits (TryAddToChest or TryAddFuel on a copy), pushes the detail and answers with the
    // number that moved. The client then removes exactly that many from its inventory. A refused insert changes nothing.
    //
    // Set filter: a request; the server runs SetFilterFromSlots on the real contents and pushes.
    //
    // Single-player and the host never send anything: the panel edits the cart directly.
    public static class CartSlotEdit
    {
        [Flags]
        enum EditFlags : byte
        {
            None = 0,
            QuickTake = 1
        }

        enum PendingKind : byte
        {
            Slot = 0,
            QuickTake = 1,
            Insert = 2
        }

        sealed class Pending
        {
            public int Seq;
            public int CartId;
            public PendingKind Kind;
            public bool Fuel;
            public int Index;
            public Item SlotBefore;
            public Item SlotAfter;
            public Item CursorBefore;
            public Item CursorAfter;
            public int FilterBefore;
            public int FilterAfter;
            public int InventorySlot;   // insert: where the clicked item was
            public uint Tick;
        }

        sealed class Result
        {
            public int CartId;
            public int Seq;
            public bool Accepted;
            public PendingKind Kind;
            public bool Fuel;
            public int Index;
            public Item Slot;
            public int Filter = -1;     // -1 = not sent
            public int Moved;
        }

        static readonly object pendingLock = new object();
        static Pending pending;
        static int nextSeq;
        static readonly ConcurrentQueue<Result> results = new ConcurrentQueue<Result>();

        // Temporary test counters (remove in Phase 10; shown by /fcarts).
        public static int ServerAccepted;
        public static int ServerRejected;
        public static int ServerInserts;
        public static int ServerInsertedItems;
        public static int ServerFilters;
        public static int ClientSent;
        public static int ClientConfirmed;
        public static int ClientRolledBack;
        public static int ClientInsertsSent;
        public static int ClientInsertsAccepted;
        public static int ClientInsertsRefused;
        public static int ClientInsertUnmatched;

        // ---------------------------------------------------------------- shared helpers

        public static void WriteSlotItem(BinaryWriter w, Item item)
        {
            bool present = item != null && !item.IsAir && item.stack > 0;
            w.Write(present);
            if (present)
                FactorrariaNet.WriteItem(w, item);
        }

        public static Item ReadSlotItem(BinaryReader r)
        {
            return r.ReadBoolean() ? FactorrariaNet.ReadItem(r) : new Item();
        }

        static string Describe(Item item)
        {
            return item == null || item.IsAir ? "empty" : item.stack + "x" + MachineSnapshot.SafeName(item.type);
        }

        // The single-player panel's rule (CartChestUIState.WriteChestSlot): a filtered slot that lost items goes back to unfiltered.
        static int FilterAfterEdit(int filter, Item before, Item after)
        {
            if (filter == 0)
                return 0;

            bool air = after == null || after.IsAir;
            bool beforeAir = before == null || before.IsAir;
            if (air || beforeAir || after.type != before.type || after.stack < before.stack)
                return 0;

            return filter;
        }

        static Item ReadSlot(Cart cart, bool fuel, int index)
        {
            if (fuel)
                return cart.Module == CartModule.Motor && cart.Tank == null ? cart.Fuel : null;

            return cart.Module == CartModule.Chest && cart.ChestItems != null && index >= 0 && index < cart.ChestItems.Length ? cart.ChestItems[index] : null;
        }

        static bool WriteSlot(Cart cart, bool fuel, int index, Item item)
        {
            Item value = item == null || item.IsAir ? new Item() : item;
            if (fuel)
            {
                if (cart.Module != CartModule.Motor || cart.Tank != null)
                    return false;
                cart.Fuel = value;
                return true;
            }

            if (cart.Module != CartModule.Chest || cart.ChestItems == null || index < 0 || index >= cart.ChestItems.Length)
                return false;

            cart.ChestItems[index] = value;
            return true;
        }

        static int ReadFilter(Cart cart, bool fuel, int index)
        {
            return !fuel && cart.SlotFilter != null && index >= 0 && index < cart.SlotFilter.Length ? cart.SlotFilter[index] : 0;
        }

        static void WriteFilter(Cart cart, bool fuel, int index, int value)
        {
            if (!fuel && cart.SlotFilter != null && index >= 0 && index < cart.SlotFilter.Length)
                cart.SlotFilter[index] = value;
        }

        // ---------------------------------------------------------------- client: sending

        public static bool IsBusy
        {
            get
            {
                lock (pendingLock)
                    return pending != null;
            }
        }

        /// <summary>
        /// Client, fuel slot only: keeps the predicted stack within Cart.FuelSlotCap. The excess goes to the cursor when it is empty;
        /// otherwise the click is undone (item and cursor restored) and false is returned: nothing may be sent.
        /// </summary>
        public static bool PredictFuel(ref Item item, Item slotBefore, Item cursorBefore)
        {
            if (item == null || item.IsAir || item.stack <= Cart.FuelSlotCap)
                return true;

            if (Main.mouseItem.IsAir)
            {
                Item excess = item.Clone();
                excess.stack = item.stack - Cart.FuelSlotCap;
                item.stack = Cart.FuelSlotCap;
                Main.mouseItem = excess;
                return true;
            }

            item = slotBefore.Clone();
            Main.mouseItem = cursorBefore.Clone();
            return false;
        }

        public static void SendEdit(Cart cart, bool fuel, int index, Item slotBefore, Item slotAfter, Item cursorBefore, Item cursorAfter, int filterBefore)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || cart == null)
                return;

            var p = new Pending
            {
                CartId = cart.Id,
                Kind = PendingKind.Slot,
                Fuel = fuel,
                Index = index,
                SlotBefore = slotBefore.Clone(),
                SlotAfter = slotAfter.Clone(),
                CursorBefore = cursorBefore.Clone(),
                CursorAfter = cursorAfter.Clone(),
                FilterBefore = filterBefore,
                FilterAfter = FilterAfterEdit(filterBefore, slotBefore, slotAfter)
            };
            Begin(p, EditFlags.None, p.SlotBefore, p.SlotAfter);
        }

        public static void SendQuickTake(Cart cart, bool fuel, int index, Item slot)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || cart == null || slot == null || slot.IsAir)
                return;

            var p = new Pending
            {
                CartId = cart.Id,
                Kind = PendingKind.QuickTake,
                Fuel = fuel,
                Index = index,
                SlotBefore = slot.Clone(),
                SlotAfter = new Item(),
                CursorBefore = new Item(),
                CursorAfter = new Item(),
                FilterBefore = ReadFilter(cart, fuel, index)
            };
            Begin(p, EditFlags.QuickTake, p.SlotBefore, p.SlotAfter);
        }

        static void Begin(Pending p, EditFlags flags, Item expected, Item next)
        {
            nextSeq = (nextSeq + 1) & 0xFFFFFF;
            p.Seq = nextSeq;
            p.Tick = Main.GameUpdateCount;
            lock (pendingLock)
                pending = p;

            ClientSent++;
            int cartId = p.CartId;
            int seq = p.Seq;
            bool fuel = p.Fuel;
            int index = p.Index;
            FactorrariaNet.SendRequest(FactorrariaPacket.CartPanelRequest, w =>
            {
                w.Write((byte)CartPanelAction.SlotEdit);
                w.Write(cartId);
                w.Write7BitEncodedInt(seq);
                w.Write((byte)flags);
                w.Write(fuel);
                w.Write7BitEncodedInt(index);
                WriteSlotItem(w, expected);
                WriteSlotItem(w, next);
            });
        }

        /// <summary>Client: shift-click of an inventory item while a cart panel is open. True when the click was taken (sent, or ignored while busy).</summary>
        public static bool ClientInsert(Cart cart, Item item, int inventorySlot)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || cart == null || item == null || item.IsAir)
                return false;

            if (!CartPanelNet.NeedsDetail(cart) || !CartPanelNet.IsReady(cart.Id))
                return false;

            if (IsBusy)
                return true;   // swallow the click, the previous edit is still on its way

            if (cart.Module == CartModule.Motor && !Cart.AcceptsFuel(item))
                return false;

            var p = new Pending
            {
                CartId = cart.Id,
                Kind = PendingKind.Insert,
                SlotBefore = item.Clone(),
                SlotAfter = new Item(),
                CursorBefore = new Item(),
                CursorAfter = new Item(),
                InventorySlot = inventorySlot
            };

            nextSeq = (nextSeq + 1) & 0xFFFFFF;
            p.Seq = nextSeq;
            p.Tick = Main.GameUpdateCount;
            lock (pendingLock)
                pending = p;

            ClientInsertsSent++;
            int cartId = cart.Id;
            int seq = p.Seq;
            Item sent = item.Clone();
            FactorrariaNet.SendRequest(FactorrariaPacket.CartPanelRequest, w =>
            {
                w.Write((byte)CartPanelAction.Insert);
                w.Write(cartId);
                w.Write7BitEncodedInt(seq);
                WriteSlotItem(w, sent);
            });
            return true;
        }

        /// <summary>Client: the "Set filter" button on a multiplayer client.</summary>
        public static void ClientSetFilter(Cart cart)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || cart == null)
                return;

            int cartId = cart.Id;
            FactorrariaNet.SendRequest(FactorrariaPacket.CartPanelRequest, w =>
            {
                w.Write((byte)CartPanelAction.SetFilter);
                w.Write(cartId);
            });
        }

        // ---------------------------------------------------------------- server

        // Returns null when the request may go on, otherwise why not.
        static string Check(int player, int cartId, out Cart cart)
        {
            cart = null;
            if (Main.netMode != NetmodeID.Server || player < 0 || player >= Main.maxPlayers)
                return "not on a server";

            Player who = Main.player[player];
            if (!who.active || who.dead)
                return "player not active";

            cart = CartSystem.FindById(cartId);
            if (cart == null)
                return "no such cart";

            CartPanelSystem system = ModContent.GetInstance<CartPanelSystem>();
            if (!system.IsSubscribed(player, cart))
                return "the player does not have this cart open";

            if (!system.InKeepReach(who, cart))
                return "out of reach";

            return null;
        }

        public static void ServerSlotEdit(int player, int cartId, BinaryReader reader)
        {
            int seq = reader.Read7BitEncodedInt();
            var flags = (EditFlags)reader.ReadByte();
            bool fuel = reader.ReadBoolean();
            int index = reader.Read7BitEncodedInt();
            Item expected = ReadSlotItem(reader);
            Item next = ReadSlotItem(reader);

            string why = Check(player, cartId, out Cart cart);
            if (why != null)
            {
                Reject(player, cartId, seq, fuel, index, cart, why);
                return;
            }

            Item current = ReadSlot(cart, fuel, index);
            if (current == null && !(fuel ? cart.Module == CartModule.Motor && cart.Tank == null : cart.Module == CartModule.Chest && index >= 0 && index < Cart.ChestSlots))
            {
                Reject(player, cartId, seq, fuel, index, cart, "no such slot");
                return;
            }

            current = current ?? new Item();

            if (!MachineSlotEdit.SameStack(current, expected))
            {
                Reject(player, cartId, seq, fuel, index, cart, "slot changed: expected " + Describe(expected) + ", found " + Describe(current));
                return;
            }

            if (!next.IsAir && (next.type <= ItemID.None || next.type >= ItemLoader.ItemCount || next.stack < 1 || next.stack > Math.Max(1, next.maxStack)))
            {
                Reject(player, cartId, seq, fuel, index, cart, "not a valid item or stack: " + Describe(next));
                return;
            }

            if ((flags & EditFlags.QuickTake) != 0 && !next.IsAir)
            {
                Reject(player, cartId, seq, fuel, index, cart, "a quick take must empty the slot");
                return;
            }

            if (fuel && !next.IsAir && (!Cart.AcceptsFuel(next) || next.stack > Cart.FuelSlotCap))
            {
                Reject(player, cartId, seq, fuel, index, cart, "the fuel slot does not take " + Describe(next));
                return;
            }

            if (MachineSlotEdit.SameStack(current, next))
            {
                SendResult(player, cartId, seq, true, (flags & EditFlags.QuickTake) != 0 ? PendingKind.QuickTake : PendingKind.Slot, fuel, index, null, -1, 0);
                return;
            }

            int filter = ReadFilter(cart, fuel, index);
            WriteSlot(cart, fuel, index, next);
            WriteFilter(cart, fuel, index, FilterAfterEdit(filter, current, next));

            ServerAccepted++;
            FactorrariaNet.Log("[CartPanel] " + Main.player[player].name + " edited cart " + cartId + (fuel ? " fuel slot" : " slot " + index) + ": "
                + Describe(current) + " -> " + Describe(next) + ((flags & EditFlags.QuickTake) != 0 ? " (quick take)" : ""));

            ModContent.GetInstance<CartPanelSystem>().Push(cart);
            SendResult(player, cartId, seq, true, (flags & EditFlags.QuickTake) != 0 ? PendingKind.QuickTake : PendingKind.Slot, fuel, index, null, -1, 0);
        }

        public static void ServerInsert(int player, int cartId, BinaryReader reader)
        {
            int seq = reader.Read7BitEncodedInt();
            Item item = ReadSlotItem(reader);

            string why = Check(player, cartId, out Cart cart);
            if (why == null && (item.IsAir || item.type <= ItemID.None || item.type >= ItemLoader.ItemCount || item.stack < 1 || item.stack > Math.Max(1, item.maxStack)))
                why = "not a valid item or stack: " + Describe(item);

            if (why == null && !CartPanelNet.NeedsDetail(cart))
                why = "this cart takes no items";

            if (why == null)
            {
                int held = 0;
                Player who = Main.player[player];
                for (int i = 0; i < 58; i++)
                {
                    Item candidate = who.inventory[i];
                    if (candidate != null && !candidate.IsAir && candidate.type == item.type && candidate.prefix == item.prefix)
                        held += candidate.stack;
                }

                if (held < item.stack)
                    why = "the player holds only " + held + " of " + Describe(item);
            }

            if (why != null)
            {
                ServerRejected++;
                FactorrariaNet.Log("[CartPanel] refused insert " + seq + " from " + Main.player[player].name + " into cart " + cartId + ": " + why);
                SendResult(player, cartId, seq, false, PendingKind.Insert, false, 0, null, -1, 0);
                return;
            }

            Item copy = item.Clone();
            int before = copy.stack;
            bool moved = cart.Module == CartModule.Motor ? cart.TryAddFuel(copy) : cart.TryAddToChest(copy);
            int amount = moved ? before - (copy.IsAir ? 0 : copy.stack) : 0;

            if (amount > 0)
            {
                ServerInserts++;
                ServerInsertedItems += amount;
                FactorrariaNet.Log("[CartPanel] " + Main.player[player].name + " inserted " + amount + "x" + MachineSnapshot.SafeName(item.type) + " into cart " + cartId);
                ModContent.GetInstance<CartPanelSystem>().Push(cart);
            }

            SendResult(player, cartId, seq, amount > 0, PendingKind.Insert, false, 0, null, -1, amount);
        }

        public static void ServerSetFilter(int player, int cartId)
        {
            string why = Check(player, cartId, out Cart cart);
            if (why != null || cart.Module != CartModule.Chest)
            {
                FactorrariaNet.Log("[CartPanel] refused Set filter from " + Main.player[player].name + " for cart " + cartId + ": " + (why ?? "not a chest cart"));
                return;
            }

            cart.SetFilterFromSlots();
            ServerFilters++;
            ModContent.GetInstance<CartPanelSystem>().Push(cart);
        }

        static void Reject(int player, int cartId, int seq, bool fuel, int index, Cart cart, string why)
        {
            ServerRejected++;
            FactorrariaNet.Log("[CartPanel] rejected edit " + seq + " from " + Main.player[player].name + " cart " + cartId + (fuel ? " fuel slot" : " slot " + index) + ": " + why);

            Item slot = null;
            int filter = -1;
            if (cart != null)
            {
                Item current = ReadSlot(cart, fuel, index);
                if (current != null)
                {
                    slot = current.Clone();
                    filter = ReadFilter(cart, fuel, index);
                }
            }

            SendResult(player, cartId, seq, false, PendingKind.Slot, fuel, index, slot, filter, 0);
        }

        static void SendResult(int player, int cartId, int seq, bool accepted, PendingKind kind, bool fuel, int index, Item authoritative, int filter, int moved)
        {
            FactorrariaNet.SendToClient(player, FactorrariaPacket.CartPanelResult, w =>
            {
                w.Write(cartId);
                w.Write7BitEncodedInt(seq);
                w.Write(accepted);
                w.Write((byte)kind);
                w.Write(fuel);
                w.Write7BitEncodedInt(index);
                w.Write(authoritative != null);
                if (authoritative != null)
                    WriteSlotItem(w, authoritative);
                w.Write(filter >= 0);
                if (filter >= 0)
                    w.Write7BitEncodedInt(filter);
                w.Write7BitEncodedInt(moved);
            });
        }

        // ---------------------------------------------------------------- client: the answer

        // May run on the network thread: it only queues.
        public static void HandleResult(BinaryReader reader, int sender)
        {
            var r = new Result();
            r.CartId = reader.ReadInt32();
            r.Seq = reader.Read7BitEncodedInt();
            r.Accepted = reader.ReadBoolean();
            r.Kind = (PendingKind)reader.ReadByte();
            r.Fuel = reader.ReadBoolean();
            r.Index = reader.Read7BitEncodedInt();
            r.Slot = reader.ReadBoolean() ? ReadSlotItem(reader) : null;
            r.Filter = reader.ReadBoolean() ? reader.Read7BitEncodedInt() : -1;
            r.Moved = reader.Read7BitEncodedInt();
            results.Enqueue(r);
        }

        public static void Update(int timeoutTicks)
        {
            while (results.TryDequeue(out Result result))
                ProcessResult(result);

            Pending p;
            lock (pendingLock)
                p = pending;

            if (p != null && Main.GameUpdateCount - p.Tick > (uint)Math.Max(1, timeoutTicks))
            {
                FactorrariaNet.Log("[CartPanel] no answer for edit " + p.Seq + " after " + timeoutTicks + " ticks: treating it as rejected");
                ProcessResult(new Result { CartId = p.CartId, Seq = p.Seq, Accepted = false, Kind = p.Kind, Fuel = p.Fuel, Index = p.Index });
            }
        }

        static void ProcessResult(Result r)
        {
            Pending p;
            lock (pendingLock)
            {
                if (pending == null || pending.Seq != r.Seq)
                    return;

                p = pending;
                pending = null;
            }

            Cart cart = CartSystem.FindById(p.CartId);

            if (p.Kind == PendingKind.Insert)
            {
                if (r.Accepted && r.Moved > 0)
                {
                    ClientInsertsAccepted++;
                    RemoveFromInventory(p.SlotBefore, r.Moved, p.InventorySlot);
                    SoundEngine.PlaySound(SoundID.Grab);
                }
                else
                {
                    ClientInsertsRefused++;
                }
                return;
            }

            if (r.Accepted)
            {
                ClientConfirmed++;
                if (p.Kind == PendingKind.QuickTake)
                {
                    if (cart != null && MachineSlotEdit.SameStack(ReadSlot(cart, p.Fuel, p.Index), p.SlotBefore))
                        WriteSlot(cart, p.Fuel, p.Index, new Item());
                    GiveToPlayer(p.SlotBefore);
                }
                return;
            }

            ClientRolledBack++;
            if (cart != null)
            {
                WriteSlot(cart, p.Fuel, p.Index, r.Slot != null ? r.Slot : p.SlotBefore.Clone());
                WriteFilter(cart, p.Fuel, p.Index, r.Filter >= 0 ? r.Filter : p.FilterBefore);
            }

            if (p.Kind == PendingKind.QuickTake)
            {
                FactorrariaNet.Log("[CartPanel] quick take " + p.Seq + " was rejected: the slot now shows the server's copy");
                return;
            }

            if (MachineSlotEdit.SameStack(Main.mouseItem, p.CursorAfter))
            {
                Main.mouseItem = p.CursorBefore.Clone();
                FactorrariaNet.Log("[CartPanel] edit " + p.Seq + " was rejected: slot and cursor rolled back");
            }
            else
            {
                FactorrariaNet.Log("[CartPanel] edit " + p.Seq + " was rejected but the cursor had already changed: only the slot was restored");
            }
        }

        static void GiveToPlayer(Item taken)
        {
            if (taken == null || taken.IsAir)
                return;

            Player player = Main.LocalPlayer;
            Item leftover = player.GetItem(player.whoAmI, taken.Clone(), new GetItemSettings(NoText: true));
            SoundEngine.PlaySound(SoundID.Grab);

            if (leftover != null && !leftover.IsAir)
                player.QuickSpawnItem(player.GetSource_Misc("FactorrariaCartPanel"), leftover, leftover.stack);
        }

        // Removes what the server moved into the cart: the clicked slot first, then other matching stacks. Each changed slot is synced.
        static void RemoveFromInventory(Item sample, int amount, int preferredSlot)
        {
            Player player = Main.LocalPlayer;
            int left = amount;

            void Take(int slot)
            {
                if (left <= 0 || slot < 0 || slot >= 58)
                    return;

                Item candidate = player.inventory[slot];
                if (candidate == null || candidate.IsAir || candidate.type != sample.type || candidate.prefix != sample.prefix)
                    return;

                int take = Math.Min(left, candidate.stack);
                candidate.stack -= take;
                left -= take;
                if (candidate.stack <= 0)
                    candidate.TurnToAir();

                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot, candidate.prefix);
            }

            Take(preferredSlot);
            for (int i = 0; i < 58 && left > 0; i++)
                Take(i);

            if (left > 0)
            {
                ClientInsertUnmatched += left;
                FactorrariaNet.Log("[CartPanel] insert: " + left + " item(s) the server moved were not found in the inventory");
            }
        }

        /// <summary>Client main thread: a detail packet has just overwritten the replica; while an edit is pending the predicted slot is put back.</summary>
        public static void ReapplyPending(Cart cart)
        {
            Pending p;
            lock (pendingLock)
                p = pending;

            if (p == null || p.Kind != PendingKind.Slot || cart == null || p.CartId != cart.Id)
                return;

            WriteSlot(cart, p.Fuel, p.Index, p.SlotAfter.Clone());
            WriteFilter(cart, p.Fuel, p.Index, p.FilterAfter);
        }

        public static void Reset()
        {
            lock (pendingLock)
                pending = null;

            while (results.TryDequeue(out _)) { }
            ServerAccepted = ServerRejected = ServerInserts = ServerInsertedItems = ServerFilters = 0;
            ClientSent = ClientConfirmed = ClientRolledBack = ClientInsertsSent = ClientInsertsAccepted = ClientInsertsRefused = ClientInsertUnmatched = 0;
        }
    }
}