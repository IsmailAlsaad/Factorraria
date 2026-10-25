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
        public static bool networkNeedsRebuilding = true;

        // The pipe networks (nodes, ports, routes), rebuilt from AllPipeTiles whenever the topology changes. They move all the liquid.
        public static List<FlowNetwork> FlowNetworks = new();

        // Liquid overhaul step 3: the per-cell banks of world liquid, shared by every flow network (see LiquidWorldBanks).
        public static LiquidWorldBanks WorldBanks = new();

        // TEMPORARY (patch 8.1b, remove in Phase 10): how many times the networks were rebuilt, for the [Pipes] log line and /fpipes.
        public static int RebuildCount;

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (networkNeedsRebuilding)
            {
                HealPipeList();
                RebuildFlowNetworks();

                RebuildCount++;
                if (Main.netMode == NetmodeID.Server)
                    Mod.Logger.Info("[MP-Net] [Pipes] networks rebuilt: " + FlowNetworks.Count + " network(s), " + AllPipeTiles.Count + " pipe tile(s); " + global::Factorraria.Common.Net.LiquidPipeDiagnostics.Summary());
                networkNeedsRebuilding = false;
            }

            // The flow networks move all the liquid.
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
            FlowNetworks = new();
            WorldBanks.Clear();
            networkNeedsRebuilding = true;
            pipeListNeedsHeal = true;
        }

        public override void OnWorldUnload()
        {
            AllPipeTiles.Clear();
            FlowNetworks = new();
            WorldBanks.Clear();

            LiquidTextureCache.Clear();
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


        // Liquid overhaul. The flow networks are rebuilt from the registered pipes. First every whole unit still banked in a world cell
        // is applied (the networks are about to be replaced), then the new networks are built. An exception is logged and leaves
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
    }
}