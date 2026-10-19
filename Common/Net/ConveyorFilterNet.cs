using Factorraria.Common.Networks;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    public class ConveyorFilterNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.ConveyorFilterRequest, PacketDirection.ToServer, ConveyorFilterNet.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.PriorityFiltersChanged, PacketDirection.ToClient, ConveyorFilterNet.HandleSnapshot);
            JoinSync.Register("priority-filters", ConveyorFilterNet.WriteSnapshot, ConveyorFilterNet.ReadSnapshot);
        }

        public override void OnWorldLoad() => ConveyorFilterNet.Reset();

        public override void OnWorldUnload()
        {
            ConveyorFilterNet.Reset();
            VirtualItemSystem.ConveyorNetworkMap.Clear();
        }
    }

    // Priority conveyor filters (roadmap patch 7.3, findings U6 and I4).
    //
    // Authority: the server (or single-player) owns the networks and their filters. They drive where vItems go, and the server saves them.
    //  - Placement: PriorityConveyorPlacedConsumer (patch 3.3) already builds the network on the server.
    //  - Setting a filter: a right-click on a multiplayer client changes nothing locally. It sends ConveyorFilterRequest(x, y, item type);
    //    the server checks the player (alive, in reach), that the tile is a priority conveyor and that the item type is valid, applies the same
    //    toggle as single-player (the same item again clears the filter), and marks the filters dirty. Single-player applies it directly.
    //  - Replication: a client only needs the FILTERED networks (filter + tile list) to draw the filter icons. The server rebuilds that
    //    snapshot at most every 6 ticks after any change (filter set, conveyor placed or broken) and broadcasts PriorityFiltersChanged when it
    //    really differs from the last one; the join provider "priority-filters" sends the same bytes to a joining player.
    //  - The client replaces its whole network map with the snapshot (on the main thread: the handler only queues it).
    public static class ConveyorFilterNet
    {
        static volatile bool dirty;
        static uint lastSendTick;
        static byte[] lastSnapshot;
        static readonly ConcurrentQueue<List<KeyValuePair<int, List<Point>>>> incoming = new();

        // Temporary test counters (remove in Phase 10).
        public static int ServerAccepted;
        public static int ServerRefused;
        public static int ServerBroadcasts;
        public static int ClientSent;
        public static int ClientSnapshots;

        public static void Reset()
        {
            dirty = false;
            lastSnapshot = null;
            lastSendTick = 0;
            while (incoming.TryDequeue(out _)) { }
            ServerAccepted = ServerRefused = ServerBroadcasts = ClientSent = ClientSnapshots = 0;
        }

        public static string Describe()
        {
            return "filters: accepted=" + ServerAccepted + " refused=" + ServerRefused + " broadcasts=" + ServerBroadcasts + " sent=" + ClientSent + " snapshots=" + ClientSnapshots
                + " networks=" + VirtualItemSystem.ConveyorNetworkMap.Values.Distinct().Count(n => n.FilteredItemId != ItemID.None);
        }

        /// <summary>Server only (no-op elsewhere): the filtered networks changed, so the next tick that is due rebuilds and broadcasts the snapshot.</summary>
        public static void MarkDirty()
        {
            if (Main.netMode == NetmodeID.Server)
                dirty = true;
        }

        // ---------------------------------------------------------------- request

        /// <summary>
        /// Owner side (the tile's RightClick): set the filter of the network at (x, y), or clear it when it is already that item.
        /// Single-player applies it directly; a multiplayer client sends the request and waits for the snapshot. Returns false when the
        /// tile is not a priority conveyor.
        /// </summary>
        public static bool RequestSet(int x, int y, int itemType)
        {
            if (!VirtualItemSystem.IsPriorityConveyorTile(x, y))
                return false;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                FactorrariaNet.SendRequest(FactorrariaPacket.ConveyorFilterRequest, writer =>
                {
                    writer.Write((short)x);
                    writer.Write((short)y);
                    writer.Write(itemType);
                });
                ClientSent++;
                return true;
            }

            return VirtualItemSystem.SetConveyorFilter(x, y, itemType);
        }

        public static void HandleRequest(BinaryReader reader, int sender)
        {
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();
            int itemType = reader.ReadInt32();

            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers)
                return;

            Player who = Main.player[sender];
            string refusal = null;

            if (who == null || !who.active || who.dead)
                refusal = "player not available";
            else if (!WorldGen.InWorld(x, y, 1) || !VirtualItemSystem.IsPriorityConveyorTile(x, y))
                refusal = "not a priority conveyor";
            else if (!FactorrariaNet.InReach(who, x, y))
                refusal = "out of reach";
            else if (itemType != ItemID.None && !VirtualItemSystem.IsValidItemID(itemType))
                refusal = "invalid item type " + itemType;

            if (refusal != null)
            {
                ServerRefused++;
                FactorrariaNet.Log("[Conveyor] refused filter request from " + (who?.name ?? "?") + " at (" + x + ", " + y + "): " + refusal);
                return;
            }

            VirtualItemSystem.SetConveyorFilter(x, y, itemType);
            ServerAccepted++;
            FactorrariaNet.Log("[Conveyor] " + who.name + " set the filter at (" + x + ", " + y + ") to item " + itemType);
        }

        // ---------------------------------------------------------------- server: snapshot

        /// <summary>Server (called from VirtualItemSystem.PostUpdateWorld): broadcasts the filtered networks when they changed.</summary>
        public static void ServerTick()
        {
            if (Main.netMode != NetmodeID.Server || !dirty)
                return;

            if (Main.GameUpdateCount - lastSendTick < 6)
                return;

            dirty = false;
            lastSendTick = Main.GameUpdateCount;

            byte[] data = BuildSnapshot();
            if (lastSnapshot != null && data.SequenceEqual(lastSnapshot))
                return;

            lastSnapshot = data;
            FactorrariaNet.Broadcast(FactorrariaPacket.PriorityFiltersChanged, writer => writer.Write(data));
            ServerBroadcasts++;
        }

        static byte[] BuildSnapshot()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
                WriteSnapshot(writer);

            return stream.ToArray();
        }

        /// <summary>Writes every filtered network (filter, tile list), sorted so equal states give equal bytes. Also the join provider's writer.</summary>
        public static void WriteSnapshot(BinaryWriter writer)
        {
            List<List<Point>> networks = new();
            List<int> filters = new();

            foreach (PriorityConveyorNetwork network in VirtualItemSystem.ConveyorNetworkMap.Values.Distinct())
            {
                if (network.FilteredItemId == ItemID.None || network.Tiles.Count == 0)
                    continue;

                networks.Add(network.Tiles.OrderBy(p => p.X).ThenBy(p => p.Y).Take(ushort.MaxValue).ToList());
                filters.Add(network.FilteredItemId);
            }

            List<int> order = Enumerable.Range(0, networks.Count).OrderBy(i => networks[i][0].X).ThenBy(i => networks[i][0].Y).ToList();

            writer.Write((ushort)order.Count);
            foreach (int i in order)
            {
                writer.Write(filters[i]);
                writer.Write((ushort)networks[i].Count);
                foreach (Point tile in networks[i])
                {
                    writer.Write((short)tile.X);
                    writer.Write((short)tile.Y);
                }
            }
        }

        // ---------------------------------------------------------------- client

        public static void HandleSnapshot(BinaryReader reader, int sender)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ReadSnapshot(reader);
        }

        /// <summary>Reads one snapshot and queues it; the main thread applies it. Also the join provider's reader.</summary>
        public static void ReadSnapshot(BinaryReader reader)
        {
            List<KeyValuePair<int, List<Point>>> networks = new();

            int count = reader.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                int filter = reader.ReadInt32();
                int tileCount = reader.ReadUInt16();
                List<Point> tiles = new(tileCount);
                for (int t = 0; t < tileCount; t++)
                    tiles.Add(new Point(reader.ReadInt16(), reader.ReadInt16()));

                networks.Add(new KeyValuePair<int, List<Point>>(filter, tiles));
            }

            incoming.Enqueue(networks);
        }

        /// <summary>Client, main thread: applies the newest queued snapshot (older ones are obsolete).</summary>
        public static void ClientTick()
        {
            List<KeyValuePair<int, List<Point>>> latest = null;
            while (incoming.TryDequeue(out List<KeyValuePair<int, List<Point>>> snapshot))
            {
                latest = snapshot;
                ClientSnapshots++;
            }

            if (latest != null)
                VirtualItemSystem.ReplaceFilterNetworks(latest);
        }
    }
}