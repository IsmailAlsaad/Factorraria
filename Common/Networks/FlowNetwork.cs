using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Networks.Flow;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Terraria;
using Terraria.DataStructures;

namespace Factorraria.Common.Networks
{
    // Liquid overhaul: the game-side topology of one pipe network, built from the real tiles and machines (step 2) and handed to the
    // pure core in Common/Networks/Flow, plus the per-tick transfer of liquid through it (step 3, the Tick region below).
    //
    // Multiplayer: server / single-player only (LiquidNetworkSystem.PostUpdateWorld returns on a client).
    // It is derived data: rebuilt from tiles, never saved, never sent. A client never has one.

    // A place liquid can be taken from: ONE output tank of a machine, or ONE world liquid cell.
    public sealed class FlowSource
    {
        public BaseMachine Machine;   // null = world liquid cell
        public LiquidStack Tank;      // null for a world cell
        public int TankIndex;         // index in Machine.OutputLiquids
        public Point WorldPos;        // only for a world cell
        public int Owner;             // same Owner as the sink of the same machine / cell: a thing never pumps into itself
        public bool IsWorld => Machine == null;
    }

    // A place liquid can be put into: ALL input tanks of a machine together (the tank is picked by liquid type when it arrives,
    // the way FindSinkSlot does today), or ONE world liquid cell.
    public sealed class FlowSink
    {
        public BaseMachine Machine;   // null = world liquid cell
        public Point WorldPos;
        public int Owner;
        public bool IsWorld => Machine == null;
    }

    public sealed class FlowNetwork
    {
        // Nodes: every pipe tile and every motor tile of the network, sorted by (X, Y). Index = node index in the core.
        public Point[] Positions = Array.Empty<Point>();
        public FlowNode[] Nodes = Array.Empty<FlowNode>();
        public MotorTileEntityBase[] Motors = Array.Empty<MotorTileEntityBase>();   // null for a pipe
        public float[] PassRate = Array.Empty<float>();   // what the node carries when it is a plain pipe: pipe tier rate, or the motor's PipeEquivalentMaxFlowRate

        // Ports (what the solver is told about) and the attach points the route finder walks from/to.
        public List<FlowSource> Sources = new();
        public List<FlowSink> Sinks = new();
        public FlowPort[] SourceAttach = Array.Empty<FlowPort>();
        public FlowPort[] SinkAttach = Array.Empty<FlowPort>();
        public int[][] SourcePortsOfAttach = Array.Empty<int[]>();
        public int[][] SinkPortsOfAttach = Array.Empty<int[]>();

        // Port-level routes (Src = index in Sources, Dst = index in Sinks). Rebuilt by RefreshRoutes.
        public List<FlowRoute> Routes = new();
        public FlowSolver Solver = new();

        // The bottleneck: the weakest pipe tier in the network, and every OFF motor counts as a pipe of its own tier.
        public float Cap = float.MaxValue;
        public float DecayPerClimbPerMinute = 0.5f * 3600f;

        public double BuildMs;      // last full build, for /fflow
        public double RefreshMs;    // last route refresh

        // Re-reads every motor (on/off, strength), recomputes the cap and the route table. Needs no topology rebuild:
        // a power flip changes which routes exist, nothing else.
        public void RefreshRoutes()
        {
            var timer = Stopwatch.StartNew();

            for (int i = 0; i < Nodes.Length; i++)
            {
                MotorTileEntityBase motor = Motors[i];
                if (motor == null) continue;

                Nodes[i].MotorOn = motor.isOn;
                Nodes[i].Strength = motor.PumpStrength;
            }

            Cap = ComputeCap();
            List<FlowRoute> raw = FlowRouteFinder.Build(Nodes, SourceAttach, SinkAttach, DecayPerClimbPerMinute, Cap);
            Routes = FlowRouteMerger.Merge(raw, SourcePortsOfAttach, SinkPortsOfAttach);
            MarkUsedPorts();

            timer.Stop();
            RefreshMs = timer.Elapsed.TotalMilliseconds;
        }

