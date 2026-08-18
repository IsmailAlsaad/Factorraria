using Factorraria.Common.Interfaces;
using Factorraria.Common.PowerGrid;
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

        public override void PostUpdateWorld()
        {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
                return;

            if (gridNeedsRebuilding)
            {
                if (AllMachines.Count == 0)
                {
                    InitializeMachinesList();
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
        }

        PowerNetwork BuildNetworkFromMachine(TileEntity startMachine, HashSet<TileEntity> remainingMachines)
        {
            PowerNetwork network = new PowerNetwork();
            Queue<(Point pos, CustomWireType type)> wireQueue = new Queue<(Point, CustomWireType)>();
            HashSet<(Point pos, CustomWireType type)> visitedWiresInThisNetwork = new HashSet<(Point, CustomWireType)>();

            // 1. Register startMachine to this network
            RegisterMachineToNetwork(startMachine, network, remainingMachines);

            // 2. Enqueue ALL wire types touching this machine's footprint
            EnqueueMachineWires(startMachine, wireQueue, visitedWiresInThisNetwork);

            Point[] offsets = new Point[] { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) };

            // 3. Flood fill outward through wires
            while (wireQueue.Count > 0)
            {
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
                            }
                        }
                    }
                }
            }

            return network;
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