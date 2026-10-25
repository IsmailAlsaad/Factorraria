using System.Collections.Generic;
using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // R9 fix (2026-10-25). The server learns about client-built pipes and machines only through requests (TilePlaced, entity
    // request 87). The server's /fflow showed a client-built network with missing pipes and no motor, while the saved world had
    // them all (a reload's heal scan found them), so some requests never registered anything. This sweep is the safety net: once a
    // second, server only, it looks at the tiles around every player and registers what the requests missed:
    //  - a pipe tile that is not in AllPipeTiles is registered (rebuild flag set by RegisterPipeTile);
    //  - a machine tile with no entity gets one (MachinePlacement.EnsureEntity: power grid + liquid registration + share).
    // A tile must be seen missing in two sweeps in a row before it is acted on, so a request that is merely in flight wins.
    public class LiquidServerSweepSystem : ModSystem
    {
        public int SweepEveryTicks = 60;
        public int SweepRadiusTiles = 110;

        HashSet<Point> pendingPipes = new();
        HashSet<Point> pendingMachines = new();

        public override void OnWorldLoad()
        {
            pendingPipes.Clear();
            pendingMachines.Clear();
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode != NetmodeID.Server || Main.GameUpdateCount % (uint)SweepEveryTicks != 0)
                return;

            var seenPipes = new HashSet<Point>();
            var seenMachines = new HashSet<Point>();
            var doneAreas = new List<Rectangle>();
            int addedPipes = 0, madeEntities = 0;

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (player == null || !player.active)
                    continue;

                int cx = (int)(player.Center.X / 16f);
                int cy = (int)(player.Center.Y / 16f);
                int minX = System.Math.Max(2, cx - SweepRadiusTiles);
                int maxX = System.Math.Min(Main.maxTilesX - 3, cx + SweepRadiusTiles);
                int minY = System.Math.Max(2, cy - SweepRadiusTiles);
                int maxY = System.Math.Min(Main.maxTilesY - 3, cy + SweepRadiusTiles);

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        Tile tile = Main.tile[x, y];
                        if (!tile.HasTile)
                            continue;

                        int type = tile.TileType;
                        if (PipeTierRegistry.IsPipeTile(type))
                        {
                            var pos = new Point(x, y);
                            if (Systems.LiquidNetworkSystem.AllPipeTiles.Contains(pos))
                                continue;

                            seenPipes.Add(pos);
                            if (pendingPipes.Contains(pos))
                            {
                                Systems.LiquidNetworkSystem.RegisterPipeTile(pos);
                                addedPipes++;
                            }
                        }
                        else if (MachineVisualRegistry.Definitions.ContainsKey(type))
                        {
                            if (TileEntityHelper.TryGetEntityFromTile(x, y, out _, out _))
                                continue;

                            var pos = new Point(x, y);
                            seenMachines.Add(pos);
                            if (pendingMachines.Contains(pos) && MachinePlacement.EnsureEntity(x, y) != null)
                                madeEntities++;
                        }
                    }
                }
            }

            // Whatever was just acted on is no longer missing; the rest waits for the next sweep.
            pendingPipes = seenPipes;
            pendingMachines = seenMachines;

            if (addedPipes > 0 || madeEntities > 0)
                FactorrariaNet.Log("[Sweep] R9 safety net registered " + addedPipes + " pipe tile(s) and created " + madeEntities + " machine entity(ies) that the client requests had missed");
        }
    }
}