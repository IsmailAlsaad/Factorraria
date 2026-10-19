using Factorraria.Content.VirtualItems;
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
    public class VItemNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.VItemSubscribeRequest, PacketDirection.ToServer, VItemNet.HandleSubscribe);
            FactorrariaNet.Register(FactorrariaPacket.VItemDelta, PacketDirection.ToClient, VItemNet.HandleDelta);
            JoinSync.RegisterDisconnectHook(VItemNet.OnDisconnect);

            if (!Main.dedServ)
            {
                JoinSync.JoinCompleted += VItemNet.Subscribe;
            }
        }

        public override void OnWorldLoad() => VItemNet.Reset();

        public override void OnWorldUnload() => VItemNet.Reset();
    }

    // vItem replication (roadmap patch 7.2, finding "VirtualItemSystem" and the vItem part of step 1).
    //
    // Authority: the server (or single-player) simulates every vItem (patch 7.1). A multiplayer client keeps only display copies
    // (replicas, VirtualItem.CreateReplica) in VirtualItemSystem.virtualItems, so the existing draw and hover code works unchanged.
    //
    // Flow: when a client finishes its join sync it sends VItemSubscribeRequest. From then on the server, once per tick after the
    // simulation, compares the vItems near that player with what it last told that player (a per-client "known" table) and sends one
    // VItemDelta with: spawns (id, the whole item through the shared item writer, position, target tile, step), updates (a vItem that
    // changed target tile, stack, type or speed), and removals (ids that were removed or left the area). The first delta after a
    // subscribe carries a reset flag so the client drops any old replicas. A replica then moves toward its target tile by the sent
    // step every tick, the same way the server moves the real vItem, so a steady belt needs no packets between tile changes.
    //
    // Interest: a rectangle around the player (NetworkConfig VItemInterestHalfWidthTiles / HalfHeightTiles). A vItem is sent when it is
    // inside, and removed from that player's view only when it is 12 tiles outside, so items on the edge do not flicker. At most
    // VItemMaxEntriesPerPacket spawns and updates (and twice that many removals) per player per tick; a bigger backlog is spread over ticks.
    //
    // Threads: packet handlers run off the main thread, so they only parse into a Batch and queue it; ClientTick applies it on the main thread.
    // Single-player sends nothing.
    public static class VItemNet
    {
        const int OuterMargin = 12;

        sealed class Known
        {
            public int Type;
            public int Stack;
            public int TileX;
            public int TileY;
            public float Step;
        }

        sealed class ClientView
        {
            public Dictionary<int, Known> Known = new();
            public bool ResetPending = true;
        }

        sealed class SpawnEntry
        {
            public int Id;
            public Item Payload;
            public Vector2 Position;
            public int TileX;
            public int TileY;
            public float Step;
        }

        sealed class UpdateEntry
        {
            public int Id;
            public int Stack;
            public Item Payload;   // null when only the stack changed
            public Vector2 Position;
            public int TileX;
            public int TileY;
            public float Step;
        }

        sealed class Batch
        {
            public bool Reset;
            public List<SpawnEntry> Spawns = new();
            public List<UpdateEntry> Updates = new();
            public List<int> Removes = new();
        }

        static readonly ConcurrentDictionary<int, ClientView> views = new();
        static readonly ConcurrentQueue<int> subscribeQueue = new();
        static readonly ConcurrentQueue<Batch> incoming = new();

        // Temporary test counters (remove in Phase 10).
        public static int ServerPackets;
        public static int ServerSpawns;
        public static int ServerUpdates;
        public static int ServerRemoves;
        public static int ClientPackets;
        public static int ClientSpawns;
        public static int ClientUpdates;
        public static int ClientRemoves;
        public static int ClientMissed;

        static int HalfWidth => global::Factorraria.Content.Configs.NetworkConfig.Current.VItemInterestHalfWidthTiles;
        static int HalfHeight => global::Factorraria.Content.Configs.NetworkConfig.Current.VItemInterestHalfHeightTiles;
        static int MaxEntries => global::Factorraria.Content.Configs.NetworkConfig.Current.VItemMaxEntriesPerPacket;

        public static void Reset()
        {
            views.Clear();
            while (subscribeQueue.TryDequeue(out _)) { }
            while (incoming.TryDequeue(out _)) { }
            ServerPackets = ServerSpawns = ServerUpdates = ServerRemoves = 0;
            ClientPackets = ClientSpawns = ClientUpdates = ClientRemoves = ClientMissed = 0;
        }

        public static string Describe()
        {
            return "vItemNet: views=" + views.Count + " sent=" + ServerPackets + " (spawn " + ServerSpawns + ", update " + ServerUpdates + ", remove " + ServerRemoves
                + ") recv=" + ClientPackets + " (spawn " + ClientSpawns + ", update " + ClientUpdates + ", remove " + ClientRemoves + ", missed " + ClientMissed + ")";
        }

        // ---------------------------------------------------------------- client: subscribe

        /// <summary>Client: called when the join sync finished. Asks the server to start sending the vItems near this player.</summary>
        public static void Subscribe()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            FactorrariaNet.SendRequest(FactorrariaPacket.VItemSubscribeRequest);
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

        /// <summary>Server (called at the end of VirtualItemSystem.PostUpdateWorld): sends every subscribed player what changed near them.</summary>
        public static void ServerTick()
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            while (subscribeQueue.TryDequeue(out int who))
            {
                if (!Main.player[who].active)
                    continue;

                views[who] = new ClientView();
                FactorrariaNet.Log("[VItem] " + Main.player[who].name + " subscribed to vItems");
            }

            if (views.IsEmpty)
                return;

            foreach (var pair in views)
            {
                Player player = Main.player[pair.Key];
                if (!player.active)
                {
                    views.TryRemove(pair.Key, out _);
                    continue;
                }

                SendDelta(pair.Key, player, pair.Value);
            }
        }

        static void SendDelta(int who, Player player, ClientView view)
        {
            int cap = MaxEntries;
            int halfW = HalfWidth;
            int halfH = HalfHeight;
            int centerX = (int)(player.Center.X / 16f);
            int centerY = (int)(player.Center.Y / 16f);

            bool reset = view.ResetPending;
            if (reset)
                view.Known.Clear();

            List<int> removes = new();
            foreach (var pair in view.Known)
            {
                if (!VirtualItemSystem.TryGetById(pair.Key, out VirtualItem live) || !IsInside(live, centerX, centerY, halfW + OuterMargin, halfH + OuterMargin))
                {
                    removes.Add(pair.Key);
                    if (removes.Count >= cap * 2)
                        break;
                }
            }

            foreach (int id in removes)
                view.Known.Remove(id);

            List<VirtualItem> spawns = new();
            List<VirtualItem> updates = new();
            List<bool> updatesFull = new();

            List<VirtualItem> all = VirtualItemSystem.virtualItems;
            for (int i = 0; i < all.Count; i++)
            {
                VirtualItem item = all[i];
                if (!item.active || item.Id == 0 || item.Payload == null)
                    continue;

                if (view.Known.TryGetValue(item.Id, out Known known))
                {
                    bool typeChanged = known.Type != item.itemType;
                    bool changed = typeChanged || known.Stack != item.stackSize || known.TileX != item.targetTileX || known.TileY != item.targetTileY
                        || Math.Abs(known.Step - item.StepPerTick) > 0.25f;

                    if (changed && updates.Count < cap)
                    {
                        updates.Add(item);
                        updatesFull.Add(typeChanged);
                        Remember(known, item);
                    }
                }
                else if (spawns.Count < cap && IsInside(item, centerX, centerY, halfW, halfH))
                {
                    spawns.Add(item);
                    Known fresh = new Known();
                    Remember(fresh, item);
                    view.Known[item.Id] = fresh;
                }
            }

            if (!reset && spawns.Count == 0 && updates.Count == 0 && removes.Count == 0)
                return;

            view.ResetPending = false;

            FactorrariaNet.SendToClient(who, FactorrariaPacket.VItemDelta, writer =>
            {
                writer.Write(reset);

                writer.Write((ushort)spawns.Count);
                foreach (VirtualItem item in spawns)
                {
                    writer.Write(item.Id);
                    FactorrariaNet.WriteItem(writer, item.Payload);
                    writer.Write(item.worldPosition.X);
                    writer.Write(item.worldPosition.Y);
                    writer.Write((short)item.targetTileX);
                    writer.Write((short)item.targetTileY);
                    writer.Write(item.StepPerTick);
                }

                writer.Write((ushort)updates.Count);
                for (int i = 0; i < updates.Count; i++)
                {
                    VirtualItem item = updates[i];
                    writer.Write(item.Id);
                    writer.Write(item.stackSize);
                    writer.Write(item.worldPosition.X);
                    writer.Write(item.worldPosition.Y);
                    writer.Write((short)item.targetTileX);
                    writer.Write((short)item.targetTileY);
                    writer.Write(item.StepPerTick);
                    writer.Write(updatesFull[i]);
                    if (updatesFull[i])
                        FactorrariaNet.WriteItem(writer, item.Payload);
                }

                writer.Write((ushort)removes.Count);
                foreach (int id in removes)
                    writer.Write(id);
            });

            ServerPackets++;
            ServerSpawns += spawns.Count;
            ServerUpdates += updates.Count;
            ServerRemoves += removes.Count;
        }

        static void Remember(Known known, VirtualItem item)
        {
            known.Type = item.itemType;
            known.Stack = item.stackSize;
            known.TileX = item.targetTileX;
            known.TileY = item.targetTileY;
            known.Step = item.StepPerTick;
        }

        static bool IsInside(VirtualItem item, int centerX, int centerY, int halfW, int halfH)
        {
            int tileX = (int)(item.worldPosition.X / 16f);
            int tileY = (int)(item.worldPosition.Y / 16f);
            return Math.Abs(tileX - centerX) <= halfW && Math.Abs(tileY - centerY) <= halfH;
        }

        // ---------------------------------------------------------------- client

        public static void HandleDelta(BinaryReader reader, int sender)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            Batch batch = new Batch();
            batch.Reset = reader.ReadBoolean();

            int spawnCount = reader.ReadUInt16();
            for (int i = 0; i < spawnCount; i++)
            {
                SpawnEntry entry = new SpawnEntry();
                entry.Id = reader.ReadInt32();
                entry.Payload = FactorrariaNet.ReadItem(reader);
                entry.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                entry.TileX = reader.ReadInt16();
                entry.TileY = reader.ReadInt16();
                entry.Step = reader.ReadSingle();
                batch.Spawns.Add(entry);
            }

            int updateCount = reader.ReadUInt16();
            for (int i = 0; i < updateCount; i++)
            {
                UpdateEntry entry = new UpdateEntry();
                entry.Id = reader.ReadInt32();
                entry.Stack = reader.ReadInt32();
                entry.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                entry.TileX = reader.ReadInt16();
                entry.TileY = reader.ReadInt16();
                entry.Step = reader.ReadSingle();
                if (reader.ReadBoolean())
                    entry.Payload = FactorrariaNet.ReadItem(reader);
                batch.Updates.Add(entry);
            }

            int removeCount = reader.ReadUInt16();
            for (int i = 0; i < removeCount; i++)
                batch.Removes.Add(reader.ReadInt32());

            incoming.Enqueue(batch);
            ClientPackets++;
        }

        /// <summary>Client, main thread (called from VirtualItemSystem.PostUpdateWorld): applies the queued deltas, then moves every replica.</summary>
        public static void ClientTick()
        {
            while (incoming.TryDequeue(out Batch batch))
                Apply(batch);

            List<VirtualItem> list = VirtualItemSystem.virtualItems;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                VirtualItem replica = list[i];
                if (!replica.active)
                {
                    list.RemoveAt(i);
                    continue;
                }

                replica.ReplicaTick();
            }
        }

        static void Apply(Batch batch)
        {
            if (batch.Reset)
            {
                foreach (VirtualItem old in VirtualItemSystem.virtualItems)
                    old.Remove();

                VirtualItemSystem.virtualItems.Clear();
                VirtualItemSystem.vItemsById.Clear();
            }

            foreach (SpawnEntry spawn in batch.Spawns)
            {
                if (spawn.Payload == null || spawn.Payload.IsAir || !VirtualItemSystem.IsValidItemID(spawn.Payload.type))
                    continue;

                if (VirtualItemSystem.vItemsById.TryGetValue(spawn.Id, out VirtualItem existing))
                    existing.Remove();

                VirtualItemSystem.AddReplica(VirtualItem.CreateReplica(spawn.Id, spawn.Payload, spawn.Position, spawn.TileX, spawn.TileY, spawn.Step));
                ClientSpawns++;
            }

            foreach (UpdateEntry update in batch.Updates)
            {
                if (!VirtualItemSystem.TryGetById(update.Id, out VirtualItem replica))
                {
                    ClientMissed++;
                    continue;
                }

                replica.ApplyReplicaState(update.Payload, update.Stack, update.Position, update.TileX, update.TileY, update.Step);
                ClientUpdates++;
            }

            foreach (int id in batch.Removes)
            {
                if (!VirtualItemSystem.TryGetById(id, out VirtualItem replica))
                {
                    ClientMissed++;
                    continue;
                }

                replica.Remove();
                ClientRemoves++;
            }
        }
    }
}