        // ---------------------------------------------------------------------------------------------------------- the tick (step 3)
        // Server / single-player only (LiquidNetworkSystem.PostUpdateWorld returns on a client). Nothing here is saved or sent: clients
        // learn the result through the machine tank light sync (5.2) and the world-liquid broadcast (8.1, LiquidBroadcastSystem.Mark).
        // Pipes hold no liquid: every tick the solver moves liquid straight from source ports to sink ports, within the route rates
        // and the pump budgets, and the transfers are applied right away.

        public bool TickFailed;   // set by LiquidNetworkSystem when Tick threw: this network then stays still until the next rebuild
        public IReadOnlyList<FlowTransfer> LastTransfers => transfers;

        readonly List<FlowTransfer> transfers = new();
        bool[] sourceUsed = Array.Empty<bool>();          // sources that appear in at least one route (filled by RefreshRoutes)
        bool[] sourceBottomless = Array.Empty<bool>();
        float[] sourceOffer = Array.Empty<float>();
        int[] sourceType = Array.Empty<int>();
        float[] pumpBudget = Array.Empty<float>();
        Func<int, int, float> sinkSpaceFunc;
        LiquidWorldBanks banks;

        float bottomlessOffer = 1e9f;       // what a bottomless world source offers the solver per tick
        float tankEmptyBelow = 0.0001f;     // a machine tank drained below this is empty and forgets its liquid type

        public void Tick(LiquidWorldBanks worldBanks)
        {
            banks = worldBanks;

            // A motor that was switched (power, wire toggle, brownout) or changed strength: only the route table changes.
            if (MotorsChanged())
                RefreshRoutes();

            if (Routes.Count == 0)
            {
                Solver.ActiveType = -1;
                transfers.Clear();
                return;
            }

            EnsureTickBuffers();
            FillSources();
            FillPumpBudgets();

            Solver.Solve(Routes, sourceOffer, sourceType, Sinks.Count, sinkSpaceFunc, pumpBudget, transfers);
            ApplyTransfers();
        }

        bool MotorsChanged()
        {
            for (int i = 0; i < Motors.Length; i++)
            {
                MotorTileEntityBase motor = Motors[i];
                if (motor == null) continue;
                if (Nodes[i].MotorOn != motor.isOn || Nodes[i].Strength != motor.PumpStrength) return true;
            }
            return false;
        }

        void MarkUsedPorts()
        {
            if (sourceUsed.Length != Sources.Count) sourceUsed = new bool[Sources.Count];
            else Array.Clear(sourceUsed, 0, sourceUsed.Length);

            foreach (FlowRoute route in Routes)
                sourceUsed[route.Src] = true;
        }

        void EnsureTickBuffers()
        {
            if (sourceOffer.Length != Sources.Count)
            {
                sourceOffer = new float[Sources.Count];
                sourceType = new int[Sources.Count];
                sourceBottomless = new bool[Sources.Count];
            }
            if (pumpBudget.Length != Nodes.Length) pumpBudget = new float[Nodes.Length];
            sinkSpaceFunc ??= SinkSpace;
        }

        // The tank a machine source port reads from. Looked up fresh every tick, never cached from the build.
        static LiquidStack TankOf(FlowSource source)
        {
            LiquidStack[] tanks = source.Machine.OutputLiquids;
            return source.TankIndex < tanks.Length ? tanks[source.TankIndex] : null;
        }

