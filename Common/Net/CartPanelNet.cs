using Factorraria.Common.Carts;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // What a CartPanelRequest asks for (the byte after the packet id).
    public enum CartPanelAction : byte
    {
        Open = 0,       // the player opened the panel of this cart: subscribe them
        Close = 1,      // the player closed it
        SlotEdit = 2,   // a click on a chest slot or the fuel slot (compare and swap, see CartSlotEdit)
        Insert = 3,     // shift-click of an inventory item into the open cart (chest, or the fuel slot of a motor cart)
        SetFilter = 4   // the "Set filter" button
    }

    // Why the server ended a panel subscription. Written to the client in CartPanelClosed; the client closes its panel for every reason.
    public enum CartPanelCloseReason : byte
    {
        Closed = 0,       // reserved: the client asked
        Gone = 1,         // the cart was picked up or removed
        OutOfReach = 2,   // the player walked too far away
        Dead = 3,         // the player died
        Full = 4,         // the cart already has the maximum number of subscribers
        Rejected = 5      // no such cart, no panel to subscribe to, or not in reach when the request arrived
    }

    // Names one cart slot for UIItemSlotWrapper: which cart (resolved every frame), the chest slot index or the fuel slot. A wrapper that
    // has one sends its clicks to the server on a multiplayer client (patch 9.5), like MachineSlotBinding does for machines.
    public sealed class CartSlotBinding
    {
        public readonly Func<Cart> GetCart;
        public readonly bool Fuel;
        public readonly int Index;

        public CartSlotBinding(Func<Cart> getCart, bool fuel, int index)
        {
            GetCart = getCart;
            Fuel = fuel;
            Index = index;
        }
    }

    // Cart panel channel (roadmap patch 9.5, finding C5; the cart twin of MachineDetailSystem from 5.3).
    //
    // Authority: the server (or single-player) owns every cart's chest contents, slot filters and fuel slot. A multiplayer client only shows
    // replicas (patch 9.2) and asks. A cart that needs this channel is a chest cart and a motor cart that burns items; a terrarium or a
    // liquid motor shows its tank, which already travels in CartDelta, so opening its panel needs no subscription.
    //
    // Subscription: when a client opens such a panel it sends CartPanelRequest(Open, cart id). The server checks the player (active, alive),
    // that the cart exists, has a panel and is in reach, and that the cart has room for another subscriber, then lists the player as a
    // subscriber (a player has at most one panel open) and sends the full detail: the six chest slots and their filters, or the fuel slot and
    // the burn fraction. After that, every ScanIntervalTicks the server builds the detail of each subscribed cart, compares the bytes with
    // the last one and sends only a changed payload, whatever changed it (a cart picking up items, a bump spilling a slot, the fuel burning,
    // another player's edit). An unchanged cart and a cart nobody has open cost nothing. Slot edits (CartSlotEdit) call Push at once.
    //
    // The server ends a subscription, with a reason, when the cart is gone (picked up, removed, no longer the live cart), the player walks
    // further than CloseRangeTiles + ReachSlackTiles + KeepReachExtraTiles away, dies or leaves. The client re-resolves the cart by id every
    // frame and closes its panel for every reason (and when the replica is gone or was replaced).
    //
    // Single-player (and the host) never subscribes: the cart is the state and the panel edits it directly.
    public class CartPanelSystem : ModSystem
    {
        // Tuning knobs. Instance fields on purpose, so hot reload can change them.
        public int ScanIntervalTicks = 3;            // how often subscribed carts are compared (3 = at most 20 detail packets per second per subscriber)
        public int MaxSubscribersPerCart = 8;
        public float CloseRangeTiles = 10f;          // the client closes its panel at this distance (CartChestUISystem.CloseRange)
        public float KeepReachExtraTiles = 3f;       // an open subscription survives this much further than the client's own close range
        public int AnswerTimeoutTicks = 120;         // client: how long a slot edit waits for its answer

        sealed class Subscriber
        {
            public int Player;
            public uint SentVersion;
        }

        sealed class Record
        {
            public Cart Cart;
            public int Id;
            public byte[] Last;
            public uint Version;
            public readonly List<Subscriber> Subscribers = new List<Subscriber>();
        }

        readonly Dictionary<int, Record> records = new Dictionary<int, Record>();   // cart id -> record
        readonly int[] playerCart = new int[Main.maxPlayers];                       // cart id the player has open, 0 = none
        readonly bool[] wasActive = new bool[Main.maxPlayers];
        readonly List<Record> scratch = new List<Record>();

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.CartPanelRequest, PacketDirection.ToServer, CartPanelNet.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.CartPanelDetail, PacketDirection.ToClient, CartPanelNet.HandleDetail);
            FactorrariaNet.Register(FactorrariaPacket.CartPanelResult, PacketDirection.ToClient, CartSlotEdit.HandleResult);
            FactorrariaNet.Register(FactorrariaPacket.CartPanelClosed, PacketDirection.ToClient, CartPanelNet.HandleClosed);
            JoinSync.RegisterDisconnectHook(OnPlayerLeft);
        }

        // ---------------------------------------------------------------- server: requests

        public void OnOpenRequest(int player, int cartId)
        {
            if (Main.netMode != NetmodeID.Server || player < 0 || player >= Main.maxPlayers)
                return;

            Player who = Main.player[player];
            Cart cart = CartSystem.FindById(cartId);

            if (!who.active || who.dead || cart == null || !CartPanelNet.NeedsDetail(cart)
                || !CartSystem.IsInInteractRange(who, cart, FactorrariaNet.ReachSlackTiles * 16f))
            {
                CartPanelNet.ServerRefusedOpen++;
                FactorrariaNet.Log("[CartPanel] refused Open from " + who.name + " for cart " + cartId + ": "
                    + (cart == null ? "no such cart" : !CartPanelNet.NeedsDetail(cart) ? "that cart has no panel to subscribe to" : "out of reach or dead"));
                SendClosed(player, cartId, CartPanelCloseReason.Rejected);
                return;
            }

            // Subscribing again to the cart the player already has open just refreshes their copy.
            if (playerCart[player] != 0 && playerCart[player] != cartId)
                Unsubscribe(player, "switched cart");

            if (records.TryGetValue(cartId, out Record existing) && !ReferenceEquals(existing.Cart, cart))
                EndAll(cartId, CartPanelCloseReason.Gone);   // a stale record for a replaced cart object

            if (!records.TryGetValue(cartId, out Record record))
                records[cartId] = record = new Record { Cart = cart, Id = cartId };

            Subscriber subscriber = record.Subscribers.Find(s => s.Player == player);
            if (subscriber == null)
            {
                if (record.Subscribers.Count >= Math.Max(1, MaxSubscribersPerCart))
                {
                    CartPanelNet.ServerRefusedOpen++;
                    if (record.Subscribers.Count == 0)
                        records.Remove(cartId);
                    SendClosed(player, cartId, CartPanelCloseReason.Full);
                    return;
                }

                subscriber = new Subscriber { Player = player };
                record.Subscribers.Add(subscriber);
                playerCart[player] = cartId;
                CartPanelNet.ServerOpened++;
                FactorrariaNet.Log("[CartPanel] cart " + cartId + " opened by " + who.name + " (" + record.Subscribers.Count + " subscriber(s))");
            }

            Refresh(record);
            SendDetail(player, record);
            subscriber.SentVersion = record.Version;
        }

        public void OnCloseRequest(int player, int cartId)
        {
            if (Main.netMode != NetmodeID.Server || player < 0 || player >= Main.maxPlayers)
                return;

            // Only end the subscription the player really has (a close for a cart they already left is ignored).
            if (playerCart[player] == cartId)
                Unsubscribe(player, "closed by the player");
        }

        /// <summary>Server only: true when this player currently has this cart's panel open (is on its subscriber list).</summary>
        public bool IsSubscribed(int player, Cart cart)
        {
            return player >= 0 && player < Main.maxPlayers && cart != null && playerCart[player] == cart.Id
                && records.TryGetValue(cart.Id, out Record record) && ReferenceEquals(record.Cart, cart) && record.Subscribers.Exists(s => s.Player == player);
        }

        /// <summary>Server only: is the player still close enough to keep (or use) an open panel? Slightly more generous than the client's own close range.</summary>
        public bool InKeepReach(Player player, Cart cart)
        {
            if (player == null || !player.active || player.dead || cart == null || !cart.Active)
                return false;

            float pixels = (CloseRangeTiles + FactorrariaNet.ReachSlackTiles + KeepReachExtraTiles) * 16f;
            return Vector2.Distance(player.Center, cart.Position) <= pixels;
        }

        /// <summary>Server only. Sends the current detail of this cart to every subscriber now (an accepted edit calls it instead of waiting for the next scan).</summary>
        public void Push(Cart cart)
        {
            if (Main.netMode != NetmodeID.Server || cart == null || !records.TryGetValue(cart.Id, out Record record))
                return;

            Refresh(record);
            SendToSubscribers(record);
        }

        // ---------------------------------------------------------------- server: lifetime

        void OnPlayerLeft(int player)
        {
            if (Main.netMode != NetmodeID.Server || player < 0 || player >= Main.maxPlayers)
                return;

            Unsubscribe(player, "left the game");
        }

        void EndAll(int cartId, CartPanelCloseReason reason)
        {
            if (!records.TryGetValue(cartId, out Record record))
                return;

            foreach (Subscriber subscriber in record.Subscribers)
            {
                playerCart[subscriber.Player] = 0;
                SendClosed(subscriber.Player, cartId, reason);
                CartPanelNet.ServerEnded++;
            }

            FactorrariaNet.Log("[CartPanel] cart " + cartId + " ended for " + record.Subscribers.Count + " subscriber(s): " + reason);
            records.Remove(cartId);
        }

        void Unsubscribe(int player, string why)
        {
            int id = playerCart[player];
            if (id == 0)
                return;

            playerCart[player] = 0;
            if (!records.TryGetValue(id, out Record record))
                return;

            record.Subscribers.RemoveAll(s => s.Player == player);
            CartPanelNet.ServerEnded++;
            FactorrariaNet.Log("[CartPanel] cart " + id + " unsubscribed player " + player + " (" + why + ")");

            if (record.Subscribers.Count == 0)
                records.Remove(id);
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                CartPanelNet.ClientTick();
                CartSlotEdit.Update(AnswerTimeoutTicks);
                return;
            }

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
                // A cart that is gone (picked up, fell out of the world) or no longer the live object: end for everyone.
                Cart live = CartSystem.FindById(record.Id);
                if (live == null || !ReferenceEquals(live, record.Cart) || !CartPanelNet.NeedsDetail(live))
                {
                    EndAll(record.Id, CartPanelCloseReason.Gone);
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
                        SendClosed(player, record.Id, CartPanelCloseReason.Dead);
                    }
                    else if (!InKeepReach(who, live))
                    {
                        Unsubscribe(player, "out of reach");
                        SendClosed(player, record.Id, CartPanelCloseReason.OutOfReach);
                    }
                }

                if (record.Subscribers.Count == 0)
                {
                    records.Remove(record.Id);
                    continue;
                }

                Refresh(record);
                SendToSubscribers(record);
            }
        }

        // ---------------------------------------------------------------- server: sending

        static void Refresh(Record record)
        {
            byte[] payload = CartPanelNet.BuildPayload(record.Cart);
            if (record.Last == null || !payload.AsSpan().SequenceEqual(record.Last))
            {
                record.Last = payload;
                record.Version++;
            }
        }

        static void SendToSubscribers(Record record)
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
            int id = record.Id;
            FactorrariaNet.SendToClient(player, FactorrariaPacket.CartPanelDetail, w =>
            {
                w.Write(id);
                w.Write7BitEncodedInt(payload.Length);
                w.Write(payload);
            });

            CartPanelNet.ServerDetails++;
            CartPanelNet.ServerDetailBytes += payload.Length;
        }

        static void SendClosed(int player, int cartId, CartPanelCloseReason reason)
        {
            FactorrariaNet.SendToClient(player, FactorrariaPacket.CartPanelClosed, w =>
            {
                w.Write(cartId);
                w.Write((byte)reason);
            });
        }

        // Test aid (temporary, used by /fcarts): who has which cart open, on the server.
        public string DescribeSubscriptions()
        {
            if (records.Count == 0)
                return "none";

            var sb = new StringBuilder();
            foreach (Record record in records.Values)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append("cart ").Append(record.Id).Append(" <-");
                foreach (Subscriber subscriber in record.Subscribers)
                    sb.Append(' ').Append(Main.player[subscriber.Player].name);
            }
            return sb.ToString();
        }

        public override void OnWorldLoad()
        {
            ResetState();
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
            Array.Clear(playerCart, 0, playerCart.Length);
            Array.Clear(wasActive, 0, wasActive.Length);
            scratch.Clear();
            CartPanelNet.ResetClient();
            CartSlotEdit.Reset();
        }
    }

    // Packet handlers, the detail payload and the client side of the panel channel (see the system above for the design).
    public static class CartPanelNet
    {
        sealed class DetailPacket
        {
            public int CartId;
            public byte[] Bytes;
        }

        static readonly ConcurrentQueue<DetailPacket> details = new ConcurrentQueue<DetailPacket>();
        static int openCartId;
        static bool ready;
        static volatile int serverClosedId;

        // Temporary test counters (remove in Phase 10; shown by /fcarts).
        public static int ServerOpened;          // server: subscriptions started
        public static int ServerEnded;           // server: subscriptions ended (closed, left, dead, out of reach, cart gone, switched)
        public static int ServerRefusedOpen;     // server: Open requests refused
        public static int ServerDetails;         // server: detail packets
        public static long ServerDetailBytes;
        public static int ClientReceived;        // client: detail packets
        public static int ClientApplied;
        public static int ClientMissed;          // client: detail for a cart the client does not have, or for another module
        public static int ClientFailed;
        public static int ClientClosedByServer;  // client: close notices

        /// <summary>Does this cart's panel need the server's detail (chest contents, fuel slot)? A terrarium or liquid motor shows its replica tank instead.</summary>
        public static bool NeedsDetail(Cart cart)
        {
            return cart != null && (cart.Module == CartModule.Chest || (cart.Module == CartModule.Motor && cart.Tank == null && !cart.AlwaysRuns));
        }

        public static void ResetClient()
        {
            while (details.TryDequeue(out _)) { }
            openCartId = 0;
            ready = false;
            serverClosedId = 0;
            ServerOpened = ServerEnded = ServerRefusedOpen = ServerDetails = 0;
            ServerDetailBytes = 0;
            ClientReceived = ClientApplied = ClientMissed = ClientFailed = ClientClosedByServer = 0;
        }

        // ---------------------------------------------------------------- client -> server

        /// <summary>Multiplayer client: the player opened this cart's panel. The panel's slots ignore clicks until the first detail arrives (IsReady).</summary>
        public static void ClientOpen(Cart cart)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || cart == null || cart.Id <= 0 || !NeedsDetail(cart))
                return;

            openCartId = cart.Id;
            ready = false;
            serverClosedId = 0;

            int id = cart.Id;
            FactorrariaNet.SendRequest(FactorrariaPacket.CartPanelRequest, w =>
            {
                w.Write((byte)CartPanelAction.Open);
                w.Write(id);
            });
        }

        /// <summary>Multiplayer client: the player closed the panel (or it closed itself). Tells the server, which ends the subscription.</summary>
        public static void ClientClose(Cart cart)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || cart == null || cart.Id <= 0)
                return;

            bool wasOpen = openCartId == cart.Id;
            openCartId = 0;
            ready = false;

            if (!wasOpen)
                return;

            int id = cart.Id;
            FactorrariaNet.SendRequest(FactorrariaPacket.CartPanelRequest, w =>
            {
                w.Write((byte)CartPanelAction.Close);
                w.Write(id);
            });
        }

        /// <summary>Client: has the first detail of the open panel of this cart arrived (so a click has a real slot to compare with)?</summary>
        public static bool IsReady(int cartId)
        {
            return openCartId == cartId && ready;
        }

        /// <summary>Client main thread: true once if the server ended the subscription of this cart (the panel closes without telling the server again).</summary>
        public static bool TakeServerClose(int cartId)
        {
            if (serverClosedId == 0 || serverClosedId != cartId)
                return false;

            serverClosedId = 0;
            openCartId = 0;
            ready = false;
            return true;
        }

        // ---------------------------------------------------------------- server: request router

        public static void HandleRequest(BinaryReader reader, int sender)
        {
            byte raw = reader.ReadByte();
            int cartId = reader.ReadInt32();

            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers)
                return;   // single-player and the host never send these: the panel edited the cart directly

            CartPanelSystem system = ModContent.GetInstance<CartPanelSystem>();

            switch ((CartPanelAction)raw)
            {
                case CartPanelAction.Open:
                    system.OnOpenRequest(sender, cartId);
                    break;

                case CartPanelAction.Close:
                    system.OnCloseRequest(sender, cartId);
                    break;

                case CartPanelAction.SlotEdit:
                    CartSlotEdit.ServerSlotEdit(sender, cartId, reader);
                    break;

                case CartPanelAction.Insert:
                    CartSlotEdit.ServerInsert(sender, cartId, reader);
                    break;

                case CartPanelAction.SetFilter:
                    CartSlotEdit.ServerSetFilter(sender, cartId);
                    break;
            }
        }

        // ---------------------------------------------------------------- the detail payload

        /// <summary>
        /// The exact panel state of a cart: the module, then for a chest cart the six slots and the six filters, for a motor cart the fuel
        /// slot and the burn fraction in percent (255 = nothing burning). Compared byte for byte to find out whether anything changed.
        /// </summary>
        public static byte[] BuildPayload(Cart cart)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);

            w.Write((byte)cart.Module);

            if (cart.Module == CartModule.Chest && cart.ChestItems != null)
            {
                for (int i = 0; i < Cart.ChestSlots; i++)
                    CartSlotEdit.WriteSlotItem(w, i < cart.ChestItems.Length ? cart.ChestItems[i] : null);

                for (int i = 0; i < Cart.ChestSlots; i++)
                    w.Write7BitEncodedInt(cart.SlotFilter != null && i < cart.SlotFilter.Length ? cart.SlotFilter[i] : 0);
            }
            else if (cart.Module == CartModule.Motor)
            {
                CartSlotEdit.WriteSlotItem(w, cart.Fuel);

                float burn = cart.FuelBurnFraction;
                w.Write(burn < 0f ? (byte)255 : (byte)Math.Clamp((int)Math.Round(burn * 100f), 0, 100));
            }

            w.Flush();
            return stream.ToArray();
        }

        // Client main thread. Writes the exact state into the live replica in place.
        static bool ApplyPayload(Cart cart, BinaryReader r)
        {
            CartModule module = (CartModule)r.ReadByte();
            if (module != cart.Module)
                return false;

            if (module == CartModule.Chest)
            {
                if (cart.ChestItems == null || cart.SlotFilter == null)
                    return false;

                for (int i = 0; i < Cart.ChestSlots; i++)
                {
                    Item item = CartSlotEdit.ReadSlotItem(r);
                    if (i < cart.ChestItems.Length)
                        cart.ChestItems[i] = item;
                }

                for (int i = 0; i < Cart.ChestSlots; i++)
                {
                    int filter = r.Read7BitEncodedInt();
                    if (i < cart.SlotFilter.Length)
                        cart.SlotFilter[i] = filter;
                }

                return true;
            }

            if (module == CartModule.Motor && cart.Tank == null)
            {
                Item fuel = CartSlotEdit.ReadSlotItem(r);
                byte burn = r.ReadByte();
                cart.Fuel = fuel;
                cart.NetBurnFraction = burn == 255 ? -1f : burn / 100f;
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- server -> client

        // Network thread: it only queues. ClientTick (main thread) applies it.
        public static void HandleDetail(BinaryReader reader, int sender)
        {
            int cartId = reader.ReadInt32();
            int length = reader.Read7BitEncodedInt();
            byte[] bytes = length > 0 ? reader.ReadBytes(length) : Array.Empty<byte>();

            ClientReceived++;
            details.Enqueue(new DetailPacket { CartId = cartId, Bytes = bytes });
        }

        // Network thread. The panel itself is closed from the main thread (CartChestUISystem.UpdateUI asks TakeServerClose).
        public static void HandleClosed(BinaryReader reader, int sender)
        {
            int cartId = reader.ReadInt32();
            reader.ReadByte();

            ClientClosedByServer++;
            serverClosedId = cartId;
        }

        /// <summary>Client main thread, every tick: applies the queued details to the replicas.</summary>
        public static void ClientTick()
        {
            while (details.TryDequeue(out DetailPacket packet))
            {
                Cart cart = CartSystem.FindById(packet.CartId);
                if (cart == null)
                {
                    ClientMissed++;
                    continue;
                }

                try
                {
                    using var stream = new MemoryStream(packet.Bytes);
                    using var reader = new BinaryReader(stream);

                    if (ApplyPayload(cart, reader))
                    {
                        CartSlotEdit.ReapplyPending(cart);   // a slot edit still waiting for its answer stays visible
                        if (packet.CartId == openCartId)
                            ready = true;
                        ClientApplied++;
                    }
                    else
                    {
                        ClientMissed++;
                    }
                }
                catch (Exception e)
                {
                    ClientFailed++;
                    FactorrariaNet.Log("[CartPanel] detail for cart " + packet.CartId + " could not be read: " + e.Message);
                }
            }
        }

        // Test aid (temporary, printed by /fcarts).
        public static string Describe()
        {
            if (Main.netMode == NetmodeID.Server)
            {
                return "cartPanel: server subscriptions started " + ServerOpened + ", ended " + ServerEnded + ", refused " + ServerRefusedOpen
                    + ", detail packets " + ServerDetails + " (" + ServerDetailBytes + " B); slot edits accepted " + CartSlotEdit.ServerAccepted
                    + ", rejected " + CartSlotEdit.ServerRejected + "; inserts " + CartSlotEdit.ServerInserts + " (" + CartSlotEdit.ServerInsertedItems
                    + " items); filters set " + CartSlotEdit.ServerFilters + "; now: " + ModContent.GetInstance<CartPanelSystem>().DescribeSubscriptions();
            }

            return "cartPanel: client details " + ClientReceived + " (applied " + ClientApplied + ", missed " + ClientMissed + ", failed " + ClientFailed
                + "), closed by the server " + ClientClosedByServer + "; slot edits sent " + CartSlotEdit.ClientSent + " (confirmed " + CartSlotEdit.ClientConfirmed
                + ", rolled back " + CartSlotEdit.ClientRolledBack + "); inserts sent " + CartSlotEdit.ClientInsertsSent + " (accepted " + CartSlotEdit.ClientInsertsAccepted
                + ", refused " + CartSlotEdit.ClientInsertsRefused + ", not found in inventory " + CartSlotEdit.ClientInsertUnmatched + ")";
        }
    }
}