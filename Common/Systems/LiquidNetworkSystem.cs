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
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Systems
{
    public class LiquidNetworkSystem : ModSystem
    {
        public static HashSet<Point> AllPipeTiles = new();
        public static List<LiquidNetwork> ActiveNetworks = new();
        public static bool networkNeedsRebuilding = true;

        // Liquid overhaul: the NEW networks (nodes, ports, routes). Since step 3 they move all the liquid; the old ActiveNetworks are
        // still built (the old debug overlay and /fpipes read them) but are never ticked.
        public static List<FlowNetwork> FlowNetworks = new();

        // Liquid overhaul step 3: the per-cell banks of world liquid, shared by every flow network (see LiquidWorldBanks).
        public static LiquidWorldBanks WorldBanks = new();

        // TEMPORARY (patch 8.1b, remove in Phase 10): how many times the networks were rebuilt, for the [Pipes] log line and /fpipes.
        public static int RebuildCount;

        const float DecayPerClimbTile = 0.5f * 3600f;


        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (networkNeedsRebuilding)
            {
                HealPipeList();
                RebuildNetworks();
                RestoreSavedNetworkTypes();
                RebuildFlowNetworks();

                RebuildCount++;
                if (Main.netMode == NetmodeID.Server)
                    Mod.Logger.Info("[MP-Net] [Pipes] networks rebuilt: " + ActiveNetworks.Count + " network(s), " + AllPipeTiles.Count + " pipe tile(s); " + global::Factorraria.Common.Net.LiquidPipeDiagnostics.Summary());
                networkNeedsRebuilding = false;
            }

            // Liquid overhaul step 3: the flow networks move the liquid; the old LiquidNetwork.Tick() is no longer called.
            for (int i = 0; i < FlowNetworks.Count; i++)
                TickFlowNetwork(FlowNetworks[i]);
        }

        // A network whose tick throws is logged once and stays still until the next rebuild; it can never break the world update.
        void TickFlowNetwork(FlowNetwork network)
        {
            if (network.TickFailed)
                return;

            try
            {
                network.Tick(WorldBanks);
            }
            catch (Exception e)
            {
                network.TickFailed = true;
                Mod.Logger.Error("[Pipes] a flow network failed to tick and is paused until the next rebuild: " + e);
            }
        }

        // Roadmap 3.3 (finding P1). Before this patch a pipe placed by a client was never registered on the server, so the saved list
        // can miss pipes (and, after a pipe was removed some other way, hold positions that are no longer pipes). Once per world load,
        // before the first network rebuild: drop saved positions whose tile is not a pipe and add every pipe tile found in the world.
        static bool pipeListNeedsHeal = true;

        void HealPipeList()
        {
            if (!pipeListNeedsHeal)
                return;

            pipeListNeedsHeal = false;

            int removed = AllPipeTiles.RemoveWhere(p => !WorldGen.InWorld(p.X, p.Y) || !Main.tile[p.X, p.Y].HasTile || !PipeTierRegistry.IsPipeTile(Main.tile[p.X, p.Y].TileType));

            int added = 0;
            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = 0; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && PipeTierRegistry.IsPipeTile(tile.TileType) && AllPipeTiles.Add(new Point(x, y)))
                        added++;
                }
            }

            Mod.Logger.Info("[MP-Net] [Pipes] self-heal on world load: removed " + removed + " stale position(s), added " + added + " missing pipe tile(s), " + AllPipeTiles.Count + " in total");
        }

        // Called from PipePlacedConsumer (patch 3.3, where the simulation runs) and PipeTileBase.KillTile.
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
            FlowNetworks = new();
            WorldBanks.Clear();
            networkNeedsRebuilding = true;
            pipeListNeedsHeal = true;
        }

        public override void OnWorldUnload()
        {
            pendingNetworkTypes.Clear();
            AllPipeTiles.Clear();
            ActiveNetworks.Clear();
            FlowNetworks = new();
            WorldBanks.Clear();

            LiquidTextureCache.Clear();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            List<int[]> pipePositions = AllPipeTiles.Select(p => new int[] { p.X, p.Y }).ToList();
            tag["AllPipeTiles"] = pipePositions;

            // One anchor pipe + liquid NAME per typed network (names, not ids: ids are registration order).
            var typedPipes = new List<int[]>();
            var typedNames = new List<string>();
            foreach (LiquidNetwork net in ActiveNetworks)
            {
                if (net.LiquidType == -1 || net.PipeTiles.Count == 0) continue;
                Point anchor = net.PipeTiles.OrderBy(p => p.X).ThenBy(p => p.Y).First();
                typedPipes.Add(new[] { anchor.X, anchor.Y });
                typedNames.Add(LiquidTypeRegistry.Get(net.LiquidType).Name);
            }
            tag["NetworkTypePipes"] = typedPipes;
            tag["NetworkTypeNames"] = typedNames;
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

            pendingNetworkTypes.Clear();
            if (tag.ContainsKey("NetworkTypePipes") && tag.ContainsKey("NetworkTypeNames"))
            {
                var pipes = tag.GetList<int[]>("NetworkTypePipes");
                var names = tag.GetList<string>("NetworkTypeNames");
                for (int i = 0; i < Math.Min(pipes.Count, names.Count); i++)
                    pendingNetworkTypes.Add((new Point(pipes[i][0], pipes[i][1]), names[i]));
            }
            networkNeedsRebuilding = true;
        }

        static readonly Point[] Offsets = { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) };

        // Loaded from the world file, applied once after the first rebuild, then discarded.
        static readonly List<(Point Anchor, string LiquidName)> pendingNetworkTypes = new();

        void RestoreSavedNetworkTypes()
        {
            foreach (var (anchor, liquidName) in pendingNetworkTypes)
            {
                int type = LiquidTypeRegistry.definitions.FindIndex(d => d.Name == liquidName);
                if (type == -1) continue;   // that liquid no longer exists

                foreach (LiquidNetwork net in ActiveNetworks)
                {
                    if (!net.PipeTiles.Contains(anchor)) continue;
                    net.LiquidType = type;
                    break;
                }
            }
            pendingNetworkTypes.Clear();
        }

        // Liquid overhaul. The flow networks are rebuilt from the registered pipes. First every whole unit still banked in a world cell
        // is applied (the old networks are about to be replaced), then the new networks are built. An exception is logged and leaves
        // the list empty (no liquid moves until the next successful rebuild).
        void RebuildFlowNetworks()
        {
            try
            {
                WorldBanks.FlushAll();
            }
            catch (Exception e)
            {
                WorldBanks.Clear();
                Mod.Logger.Error("[Pipes] world liquid banks failed to flush: " + e);
            }

            try
            {
                FlowNetworks = FlowNetworkBuilder.BuildAll(AllPipeTiles);
            }
            catch (Exception e)
            {
                FlowNetworks = new();
                Mod.Logger.Error("[Pipes] flow topology failed to build (no liquid moves until the next rebuild): " + e);
            }
        }


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
            Direction mouthDirection = DirectionExtensions.FromOffset(offset);

            if (TileEntityHelper.TryGetEntityFromTile(neighborPos.X, neighborPos.Y, out TileEntity entity, out _))
            {
                if (entity is MotorTileEntityBase motor)
                {
                    if (!PipeConnectionHelper.CanConnect(neighborPos.X, neighborPos.Y, mouthDirection))
                        return; // perpendicular to Facing — not a valid attachment point

                    if (!network.Motors.Exists(m => m.Position == neighborPos))
                        network.Motors.Add(new MotorAttachment { Position = neighborPos, Motor = motor });

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
                    return;
                }
            }

            // World liquid/open-air: only valid on a side the pipe treats as an open mouth (real
            // neighbor, or the auto-opened far end of a straight run), and only if nothing solid
            // is in the way. Registered regardless of current liquid amount so it can serve as a
            // drain OR a dump target later — LiquidNetwork.Tick() decides which, per-tick, from flow direction.
            PipeConnectionHelper.GetOpenSides(pipePos.X, pipePos.Y, out bool up, out bool down, out bool left, out bool right);
            bool isOpenSide = mouthDirection switch
            {
                Direction.Up => up,
                Direction.Down => down,
                Direction.Left => left,
                Direction.Right => right,
                _ => false
            };

            if (!isOpenSide) return;

            Tile neighborTile = Main.tile[neighborPos.X, neighborPos.Y];
            if (neighborTile.HasTile && Main.tileSolid[neighborTile.TileType]) return; // can't dump into or drain solid rock

            network.WorldLiquidAttachments.Add(new PipeAttachment
            {
                Position = neighborPos,
                PipePosition = pipePos,
                MouthDirection = mouthDirection,
                Machine = null
            });
        }

        void ResolveFlow(LiquidNetwork network)
        {
            network.ResolvedFlow.Clear();
            if (network.Motors.Count == 0) return;

            // An off motor caps the network at its pipe-tier rate instead of its pump
            // strength — computed fresh here (not cached on the network) since isOn can
            // flip without a topology rebuild.
            float effectiveMaxFlowRate = network.MaxFlowRate;
            foreach (var m in network.Motors)
            {
                if (!m.Motor.isOn)
                    effectiveMaxFlowRate = Math.Min(effectiveMaxFlowRate, m.Motor.PipeEquivalentMaxFlowRate);
            }

            var frontier = new PriorityQueueLite<(Point tile, Direction dir, float magnitude, bool isDischarge, MotorTileEntityBase sourceMotor)>();

            foreach (var m in network.Motors)
            {
                if (!m.Motor.isOn) continue; // dead motor pumps nothing

                Point discharge = m.Position + m.Motor.Facing.ToOffset();
                Point intake = m.Position - m.Motor.Facing.ToOffset();

                frontier.Enqueue((discharge, m.Motor.Facing, m.Motor.PumpStrength, true, m.Motor), m.Motor.PumpStrength);
                frontier.Enqueue((intake, m.Motor.Facing, m.Motor.PumpStrength, false, m.Motor), m.Motor.PumpStrength);
            }

            while (frontier.TryDequeue(out var packet, out _))
            {
                var (tile, dir, magnitude, isDischarge, sourceMotor) = packet;

                bool isPipe = PipeTierRegistry.IsPipeTile(Main.tile[tile.X, tile.Y].TileType);
                bool isMotor = TileEntityHelper.TryGetEntityFromTile(tile.X, tile.Y, out TileEntity te, out _) && te is MotorTileEntityBase;

                if (!isPipe && !isMotor) continue;

                if (network.ResolvedFlow.TryGetValue(tile, out var existing))
                {
                    if (existing.Direction == dir)
                    {
                        if (magnitude <= existing.Magnitude)
                        {
                            continue;
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

                magnitude = Math.Min(magnitude, effectiveMaxFlowRate);
                network.ResolvedFlow[tile] = (dir, magnitude);

                if (isMotor && te is MotorTileEntityBase otherMotor && otherMotor.isOn && otherMotor.Facing == dir && otherMotor != sourceMotor)
                {
                    magnitude += otherMotor.PumpStrength;
                    magnitude = Math.Min(magnitude, effectiveMaxFlowRate);
                    network.ResolvedFlow[tile] = (dir, magnitude);
                    sourceMotor = otherMotor;
                }

                foreach (Point offset in Offsets)
                {
                    Point neighbor = new Point(tile.X + offset.X, tile.Y + offset.Y);
                    bool climbed = offset.Y < 0;
                    float decayed = magnitude - (climbed ? DecayPerClimbTile : 0f);
                    if (decayed <= 0) continue;

                    Direction stepDir = DirectionExtensions.FromOffset(offset);

                    if (isDischarge && isMotor && te is MotorTileEntityBase currentMotor && stepDir == currentMotor.Facing.Opposite())
                        continue;

                    Direction newDir = isDischarge ? stepDir : stepDir.Opposite();

                    frontier.Enqueue((neighbor, newDir, decayed, isDischarge, sourceMotor), decayed);
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