        void FillSources()
        {
            for (int s = 0; s < Sources.Count; s++)
            {
                sourceOffer[s] = 0f;
                sourceType[s] = -1;
                sourceBottomless[s] = false;
                if (!sourceUsed[s]) continue;

                FlowSource source = Sources[s];
                if (source.IsWorld)
                {
                    Tile tile = Main.tile[source.WorldPos.X, source.WorldPos.Y];
                    if (tile.LiquidAmount <= 0) continue;

                    int? worldType = LiquidTypeRegistry.FromTileLiquidId((byte)tile.LiquidType);
                    if (worldType == null) continue;

                    sourceType[s] = worldType.Value;
                    sourceOffer[s] = banks.WorldSourceOffer(source.WorldPos);
                }
                else
                {
                    LiquidStack tank = TankOf(source);
                    if (tank == null || tank.IsEmpty) continue;

                    sourceType[s] = tank.LiquidType;
                    sourceOffer[s] = tank.Amount;
                }
            }

            // A machine that treats world liquid as bottomless (Magic Materializer + shimmer) makes the world sources routed to it bottomless.
            foreach (FlowRoute route in Routes)
            {
                int s = route.Src;
                if (!Sources[s].IsWorld || sourceType[s] == -1) continue;

                FlowSink sink = Sinks[route.Dst];
                if (!sink.IsWorld && sink.Machine.TreatsWorldLiquidAsInfinite(sourceType[s]))
                    sourceBottomless[s] = true;
            }

            // A big enough body of liquid is never drained (same rule as before and as the terrarium carts).
            for (int s = 0; s < Sources.Count; s++)
            {
                if (!Sources[s].IsWorld || sourceType[s] == -1) continue;

                if (!sourceBottomless[s] && WorldLiquidSources.IsInfiniteSource(Sources[s].WorldPos))
                    sourceBottomless[s] = true;
                if (sourceBottomless[s])
                    sourceOffer[s] = bottomlessOffer;
            }
        }

        // Units per tick each ON motor can push: its strength, but never more than the network's weakest pipe.
        void FillPumpBudgets()
        {
            for (int i = 0; i < Nodes.Length; i++)
            {
                pumpBudget[i] = Nodes[i].IsMotor && Nodes[i].MotorOn
                    ? Math.Min(Nodes[i].Strength, Cap) / 3600f
                    : 0f;
            }
        }

        // Room for that liquid in a sink right now. Called by the solver and again while applying, so it is always live.
        float SinkSpace(int sinkIndex, int liquidType)
        {
            FlowSink sink = Sinks[sinkIndex];
            if (sink.IsWorld)
                return banks.WorldSinkSpace(sink.WorldPos, liquidType);

            BaseMachine machine = sink.Machine;
            LiquidStack slot = LiquidTankRules.FindSinkSlot(machine.InputLiquids, machine.ResolveIncomingLiquid(liquidType));
            return slot == null ? 0f : Math.Max(0f, slot.Capacity - slot.Amount);
        }

        void ApplyTransfers()
        {
            for (int i = 0; i < transfers.Count; i++)
            {
                FlowTransfer transfer = transfers[i];

                // Never take anything the sink cannot hold, so nothing is withdrawn and lost.
                float amount = Math.Min(transfer.Amount, SinkSpace(transfer.Dst, transfer.Type));
                if (amount <= 0f) continue;

                amount = Withdraw(transfer.Src, transfer.Type, amount);
                if (amount <= 0f) continue;

                Deposit(transfer.Dst, transfer.Type, amount);
            }
        }

        float Withdraw(int sourceIndex, int liquidType, float amount)
        {
            FlowSource source = Sources[sourceIndex];

            if (source.IsWorld)
            {
                Tile tile = Main.tile[source.WorldPos.X, source.WorldPos.Y];
                if (tile.LiquidAmount <= 0) return 0f;

                int? worldType = LiquidTypeRegistry.FromTileLiquidId((byte)tile.LiquidType);
                if (worldType == null || worldType.Value != liquidType) return 0f;

                if (!sourceBottomless[sourceIndex])
                    banks.AddWithdraw(source.WorldPos, amount);   // the tile only loses whole chunks
                return amount;
            }

            LiquidStack tank = TankOf(source);
            if (tank == null || tank.IsEmpty || tank.LiquidType != liquidType) return 0f;

            float taken = Math.Min(amount, tank.Amount);
            tank.Amount -= taken;
            if (tank.Amount <= tankEmptyBelow)
            {
                tank.Amount = 0f;
                tank.LiquidType = -1;   // an empty tank forgets its liquid, so it can take another one
            }
            return taken;
        }

