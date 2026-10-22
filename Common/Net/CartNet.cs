using Factorraria.Common.Carts;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    public class CartNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.CartSubscribeRequest, PacketDirection.ToServer, CartNet.HandleSubscribe);
            FactorrariaNet.Register(FactorrariaPacket.CartDelta, PacketDirection.ToClient, CartNet.HandleDelta);
            FactorrariaNet.Register(FactorrariaPacket.CartMove, PacketDirection.ToClient, CartNet.HandleMove);
            JoinSync.RegisterDisconnectHook(CartNet.OnDisconnect);

            if (!Main.dedServ)
            {
                JoinSync.JoinCompleted += CartNet.Subscribe;
            }
        }

        public override void OnWorldLoad() => CartNet.Reset();

        public override void OnWorldUnload() => CartNet.Reset();

        // PostUpdateWorld does not run on a multiplayer client (patch 7.3b); PostUpdateEverything runs on every side.
        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            CartNet.ClientTick();
        }
    }

    // Cart replication (roadmap patch 9.2, finding C1, fixes R1 of the 7.4 test).
    //
    // Authority: the server (or single-player) simulates every cart (patch 9.1). A multiplayer client keeps replicas (Cart objects with
    // Id > 0 that never simulate) in CartSystem.Carts, so the existing drawing code works unchanged, and finds them with CartSystem.FindById.
    //
    // Flow: when a client finishes its join sync it sends CartSubscribeRequest. This replaces a separate join provider: the first
    // CartDelta after a subscribe carries a reset flag and every cart in range, so a late joiner (or a /fjoin) gets the full picture the
    // same way a player walking toward carts does. From then on, once per tick after the simulation, the server compares the carts
    // with what it last told that player (a per-client "known" table):
    //  - CartDelta (on change): a cart that entered the radius, or whose module, module item, tank (type, or amount by 2 % of capacity),
    //    burning flag or chain place (motor id and slot) changed, is sent with its whole state; a cart that was removed or left the radius
    //    (plus a margin, so edge carts do not flicker) is sent as a removal.
    //  - CartMove (every 60 / CartUpdatesPerSecond ticks): the position, velocity, rotation, facing and on-track flag of every known cart
    //    whose motion changed since it was last sent (a cart at rest sends nothing). The client feeds Cart.ApplyNetState and
    //    Cart.ClientStep eases the drawn cart (9.1).
    // Skin and module item go on the wire as key strings (CartSkins.KeyOf), not item types. Chain membership is the motor's cart id plus
    // the slot, never a list index; the client rebuilds replica chains after every delta so the link drawing works.
    // Not in 9.2 (by design): chest contents and fuel slots (9.4 and 9.5), bump / pour events (9.6), any input (9.3).
    //
    // Interest: CartInterestRadiusTiles around the player (config), removal at 12 tiles beyond that. At most MaxEntries states and
    // MaxEntries moves per player per send; a bigger backlog is spread over the following ticks.
    //
    // Threads: packet handlers run off the main thread, so they only parse and queue; ClientTick applies the queue in arrival order on the
    // main thread. Single-player sends nothing.
    public static class CartNet
    {
        sealed class Known
        {
            public string Skin;
            public CartModule Module;
            public int ModuleItem;
            public int TankType;
            public float TankAmount;
            public bool HasTank;
            public bool Burning;
            public int MotorId;
            public int Slot;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public int Facing;
            public bool OnTrack;
        }

        sealed class ClientView
        {
            public Dictionary<int, Known> Known = new();
            public bool ResetPending = true;
        }

        sealed class StateEntry
        {
            public int Id;
            public string Skin;
            public CartModule Module;
            public string ModuleItemKey;
            public bool HasTank;
            public int TankType;
            public float TankAmount;
            public bool Burning;
            public int MotorId;
            public int Slot;
            public MoveEntry Motion;
        }

        sealed class MoveEntry
        {
            public int Id;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public int Facing;
            public bool OnTrack;
        }

        sealed class DeltaBatch
        {
            public bool Reset;
            public List<StateEntry> States = new();
            public List<int> Removes = new();
        }

        sealed class MoveBatch
        {
            public List<MoveEntry> Moves = new();
        }

        static readonly ConcurrentDictionary<int, ClientView> views = new();
        static readonly ConcurrentQueue<int> subscribeQueue = new();
        static readonly ConcurrentQueue<object> incoming = new(); // DeltaBatch and MoveBatch, in arrival order
        static readonly Dictionary<int, Cart> live = new();
        static int tick;

        // Temporary test counters (remove in Phase 10).
        public static int ServerDeltas;
        public static int ServerStates;
        public static int ServerRemoves;
        public static int ServerMovePackets;
        public static int ServerMoves;
        public static int ClientDeltas;
        public static int ClientCreated;
        public static int ClientUpdated;
        public static int ClientRemoves;
        public static int ClientMovePackets;
        public static int ClientMoves;
        public static int ClientMissed;

        static float RadiusPixels => global::Factorraria.Content.Configs.NetworkConfig.Current.CartInterestRadiusTiles * 16f;
        static int MoveIntervalTicks => Math.Max(1, (int)Math.Round(60.0 / Math.Max(1, global::Factorraria.Content.Configs.NetworkConfig.Current.CartUpdatesPerSecond)));
        static int MaxEntries => 60;
        static float OuterMarginPixels => 12f * 16f;

        public static void Reset()
        {
            views.Clear();
            while (subscribeQueue.TryDequeue(out _)) { }
            while (incoming.TryDequeue(out _)) { }
            live.Clear();
            tick = 0;
            ServerDeltas = ServerStates = ServerRemoves = ServerMovePackets = ServerMoves = 0;
            ClientDeltas = ClientCreated = ClientUpdated = ClientRemoves = ClientMovePackets = ClientMoves = ClientMissed = 0;
        }

        public static string Describe()
        {
            return "cartNet: views=" + views.Count + " server sent deltas=" + ServerDeltas + " (states " + ServerStates + ", removes " + ServerRemoves + ") moves="
                + ServerMovePackets + " (entries " + ServerMoves + "); client got deltas=" + ClientDeltas + " (created " + ClientCreated + ", updated " + ClientUpdated
                + ", removed " + ClientRemoves + ") moves=" + ClientMovePackets + " (entries " + ClientMoves + ", missed " + ClientMissed + ")";
        }

        // ---------------------------------------------------------------- client: subscribe

        /// <summary>Client: called when the join sync finished. Asks the server to start sending the carts near this player.</summary>
        public static void Subscribe()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            FactorrariaNet.SendRequest(FactorrariaPacket.CartSubscribeRequest);
        }

        // ---------------------------------------------------------------- server

        public static void HandleSubscribe(BinaryReader reader, int sender)
        {
            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers)
                return;

            subscribeQueue.Enqueue(sender);
        }

        public static void OnDisconnect(int who)
        {
            views.TryRemove(who, out _);
        }

        /// <summary>Server (called from CartSystem.PostUpdateWorld after the simulation): sends every subscribed player what changed near them.</summary>
        public static void ServerTick()
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            while (subscribeQueue.TryDequeue(out int who))
            {
                if (!Main.player[who].active)
                    continue;

                views[who] = new ClientView();
                FactorrariaNet.Log("[Carts] " + Main.player[who].name + " subscribed to carts");
            }

            if (views.IsEmpty)
                return;

            tick++;

            live.Clear();
            for (int i = 0; i < CartSystem.Carts.Count; i++)
            {
                Cart cart = CartSystem.Carts[i];
                if (cart.Active && cart.Id > 0)
                    live[cart.Id] = cart;
            }

            bool moveTick = tick % MoveIntervalTicks == 0;

            foreach (var pair in views)
            {
                Player player = Main.player[pair.Key];
                if (!player.active)
                {
                    views.TryRemove(pair.Key, out _);
                    continue;
                }

                SendDelta(pair.Key, player, pair.Value);

                if (moveTick)
                    SendMoves(pair.Key, pair.Value);
            }
        }

        static void SendDelta(int who, Player player, ClientView view)
        {
            int cap = MaxEntries;
            float radius = RadiusPixels;
            float radiusSquared = radius * radius;
            float outer = radius + OuterMarginPixels;
            float outerSquared = outer * outer;

            bool reset = view.ResetPending;
            if (reset)
                view.Known.Clear();

            List<int> removes = new();
            foreach (var pair in view.Known)
            {
                if (!live.TryGetValue(pair.Key, out Cart cart) || Vector2.DistanceSquared(player.Center, cart.Position) > outerSquared)
                    removes.Add(pair.Key);
            }

            foreach (int id in removes)
                view.Known.Remove(id);

            List<Cart> states = new();
            foreach (Cart cart in live.Values)
            {
                if (states.Count >= cap)
                    break;

                if (view.Known.TryGetValue(cart.Id, out Known known))
                {
                    if (StateChanged(known, cart))
                    {
                        states.Add(cart);
                        RememberState(known, cart);
                        RememberMotion(known, cart);
                    }
                }
                else if (Vector2.DistanceSquared(player.Center, cart.Position) <= radiusSquared)
                {
                    states.Add(cart);
                    Known fresh = new Known();
                    RememberState(fresh, cart);
                    RememberMotion(fresh, cart);
                    view.Known[cart.Id] = fresh;
                }
            }

            if (!reset && states.Count == 0 && removes.Count == 0)
                return;

            view.ResetPending = false;

            FactorrariaNet.SendToClient(who, FactorrariaPacket.CartDelta, writer =>
            {
                writer.Write(reset);

                writer.Write((ushort)states.Count);
                foreach (Cart cart in states)
                    WriteState(writer, cart);

                writer.Write((ushort)removes.Count);
                foreach (int id in removes)
                    writer.Write(id);
            });

            ServerDeltas++;
            ServerStates += states.Count;
            ServerRemoves += removes.Count;
        }

        static void SendMoves(int who, ClientView view)
        {
            int cap = MaxEntries;
            List<Cart> moved = new();

            foreach (var pair in view.Known)
            {
                if (!live.TryGetValue(pair.Key, out Cart cart))
                    continue;

                if (MotionChanged(pair.Value, cart))
                {
                    moved.Add(cart);
                    RememberMotion(pair.Value, cart);

                    if (moved.Count >= cap)
                        break;
                }
            }

            if (moved.Count == 0)
                return;

            FactorrariaNet.SendToClient(who, FactorrariaPacket.CartMove, writer =>
            {
                writer.Write((ushort)moved.Count);
                foreach (Cart cart in moved)
                    WriteMotion(writer, cart);
            });

            ServerMovePackets++;
            ServerMoves += moved.Count;
        }

        // ---------------------------------------------------------------- what is on the wire

        static Vector2 VelocityOf(Cart cart)
        {
            return cart.OnTrack ? new Vector2(cart.Speed, 0f) : cart.Velocity;
        }

        static int MotorIdOf(Cart cart)
        {
            return cart.Chain != null && cart.Chain.Motor != null ? cart.Chain.Motor.Id : 0;
        }

        static int SlotOf(Cart cart)
        {
            if (cart.Chain == null)
                return 0;

            return cart.Chain.Motor == cart ? 0 : cart.Chain.Carriages.IndexOf(cart) + 1;
        }

        static void RememberState(Known known, Cart cart)
        {
            known.Skin = cart.Skin;
            known.Module = cart.Module;
            known.ModuleItem = cart.ModuleItem > 0 ? cart.ModuleItem : -1;
            known.HasTank = cart.Tank != null;
            known.TankType = cart.Tank != null ? cart.Tank.LiquidType : -1;
            known.TankAmount = cart.Tank != null ? cart.Tank.Amount : 0f;
            known.Burning = cart.IsBurning;
            known.MotorId = MotorIdOf(cart);
            known.Slot = SlotOf(cart);
        }

        static void RememberMotion(Known known, Cart cart)
        {
            known.Position = cart.Position;
            known.Velocity = VelocityOf(cart);
            known.Rotation = cart.Rotation;
            known.Facing = cart.Facing;
            known.OnTrack = cart.OnTrack;
        }

        static bool StateChanged(Known known, Cart cart)
        {
            int moduleItem = cart.ModuleItem > 0 ? cart.ModuleItem : -1;
            if (known.Module != cart.Module || known.ModuleItem != moduleItem || known.Burning != cart.IsBurning)
                return true;

            if (known.MotorId != MotorIdOf(cart) || known.Slot != SlotOf(cart))
                return true;

            bool hasTank = cart.Tank != null;
            if (known.HasTank != hasTank)
                return true;

            if (hasTank)
            {
                if (known.TankType != cart.Tank.LiquidType)
                    return true;

                bool wasEmpty = known.TankAmount <= 0f;
                bool isEmpty = cart.Tank.Amount <= 0f;
                if (wasEmpty != isEmpty)
                    return true;

                float step = Math.Max(1f, cart.Tank.Capacity * 0.02f);
                if (Math.Abs(known.TankAmount - cart.Tank.Amount) >= step)
                    return true;
            }

            return false;
        }

        static bool MotionChanged(Known known, Cart cart)
        {
            if (known.Facing != cart.Facing || known.OnTrack != cart.OnTrack)
                return true;

            if (Vector2.DistanceSquared(known.Position, cart.Position) > 0.01f)
                return true;

            if (Vector2.DistanceSquared(known.Velocity, VelocityOf(cart)) > 0.0001f)
                return true;

            return Math.Abs(known.Rotation - cart.Rotation) > 0.002f;
        }

        static void WriteMotion(BinaryWriter writer, Cart cart)
        {
            Vector2 velocity = VelocityOf(cart);

            writer.Write(cart.Id);
            writer.Write(cart.Position.X);
            writer.Write(cart.Position.Y);
            writer.Write(velocity.X);
            writer.Write(velocity.Y);
            writer.Write(cart.Rotation);
            writer.Write((sbyte)(cart.Facing < 0 ? -1 : 1));
            writer.Write(cart.OnTrack);
        }

        static MoveEntry ReadMotion(BinaryReader reader)
        {
            MoveEntry entry = new MoveEntry();
            entry.Id = reader.ReadInt32();
            entry.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            entry.Velocity = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            entry.Rotation = reader.ReadSingle();
            entry.Facing = reader.ReadSByte();
            entry.OnTrack = reader.ReadBoolean();
            return entry;
        }

        // A state is the identity and look of a cart plus its motion at that moment (motion first, so a new replica appears in place).
        static void WriteState(BinaryWriter writer, Cart cart)
        {
            WriteMotion(writer, cart);

            writer.Write(cart.Skin ?? "");
            writer.Write((byte)cart.Module);
            writer.Write(cart.ModuleItem > 0 ? CartSkins.KeyOf(cart.ModuleItem) : "");

            bool hasTank = cart.Tank != null;
            writer.Write(hasTank);
            if (hasTank)
            {
                writer.Write(cart.Tank.LiquidType);
                writer.Write(cart.Tank.Amount);
            }

            writer.Write(cart.IsBurning);
            writer.Write(MotorIdOf(cart));
            writer.Write((byte)Math.Min(255, SlotOf(cart)));
        }

        static StateEntry ReadState(BinaryReader reader)
        {
            StateEntry entry = new StateEntry();
            entry.Motion = ReadMotion(reader);
            entry.Id = entry.Motion.Id;
            entry.Skin = reader.ReadString();
            entry.Module = (CartModule)reader.ReadByte();
            entry.ModuleItemKey = reader.ReadString();
            entry.HasTank = reader.ReadBoolean();
            if (entry.HasTank)
            {
                entry.TankType = reader.ReadInt32();
                entry.TankAmount = reader.ReadSingle();
            }

            entry.Burning = reader.ReadBoolean();
            entry.MotorId = reader.ReadInt32();
            entry.Slot = reader.ReadByte();
            return entry;
        }

        // ---------------------------------------------------------------- client

        public static void HandleDelta(BinaryReader reader, int sender)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            DeltaBatch batch = new DeltaBatch();
            batch.Reset = reader.ReadBoolean();

            int stateCount = reader.ReadUInt16();
            for (int i = 0; i < stateCount; i++)
                batch.States.Add(ReadState(reader));

            int removeCount = reader.ReadUInt16();
            for (int i = 0; i < removeCount; i++)
                batch.Removes.Add(reader.ReadInt32());

            incoming.Enqueue(batch);
            ClientDeltas++;
        }

        public static void HandleMove(BinaryReader reader, int sender)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            MoveBatch batch = new MoveBatch();
            int count = reader.ReadUInt16();
            for (int i = 0; i < count; i++)
                batch.Moves.Add(ReadMotion(reader));

            incoming.Enqueue(batch);
            ClientMovePackets++;
        }

        /// <summary>Client, main thread (called from CartNetSystem.PostUpdateEverything): applies the queued packets in arrival order, then eases every replica.</summary>
        public static void ClientTick()
        {
            while (incoming.TryDequeue(out object item))
            {
                if (item is DeltaBatch delta)
                    ApplyDelta(delta);
                else if (item is MoveBatch moves)
                    ApplyMoves(moves);
            }

            CartSystem.ClientInterpolate();
        }

        static void ApplyDelta(DeltaBatch batch)
        {
            if (batch.Reset)
            {
                foreach (Cart old in CartSystem.Carts)
                {
                    old.Active = false;
                    CartChestUISystem.NotifyCartRemoved(old);
                }

                CartSystem.Carts.Clear();
            }

            foreach (StateEntry entry in batch.States)
            {
                if (entry.Id <= 0)
                    continue;

                int moduleItem = -1;
                if (!string.IsNullOrEmpty(entry.ModuleItemKey) && CartSkins.TryTypeOf(entry.ModuleItemKey, out int moduleItemType) && moduleItemType > 0)
                    moduleItem = moduleItemType;

                Cart cart = CartSystem.FindById(entry.Id);
                bool created = cart == null;

                if (created)
                {
                    cart = new Cart();
                    cart.Id = entry.Id;
                    cart.Skin = entry.Skin;
                    cart.SetModule(entry.Module, moduleItem);
                    CartSystem.Carts.Add(cart);
                    ClientCreated++;
                }
                else
                {
                    int current = cart.ModuleItem > 0 ? cart.ModuleItem : -1;
                    if (cart.Module != entry.Module || current != moduleItem)
                        cart.SetModule(entry.Module, moduleItem);

                    ClientUpdated++;
                }

                if (cart.Tank != null && entry.HasTank)
                {
                    cart.Tank.LiquidType = entry.TankType;
                    cart.Tank.Amount = entry.TankAmount;
                }

                cart.NetBurning = entry.Burning;
                cart.NetMotorId = entry.MotorId;
                cart.NetSlot = entry.Slot;
                cart.ApplyNetState(entry.Motion.Position, entry.Motion.Velocity, entry.Motion.Rotation, entry.Motion.Facing, entry.Motion.OnTrack);
            }

            foreach (int id in batch.Removes)
            {
                Cart cart = CartSystem.FindById(id);
                if (cart == null)
                {
                    ClientMissed++;
                    continue;
                }

                cart.Active = false;
                CartChestUISystem.NotifyCartRemoved(cart);
                CartSystem.Carts.Remove(cart);
                ClientRemoves++;
            }

            RebuildChains();
        }

        static void ApplyMoves(MoveBatch batch)
        {
            foreach (MoveEntry move in batch.Moves)
            {
                Cart cart = CartSystem.FindById(move.Id);
                if (cart == null)
                {
                    ClientMissed++;
                    continue;
                }

                cart.ApplyNetState(move.Position, move.Velocity, move.Rotation, move.Facing, move.OnTrack);
                ClientMoves++;
            }
        }

        // Rebuilds every replica chain from the motor ids and slots the server sent, so CartSystem's link drawing (which reads
        // Chain.Motor and Chain.Carriages) works unchanged. A carriage whose motor is not known here (out of range) draws as a free cart.
        static void RebuildChains()
        {
            Dictionary<int, List<Cart>> groups = new();

            foreach (Cart cart in CartSystem.Carts)
            {
                cart.Chain = null;

                if (cart.Id > 0 && cart.NetMotorId > 0 && cart.NetSlot > 0)
                {
                    if (!groups.TryGetValue(cart.NetMotorId, out List<Cart> members))
                    {
                        members = new List<Cart>();
                        groups[cart.NetMotorId] = members;
                    }

                    members.Add(cart);
                }
            }

            foreach (var pair in groups)
            {
                Cart motor = CartSystem.FindById(pair.Key);
                if (motor == null)
                    continue;

                List<Cart> carriages = pair.Value;
                carriages.Sort((a, b) => a.NetSlot.CompareTo(b.NetSlot));

                CartChain chain = CartChain.CreateReplica(motor);
                chain.SetReplicaCarriages(carriages);
            }
        }
    }
}