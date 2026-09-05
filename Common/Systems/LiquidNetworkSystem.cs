using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Networks;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

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

        public override void SaveWorldData(TagCompound tag)
        {
            List<int[]> pipePositions = AllPipeTiles.Select(p => new int[] { p.X, p.Y }).ToList();
            tag["AllPipeTiles"] = pipePositions;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            AllPipeTiles.Clear();
            if (tag.ContainsKey("AllPipeTiles"))
            {
                var list = tag.GetList<int[]>("AllPipeTiles");
                foreach (var pos in list)
                {
                    AllPipeTiles.Add(new Point(pos[0], pos[1]));
                }
            }
            networkNeedsRebuilding = true;
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
            HashSet<Point> visited = new HashSet<Point>();

            queue.Enqueue(start);
            remainingPipes.Remove(start);
            visited.Add(start);
            network.PipeTiles.Add(start);
            NarrowCapacity(network, start);

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
                            visited.Add(neighbor);
                            network.PipeTiles.Add(neighbor);
                            NarrowCapacity(network, neighbor);
                            queue.Enqueue(neighbor);
                        }
                        continue;
                    }

                    RecordAttachmentIfAny(network, current, neighbor, offset, queue, visited);
                }
            }

            ResolveFlow(network);

            var config = ModContent.GetInstance<FurnaceOffsetConfig>();
            if (config.EnableDebugs)
            {
                Main.NewText($"[LiquidDebug] Network built — Pipes:{network.PipeTiles.Count} Motors:{network.Motors.Count} " +
                             $"MachineAtt:{network.MachineAttachments.Count} WorldAtt:{network.WorldLiquidAttachments.Count} " +
                             $"ResolvedFlow:{network.ResolvedFlow.Count}", Color.Cyan);
            }

            return network;
        }

        void NarrowCapacity(LiquidNetwork network, Point pipePos)
        {
            int tileType = Main.tile[pipePos.X, pipePos.Y].TileType;
            float thisPipeCapacity = PipeTierRegistry.MaxFlowRateByTileType[tileType];
            network.MaxFlowRate = Math.Min(network.MaxFlowRate, thisPipeCapacity);
        }

        void RecordAttachmentIfAny(LiquidNetwork network, Point pipePos, Point neighborPos, Point offset, Queue<Point> pipeQueue, HashSet<Point> visited)
        {
            var config = ModContent.GetInstance<FurnaceOffsetConfig>();
            Direction mouthDirection = DirectionExtensions.FromOffset(offset);

            if (TileEntityHelper.TryGetEntityFromTile(neighborPos.X, neighborPos.Y, out TileEntity entity, out _))
            {
                if (entity is MotorTileEntityBase motor)
                {
                    if (!PipeConnectionHelper.CanConnect(neighborPos.X, neighborPos.Y, mouthDirection))
                    {
                        if (config.EnableDebugs)
                            Main.NewText($"[LiquidDebug] Motor at {neighborPos} REJECTED — facing={motor.Facing}, pipe approached from {mouthDirection}", Color.OrangeRed);
                        return;
                    }

                    if (!network.Motors.Exists(m => m.Position == neighborPos))
                    {
                        network.Motors.Add(new MotorAttachment { Position = neighborPos, Motor = motor });
                        if (config.EnableDebugs)
                            Main.NewText($"[LiquidDebug] Motor REGISTERED at {neighborPos}, facing={motor.Facing}", Color.Lime);
                    }

                    if (visited.Add(neighborPos))
                    {
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
                        MouthDirection = mouthDirection,
                        Machine = machine
                    });
                    if (config.EnableDebugs)
                        Main.NewText($"[LiquidDebug] Machine attachment at {neighborPos}, mouth={mouthDirection}", Color.Lime);
                    return;
                }
            }

            PipeConnectionHelper.GetOpenSides(pipePos.X, pipePos.Y, out bool up, out bool down, out bool left, out bool right);
            bool isOpenSide = mouthDirection switch
            {
                Direction.Up => up,
                Direction.Down => down,
                Direction.Left => left,
                Direction.Right => right,
                _ => false
            };

            if (!isOpenSide)
            {
                if (config.EnableDebugs)
                    Main.NewText($"[LiquidDebug] World tile at {neighborPos} REJECTED — side not open (mouth={mouthDirection})", Color.OrangeRed);
                return;
            }

            Tile neighborTile = Main.tile[neighborPos.X, neighborPos.Y];
            if (neighborTile.HasTile && Main.tileSolid[neighborTile.TileType])
            {
                if (config.EnableDebugs)
                    Main.NewText($"[LiquidDebug] World tile at {neighborPos} REJECTED — solid tile in the way", Color.OrangeRed);
                return;
            }

            network.WorldLiquidAttachments.Add(new PipeAttachment
            {
                Position = neighborPos,
                PipePosition = pipePos,
                MouthDirection = mouthDirection,
                Machine = null
            });

            if (config.EnableDebugs)
                Main.NewText($"[LiquidDebug] World attachment REGISTERED at {neighborPos}, mouth={mouthDirection}, liquidAmount={neighborTile.LiquidAmount}", Color.Lime);
        }

        void ResolveFlow(LiquidNetwork network)
        {
            var config = ModContent.GetInstance<FurnaceOffsetConfig>();
            network.ResolvedFlow.Clear();
            if (network.Motors.Count == 0) return;

            var frontier = new PriorityQueueLite<(Point tile, Direction dir, float magnitude)>();

            foreach (var m in network.Motors)
            {
                Point discharge = m.Position + m.Motor.Facing.ToOffset();
                Point intake = m.Position - m.Motor.Facing.ToOffset();
                frontier.Enqueue((discharge, m.Motor.Facing, m.Motor.PumpStrength), m.Motor.PumpStrength);
                frontier.Enqueue((intake, m.Motor.Facing, m.Motor.PumpStrength), m.Motor.PumpStrength);

                if (config.EnableDebugs)
                    Main.NewText($"[LiquidDebug] Seeding motor {m.Position} — discharge={discharge}, intake={intake}, strength={m.Motor.PumpStrength}", Color.Yellow);
            }

            while (frontier.TryDequeue(out var packet, out _))
            {
                var (tile, dir, magnitude) = packet;

                bool isPipe = PipeTierRegistry.IsPipeTile(Main.tile[tile.X, tile.Y].TileType);
                bool isMotor = TileEntityHelper.TryGetEntityFromTile(tile.X, tile.Y, out TileEntity te, out _) && te is MotorTileEntityBase;

                if (!isPipe && !isMotor) continue;

                if (network.ResolvedFlow.TryGetValue(tile, out var existing))
                {
                    if (existing.Direction == dir)
                    {
                        if (magnitude <= existing.Magnitude) continue;
                    }
                    else
                    {
                        float net = magnitude - existing.Magnitude;
                        if (net <= 0) continue;
                        magnitude = net;
                    }
                }

                magnitude = Math.Min(magnitude, network.MaxFlowRate);
                network.ResolvedFlow[tile] = (dir, magnitude);

                if (config.EnableDebugs)
                    Main.NewText($"[LiquidDebug] ResolvedFlow[{tile}] = dir:{dir} mag:{magnitude}", Color.Yellow);

                if (isMotor && te is MotorTileEntityBase otherMotor && otherMotor.Facing == dir)
                {
                    magnitude += otherMotor.PumpStrength;
                    magnitude = Math.Min(magnitude, network.MaxFlowRate);
                    network.ResolvedFlow[tile] = (dir, magnitude);
                }

                foreach (Point offset in Offsets)
                {
                    Point neighbor = new Point(tile.X + offset.X, tile.Y + offset.Y);
                    bool climbed = offset.Y < 0;
                    float decayed = magnitude - (climbed ? DecayPerClimbTile : 0f);
                    if (decayed <= 0) continue;

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