        void Deposit(int sinkIndex, int liquidType, float amount)
        {
            FlowSink sink = Sinks[sinkIndex];

            if (sink.IsWorld)
            {
                banks.AddDeposit(sink.WorldPos, liquidType, amount);
                return;
            }

            BaseMachine machine = sink.Machine;
            int resolved = machine.ResolveIncomingLiquid(liquidType);
            LiquidStack slot = LiquidTankRules.FindSinkSlot(machine.InputLiquids, resolved);
            if (slot == null) return;

            slot.LiquidType = resolved;
            slot.Amount = Math.Min(slot.Capacity, slot.Amount + amount);
        }

        float ComputeCap()
        {
            float cap = float.MaxValue;
            for (int i = 0; i < Nodes.Length; i++)
            {
                // A pipe always counts. A motor counts only while it is off (an ON motor is a pump, not a pipe).
                if (!Nodes[i].IsMotor || !Nodes[i].MotorOn)
                    cap = Math.Min(cap, PassRate[i]);
            }
            return cap;
        }
    }

    public static class FlowNetworkBuilder
    {
        // Groups every pipe (and the motors joined to them) into networks and builds each one's nodes and ports.
        // Input: the list of registered pipe tiles (LiquidNetworkSystem.AllPipeTiles).
        public static List<FlowNetwork> BuildAll(HashSet<Point> allPipes)
        {
            var result = new List<FlowNetwork>();
            var assigned = new HashSet<Point>();

            // Fixed seed order, so the same world always gives the same networks in the same order.
            foreach (Point seed in allPipes.OrderBy(p => p.X).ThenBy(p => p.Y))
            {
                if (assigned.Contains(seed) || !IsPipeAt(seed)) continue;

                var timer = Stopwatch.StartNew();
                FlowNetwork network = BuildOne(seed, allPipes, assigned);
                network.RefreshRoutes();
                timer.Stop();
                network.BuildMs = timer.Elapsed.TotalMilliseconds;

                result.Add(network);
            }

            return result;
        }

        static bool IsPipeAt(Point p)
        {
            if (!WorldGen.InWorld(p.X, p.Y)) return false;
            Tile tile = Main.tile[p.X, p.Y];
            return tile.HasTile && PipeTierRegistry.IsPipeTile(tile.TileType);
        }

        static bool TryGetMotor(Point p, out MotorTileEntityBase motor)
        {
            motor = null;
            if (!WorldGen.InWorld(p.X, p.Y)) return false;

            if (TileEntityHelper.TryGetEntityFromTile(p.X, p.Y, out TileEntity entity, out _) && entity is MotorTileEntityBase found)
            {
                motor = found;
                return true;
            }
            return false;
        }

        // A motor is only joined to the network along its axis (front and back). A pipe touching its side is NOT connected.
        static bool OnAxis(Direction facing, Direction moveDir) => moveDir == facing || moveDir == facing.Opposite();

        static FlowNetwork BuildOne(Point seed, HashSet<Point> allPipes, HashSet<Point> assigned)
        {
            // 1. Flood fill. members: tile -> the motor on it (null = pipe). Both ends of a link must accept it (FlowLinks rule).
            var members = new Dictionary<Point, MotorTileEntityBase> { [seed] = null };
            var queue = new Queue<Point>();
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                MotorTileEntityBase here = members[p];

                for (int d = 0; d < 4; d++)
                {
                    Direction dir = (Direction)d;
                    if (here != null && !OnAxis(here.Facing, dir)) continue;

                    Point q = p + dir.ToOffset();
                    if (members.ContainsKey(q) || !WorldGen.InWorld(q.X, q.Y)) continue;

                    if (IsPipeAt(q))
                    {
                        // Only registered pipes join, same as the old flood fill.
                        if (allPipes.Contains(q))
                        {
                            members[q] = null;
                            queue.Enqueue(q);
                        }
                        continue;
                    }

                    if (TryGetMotor(q, out MotorTileEntityBase motor) && OnAxis(motor.Facing, dir))
                    {
                        members[q] = motor;
                        queue.Enqueue(q);
                    }
                }
            }

