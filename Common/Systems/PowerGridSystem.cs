
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Common.PowerGrid;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Systems
{
    public class PowerGridSystem : ModSystem
    {
        public static HashSet<TileEntity> AllMachines = new HashSet<TileEntity>();
        public static List<PowerNetwork> ActivePowerNetworks = new List<PowerNetwork>();
        public static bool gridNeedsRebuilding = true;

        // Patch 5.4: saved machines are indexed once per world. The old check ("when the machine list is empty") was skipped whenever a
        // machine had registered itself first (the 4.5 bootstrap scan, an early client placement), which left every saved machine out of the grids.
        bool machinesIndexed;

        public override void PostUpdateWorld()
        {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
                return;

            if (gridNeedsRebuilding)
            {
                if (!machinesIndexed)
                {
                    InitializeMachinesList();
                    machinesIndexed = true;
                }

                RebuildNetworks();
                gridNeedsRebuilding = false;
            }

            for (int i = 0; i < ActivePowerNetworks.Count; i++)
            {
                ActivePowerNetworks[i].Tick();
            }
        }

        void InitializeMachinesList()
        {
            foreach (TileEntity te in TileEntity.ByPosition.Values)
            {
                if (te is IElectricProducer || te is IElectricConsumer)
                {
                    AllMachines.Add(te);
                }
            }
        }

        public override void OnWorldLoad()
        {
            AllMachines.Clear();
            ActivePowerNetworks.Clear();
            gridNeedsRebuilding = true;
            machinesIndexed = false;
        }

        public override void OnWorldUnload()
        {
            AllMachines.Clear();
            ActivePowerNetworks.Clear();
        }

        void RebuildNetworks()
        {
            ActivePowerNetworks.Clear();

            HashSet<TileEntity> remainingMachines = new HashSet<TileEntity>(AllMachines);

            while (remainingMachines.Count > 0)
            {
                TileEntity startMachine = remainingMachines.First();
                PowerNetwork network = BuildNetworkFromMachine(startMachine, remainingMachines);

                if (network.electricProducers.Count > 0 || network.electricConsumers.Count > 0)
                {
                    ActivePowerNetworks.Add(network);
                }
            }

            // Test aid (temporary, patch 5.4): the server's view of the grids after every rebuild.
            if (Main.netMode == Terraria.ID.NetmodeID.Server)
            {
                int producers = 0;
                int consumers = 0;
                foreach (PowerNetwork built in ActivePowerNetworks)
                {
                    producers += built.electricProducers.Count;
                    consumers += built.electricConsumers.Count;
                }

                FactorrariaNet.Log("[Power] rebuilt: " + AllMachines.Count + " electric machine(s), " + ActivePowerNetworks.Count + " network(s), " + producers + " producer(s), " + consumers + " consumer(s)");
            }
        }

        PowerNetwork BuildNetworkFromMachine(TileEntity startMachine, HashSet<TileEntity> remainingMachines)
        {
            PowerNetwork network = new PowerNetwork();
            Queue<(Point pos, CustomWireType type)> wireQueue = new Queue<(Point, CustomWireType)>();
            HashSet<(Point pos, CustomWireType type)> visitedWiresInThisNetwork = new HashSet<(Point, CustomWireType)>();
            Queue<Point16> linkedPoleQueue = new Queue<Point16>();   // power poles patch P4: poles that a registered pole's links lead to

            // 1. Register startMachine to this network
            RegisterMachineToNetwork(startMachine, network, remainingMachines);

            // 2. Enqueue ALL wire types touching this machine's footprint
            EnqueueMachineWires(startMachine, wireQueue, visitedWiresInThisNetwork);
            EnqueueLinkedPoles(startMachine, linkedPoleQueue);

            Point[] offsets = new Point[] { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) };

            // 3. Flood fill outward through wires. A registered pole also pulls in the poles it is linked to (power poles patch P4):
            //    each of those registers, enqueues its own footprint wires and its own links, so wire networks at both ends merge
            //    and a chain of poles works.
            while (wireQueue.Count > 0 || linkedPoleQueue.Count > 0)
            {
                DrainLinkedPoles(linkedPoleQueue, network, remainingMachines, wireQueue, visitedWiresInThisNetwork);
                if (wireQueue.Count == 0)
                    continue;   // nothing left to walk: the loop ends (the linked pole queue was just emptied)

                var (currentPos, currentType) = wireQueue.Dequeue();

                foreach (Point offset in offsets)
                {
                    Point neighborPos = new Point(currentPos.X + offset.X, currentPos.Y + offset.Y);
                    var neighborNode = (neighborPos, currentType);

                    // Only propagate along the SAME wire type in mid-air
                    if (CustomWireSystem.HasWire(neighborPos.X, neighborPos.Y, currentType) && visitedWiresInThisNetwork.Add(neighborNode))
                    {
                        wireQueue.Enqueue(neighborNode);

                        // Check if an unassigned machine sits on this wire tile
                        if (TileEntityHelper.TryGetEntityFromTile(neighborPos.X, neighborPos.Y, out TileEntity foundMachine, out _))
                        {
                            if (remainingMachines.Contains(foundMachine))
                            {
                                RegisterMachineToNetwork(foundMachine, network, remainingMachines);

                                // Bridge: Machine enqueues ALL wire types touching its footprint
                                EnqueueMachineWires(foundMachine, wireQueue, visitedWiresInThisNetwork);
                                EnqueueLinkedPoles(foundMachine, linkedPoleQueue);
                            }
                        }
                    }
                }
            }

            return network;
        }

        // Power poles patch P4. If this machine is a pole, queue the position of every pole its links lead to. Positions only: the
        // entity at a position may not exist on this process (a pole the bootstrap scan has not created yet, or a stale link), so
        // DrainLinkedPoles looks each one up and skips what is missing (verification V8).
        void EnqueueLinkedPoles(TileEntity machine, Queue<Point16> linkedPoleQueue)
        {
            // A pole switched off by a vanilla wire pulse (WireToggle, the electricity icon) does not conduct through its hanging wires. Its
            // ground wires on the footprint still connect, like any other machine's.
            if (machine is not PowerPoleTileEntityBase pole || pole.WireToggle)
                return;

            foreach (Point16 other in PoleLinkSystem.Instance.GetLinkedPoles(machine.Position))
                linkedPoleQueue.Enqueue(other);
        }

        // Registers every queued linked pole that is still unassigned, with its footprint wires and its own links. A position that holds no
        // pole entity is skipped on purpose: a link must never bridge power into some other machine that was built where a pole used to be.
        // A pole that is not in remainingMachines is already in a grid (this one, since links are two-way), so it is skipped too.
        void DrainLinkedPoles(
            Queue<Point16> linkedPoleQueue,
            PowerNetwork network,
            HashSet<TileEntity> remainingMachines,
            Queue<(Point pos, CustomWireType type)> wireQueue,
            HashSet<(Point pos, CustomWireType type)> visitedWires)
        {
            while (linkedPoleQueue.Count > 0)
            {
                Point16 polePosition = linkedPoleQueue.Dequeue();

                if (!TileEntity.ByPosition.TryGetValue(polePosition, out TileEntity linkedPole) || linkedPole is not PowerPoleTileEntityBase)
                    continue;

                if (!remainingMachines.Contains(linkedPole))
                    continue;

                // The far end is switched off too: the hanging wire does not conduct into it (it can still join a grid through its ground wires).
                if (linkedPole is PowerPoleTileEntityBase { WireToggle: true })
                    continue;

                RegisterMachineToNetwork(linkedPole, network, remainingMachines);
                EnqueueMachineWires(linkedPole, wireQueue, visitedWires);
                EnqueueLinkedPoles(linkedPole, linkedPoleQueue);
            }
        }

        void RegisterMachineToNetwork(TileEntity machine, PowerNetwork network, HashSet<TileEntity> remainingMachines)
        {
            if (machine is IElectricConsumer consumer) { network.electricConsumers.Add(consumer); }
            if (machine is IElectricProducer producer) { network.electricProducers.Add(producer); }

            remainingMachines.Remove(machine);
        }

        public static void RegisterMachineToMasterList(TileEntity newMachine)
        {
            if (newMachine is IElectricConsumer || newMachine is IElectricProducer)
            {
                AllMachines.Add(newMachine);
                gridNeedsRebuilding = true;
            }
        }

        private void EnqueueMachineWires(
            TileEntity te,
            Queue<(Point pos, CustomWireType type)> wireQueue,
            HashSet<(Point pos, CustomWireType type)> visitedWires)
        {
            Tile tile = Main.tile[te.Position.X, te.Position.Y];
            TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);

            int width = data != null ? data.Width : 1;
            int height = data != null ? data.Height : 1;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Point tilePos = new Point(te.Position.X + x, te.Position.Y + y);

                    foreach (var wireType in CustomWireSystem.AllWireTypes)
                    {
                        if (CustomWireSystem.HasWire(tilePos.X, tilePos.Y, wireType))
                        {
                            var node = (tilePos, wireType);
                            if (visitedWires.Add(node))
                            {
                                wireQueue.Enqueue(node);
                            }
                        }
                    }
                }
            }
        }
    }
}