using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Networks;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    public class LiquidNetworkSystem : ModSystem
    {
        // Every placed pipe tile's position, world-wide — the liquid equivalent of
        // PowerGridSystem.AllMachines, just tracking pipe tiles instead of machines,
        // since pipes (not machines) are what actually forms the connected graph here.
        public static HashSet<Point> AllPipeTiles = new();
        public static List<LiquidNetwork> ActiveNetworks = new();
        public static bool networkNeedsRebuilding = true;

        const float DecayPerClimbTile = 5f; // tunable, not a locked design value

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (networkNeedsRebuilding)
            {
                RebuildNetworks();
                networkNeedsRebuilding = false;
            }

            for (int i = 0; i < ActiveNetworks.Count; i++)
            {
                ActiveNetworks[i].Tick();
            }
        }

        // Called from PipeTileBase.PlaceInWorld / KillTile.
        public static void RegisterPipeTile(Point pos)
        {
            AllPipeTiles.Add(pos);
            networkNeedsRebuilding = true;
        }

        public static void UnregisterPipeTile(Point pos)
        {
            AllPipeTiles.Remove(pos);
            networkNeedsRebuilding = true;
        }

        public override void OnWorldLoad()
        {
            AllPipeTiles.Clear();
            ActiveNetworks.Clear();
            networkNeedsRebuilding = true;
        }

        public override void OnWorldUnload()
        {
            AllPipeTiles.Clear();
            ActiveNetworks.Clear();
        }

        static readonly Point[] Offsets = { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) };

        void RebuildNetworks()
        {
            ActiveNetworks.Clear();

            // Same "keep pulling an unvisited one out and flood-fill from it" shape
            // as PowerGridSystem.RebuildNetworks — just seeded from pipe tiles instead
            // of machines, since pipes (not machines) are the connective structure here.
            HashSet<Point> remainingPipes = new HashSet<Point>(AllPipeTiles);

            while (remainingPipes.Count > 0)
            {
                Point start = remainingPipes.First();
                LiquidNetwork network = BuildNetworkFromPipe(start, remainingPipes);
                ActiveNetworks.Add(network);
            }
        }

        LiquidNetwork BuildNetworkFromPipe(Point start, HashSet<Point> remainingPipes)
        {
            LiquidNetwork network = new LiquidNetwork();
            Queue<Point> queue = new Queue<Point>();

            queue.Enqueue(start);
            remainingPipes.Remove(start);
            network.PipeTiles.Add(start);
            NarrowCapacity(network, start);
            ResolveFlow(network);

            while (queue.Count > 0)
            {
                Point current = queue.Dequeue();

                foreach (Point offset in Offsets)
                {
                    Point neighbor = new Point(current.X + offset.X, current.Y + offset.Y);
                    int neighborTileType = Main.tile[neighbor.X, neighbor.Y].TileType;

                    if (PipeTierRegistry.IsPipeTile(neighborTileType))
                    {
                        if (remainingPipes.Contains(neighbor))
                        {
                            remainingPipes.Remove(neighbor);
                            network.PipeTiles.Add(neighbor);
                            NarrowCapacity(network, neighbor);
                            queue.Enqueue(neighbor);
                        }
                        continue;
                    }

                    RecordAttachmentIfAny(network, current, neighbor, offset, queue, remainingPipes);
                }
            }

            return network;
        }

        void NarrowCapacity(LiquidNetwork network, Point pipePos)
        {
            int tileType = Main.tile[pipePos.X, pipePos.Y].TileType;
            float thisPipeCapacity = PipeTierRegistry.MaxFlowRateByTileType[tileType];
            network.MaxFlowRate = Math.Min(network.MaxFlowRate, thisPipeCapacity);
        }

        void RecordAttachmentIfAny(LiquidNetwork network, Point pipePos, Point neighborPos, Point offset, Queue<Point> pipeQueue, HashSet<Point> remainingPipes)
        {
            Direction mouthDirection = DirectionExtensions.FromOffset(offset);

            // NEW: if this pipe's rendered mouth doesn't actually face this neighbor,
            // it's not a usable connection, even though the tiles are adjacent.
            if (!DirectionExtensions.IsPipeMouthOpen(pipePos.X, pipePos.Y, mouthDirection))
            {
                return;
            }

            if (TileEntityHelper.TryGetEntityFromTile(neighborPos.X, neighborPos.Y, out TileEntity entity, out _))
            {
                // Motors are the one exception to "machines are endpoints" — record it
                // AND keep flood-filling through it, so pipes on either side of a motor
                // end up in the same network (the whole point of a motor is connecting
                // an intake run to a discharge run).
                if (entity is MotorTileEntityBase motor)
                {
                    // Avoid double-recording the same motor if the scan reaches it from
                    // more than one side (e.g. a motor with pipes on 3 sides).
                    bool alreadyRecorded = network.Motors.Exists(m => m.Position == neighborPos);
                    if (!alreadyRecorded)
                        network.Motors.Add(new MotorAttachment { Position = neighborPos, Motor = motor });

                    // Treat the motor's own tile as if it were a pipe tile for traversal
                    // purposes only — this is what makes it a bridge instead of a dead end.
                    if (remainingPipes.Contains(neighborPos))
                    {
                        remainingPipes.Remove(neighborPos);
                        pipeQueue.Enqueue(neighborPos);
                    }
                    return;
                }

                if (entity is BaseMachine machine)
                {
                    network.MachineAttachments.Add(new PipeAttachment
                    {
                        Position = neighborPos,
                        PipePosition = pipePos,
                        Machine = machine
                    });
                    return;
                }
            }

            Tile neighborTile = Main.tile[neighborPos.X, neighborPos.Y];
            if (neighborTile.LiquidAmount > 0)
            {
                network.WorldLiquidAttachments.Add(new PipeAttachment
                {
                    Position = neighborPos,
                    PipePosition = pipePos,
                    Machine = null
                });
            }
        }

        void ResolveFlow(LiquidNetwork network)
        {
            network.ResolvedFlow.Clear();
            if (network.Motors.Count == 0) return;

            var frontier = new PriorityQueueLite<(Point tile, Direction dir, float magnitude)>();

            // Seed every motor's discharge AND intake side at once — this is what lets
            // motors in the same network compete or reinforce each other in one pass,
            // instead of resolving each motor in isolation first.
            foreach (var m in network.Motors)
            {
                Point discharge = m.Position + m.Motor.Facing.ToOffset();
                Point intake = m.Position - m.Motor.Facing.ToOffset();
                frontier.Enqueue((discharge, m.Motor.Facing, m.Motor.PumpStrength), m.Motor.PumpStrength);
                frontier.Enqueue((intake, m.Motor.Facing, m.Motor.PumpStrength), m.Motor.PumpStrength);
            }

            while (frontier.TryDequeue(out var packet, out _))
            {
                var (tile, dir, magnitude) = packet;
                if (!PipeTierRegistry.IsPipeTile(Main.tile[tile.X, tile.Y].TileType)) continue;

                if (network.ResolvedFlow.TryGetValue(tile, out var existing))
                {
                    if (existing.Direction == dir)
                    {
                        if (magnitude <= existing.Magnitude)
                        {
                            continue; // weaker duplicate, discard
                            // no addition here — just let the stronger value overwrite below
                        }
                    }
                    else
                    {
                        float net = magnitude - existing.Magnitude;
                        if (net <= 0) 
                        {
                            continue;
                        }
                        magnitude = net;
                    }
                }

                magnitude = Math.Min(magnitude, network.MaxFlowRate); // capped by weakest pipe tier
                network.ResolvedFlow[tile] = (dir, magnitude);

                // If ANOTHER motor sits on this exact tile, inject its strength right here,
                // in the same direction, before continuing to propagate outward — this is
                // literally "motor B adds its pump power at the position of the motor."
                if (TileEntityHelper.TryGetEntityFromTile(tile.X, tile.Y, out TileEntity te, out _)
                    && te is MotorTileEntityBase otherMotor && otherMotor.Facing == dir)
                {
                    magnitude += otherMotor.PumpStrength;
                    magnitude = Math.Min(magnitude, network.MaxFlowRate);
                    network.ResolvedFlow[tile] = (dir, magnitude);
                }

                foreach (Point offset in Offsets)
                {
                    Point neighbor = new Point(tile.X + offset.X, tile.Y + offset.Y);
                    bool climbed = offset.Y < 0; // moving to smaller Y = upward = penalized
                    float decayed = magnitude - (climbed ? DecayPerClimbTile : 0f);
                    if (decayed <= 0) continue; // fully decayed, nothing left to propagate

                    frontier.Enqueue((neighbor, dir, decayed), decayed);
                }
            }
        }

    }

    // Small linear-scan priority queue — fine at this network scale, only recomputed
    // on topology change, not per tick. Swap for .NET's built-in PriorityQueue<T,T>
    // later if networks get large enough for this to matter.
    class PriorityQueueLite<T>
    {
        List<(T item, float priority)> heap = new();
        public void Enqueue(T item, float priority) => heap.Add((item, priority));
        public bool TryDequeue(out T item, out float priority)
        {
            if (heap.Count == 0) { item = default; priority = 0; return false; }
            int best = 0;
            for (int i = 1; i < heap.Count; i++)
                if (heap[i].priority > heap[best].priority) best = i; // largest first now, not smallest
            (item, priority) = heap[best];
            heap.RemoveAt(best);
            return true;
        }
    }
}