            // 2. Nodes, sorted by tile position so node indices (and so every later order) never depend on hash order.
            Point[] points = members.Keys.OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
            foreach (Point p in points) assigned.Add(p);

            var network = new FlowNetwork
            {
                Positions = points,
                Nodes = new FlowNode[points.Length],
                Motors = new MotorTileEntityBase[points.Length],
                PassRate = new float[points.Length]
            };

            for (int i = 0; i < points.Length; i++)
            {
                Point p = points[i];
                MotorTileEntityBase motor = members[p];

                if (motor == null)
                {
                    network.Nodes[i] = new FlowNode { X = p.X, Y = p.Y };
                    network.PassRate[i] = PipeTierRegistry.MaxFlowRateByTileType.TryGetValue(Main.tile[p.X, p.Y].TileType, out float tier) ? tier : float.MaxValue;
                }
                else
                {
                    network.Nodes[i] = new FlowNode
                    {
                        X = p.X, Y = p.Y, IsMotor = true,
                        MotorOn = motor.isOn,
                        Facing = (FlowDir)(int)motor.Facing,
                        Strength = motor.PumpStrength
                    };
                    network.Motors[i] = motor;
                    network.PassRate[i] = motor.PipeEquivalentMaxFlowRate;
                }
            }

            // 3. Attach points and ports.
            var ports = new PortTables();
            for (int i = 0; i < points.Length; i++)
            {
                Point p = points[i];
                MotorTileEntityBase motorHere = network.Motors[i];

                bool openSidesKnown = false;
                bool openUp = false, openDown = false, openLeft = false, openRight = false;

                for (int d = 0; d < 4; d++)
                {
                    Direction dir = (Direction)d;
                    if (motorHere != null && !OnAxis(motorHere.Facing, dir)) continue;   // a motor has no mouth on its sides

                    Point q = p + dir.ToOffset();
                    if (!WorldGen.InWorld(q.X, q.Y)) continue;

                    Tile tile = Main.tile[q.X, q.Y];
                    if (tile.HasTile && PipeTierRegistry.IsPipeTile(tile.TileType)) continue;   // joined above (or an unregistered pipe, ignored like before)

                    if (TileEntityHelper.TryGetEntityFromTile(q.X, q.Y, out TileEntity entity, out _))
                    {
                        if (entity is MotorTileEntityBase) continue;   // a motor that did not join (pipe touching its side): not an attachment

                        if (entity is BaseMachine machine)
                        {
                            ports.AttachMachine(i, dir, machine);
                            continue;
                        }
                    }

                    // World liquid / open air: only on a side the pipe treats as an open mouth, and never into solid rock.
                    if (!openSidesKnown)
                    {
                        PipeConnectionHelper.GetOpenSides(p.X, p.Y, out openUp, out openDown, out openLeft, out openRight);
                        openSidesKnown = true;
                    }

                    bool isOpenSide = dir switch
                    {
                        Direction.Up => openUp,
                        Direction.Down => openDown,
                        Direction.Left => openLeft,
                        Direction.Right => openRight,
                        _ => false
                    };
                    if (!isOpenSide) continue;
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) continue;

                    ports.AttachWorld(i, dir, q);
                }
            }

            network.Sources = ports.Sources;
            network.Sinks = ports.Sinks;
            network.SourceAttach = ports.SourceAttach.ToArray();
            network.SinkAttach = ports.SinkAttach.ToArray();
            network.SourcePortsOfAttach = ports.SourcePortsOfAttach.ToArray();
            network.SinkPortsOfAttach = ports.SinkPortsOfAttach.ToArray();
            return network;
        }

        // Collapses every touching point of the same tank / machine / world cell into ONE port, so nothing is counted twice.
        sealed class PortTables
        {
            public List<FlowSource> Sources = new();
            public List<FlowSink> Sinks = new();
            public List<FlowPort> SourceAttach = new();
            public List<FlowPort> SinkAttach = new();
            public List<int[]> SourcePortsOfAttach = new();
            public List<int[]> SinkPortsOfAttach = new();

            readonly Dictionary<(BaseMachine, int), int> machineSourceIndex = new();
            readonly Dictionary<BaseMachine, int> machineSinkIndex = new();
            readonly Dictionary<Point, int> worldSourceIndex = new();
            readonly Dictionary<Point, int> worldSinkIndex = new();
            readonly Dictionary<BaseMachine, int> machineOwner = new();
            readonly Dictionary<Point, int> worldOwner = new();
            int nextOwner;

            int OwnerOf(BaseMachine machine)
            {
                if (!machineOwner.TryGetValue(machine, out int owner)) machineOwner[machine] = owner = nextOwner++;
                return owner;
            }

            int OwnerOf(Point cell)
            {
                if (!worldOwner.TryGetValue(cell, out int owner)) worldOwner[cell] = owner = nextOwner++;
                return owner;
            }

            public void AttachMachine(int node, Direction dir, BaseMachine machine)
            {
                int owner = OwnerOf(machine);
                FlowDir mouth = (FlowDir)(int)dir;

                LiquidStack[] outputs = machine.OutputLiquids;
                if (outputs.Length > 0)
                {
                    var sourcePorts = new int[outputs.Length];
                    for (int t = 0; t < outputs.Length; t++)
                    {
                        if (!machineSourceIndex.TryGetValue((machine, t), out int index))
                        {
                            index = Sources.Count;
                            Sources.Add(new FlowSource { Machine = machine, Tank = outputs[t], TankIndex = t, Owner = owner });
                            machineSourceIndex[(machine, t)] = index;
                        }
                        sourcePorts[t] = index;
                    }
                    SourceAttach.Add(new FlowPort { Node = node, Mouth = mouth, Owner = owner });
                    SourcePortsOfAttach.Add(sourcePorts);
                }

                if (machine.InputLiquids.Length > 0)
                {
                    if (!machineSinkIndex.TryGetValue(machine, out int index))
                    {
                        index = Sinks.Count;
                        Sinks.Add(new FlowSink { Machine = machine, Owner = owner });
                        machineSinkIndex[machine] = index;
                    }
                    SinkAttach.Add(new FlowPort { Node = node, Mouth = mouth, Owner = owner });
                    SinkPortsOfAttach.Add(new[] { index });
                }
            }

            public void AttachWorld(int node, Direction dir, Point cell)
            {
                int owner = OwnerOf(cell);
                FlowDir mouth = (FlowDir)(int)dir;

                if (!worldSourceIndex.TryGetValue(cell, out int sourceIndex))
                {
                    sourceIndex = Sources.Count;
                    Sources.Add(new FlowSource { WorldPos = cell, Owner = owner });
                    worldSourceIndex[cell] = sourceIndex;
                }
                SourceAttach.Add(new FlowPort { Node = node, Mouth = mouth, Owner = owner });
                SourcePortsOfAttach.Add(new[] { sourceIndex });

                if (!worldSinkIndex.TryGetValue(cell, out int sinkIndex))
                {
                    sinkIndex = Sinks.Count;
                    Sinks.Add(new FlowSink { WorldPos = cell, Owner = owner });
                    worldSinkIndex[cell] = sinkIndex;
                }
                SinkAttach.Add(new FlowPort { Node = node, Mouth = mouth, Owner = owner });
                SinkPortsOfAttach.Add(new[] { sinkIndex });
            }
        }
    }
}
