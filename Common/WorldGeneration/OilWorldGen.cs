using System;
using System.Collections.Generic;
using Factorraria.Content.Liquids.Oil;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace Factorraria.Common.WorldGeneration
{
    // Underground Desert oil: one giant pool at the bottom of the biome, small sealed pockets above it that
    // thin out to zero at the top. Plan: Oil_ocean_plan.md.
    // PATCH 2 = the pool (4.4): pass insertion, bail-outs, desert mask, per-column limits, flat-surfaced pool at the bottom with sealing.
    // The pockets (4.5) come in patch 3.
    // Randomness (later patches): WorldGen.genRand only. Tunables are public instance fields (hot reload friendly),
    // read via ModContent.GetInstance<OilWorldGen>().
    public class OilWorldGen : ModSystem
    {
        public bool Enabled = true;
        public int PoolDepth = 14;                    // tiles of oil at the deepest point
        public int FloorThickness = 4;                // solid tiles kept under the pool
        public int RimMargin = 3;                     // solid desert kept between oil and the biome edge
        public int MinPoolDepthHere = 3;              // a column shallower than this gets no pool
        public double PocketAttemptsPerTile = 0.02;
        public double PocketExponent = 2.0;           // the k in d^k
        public double PocketMinDepthFraction = 0.08;  // no pockets above this, so the top is literally zero
        public double PocketRadiusXMin = 1.5;
        public double PocketRadiusXMax = 4.0;
        public double PocketRadiusYMin = 1.0;
        public double PocketRadiusYMax = 3.0;
        public int PocketMaxCells = 60;               // hard cap, must stay under 100
        public bool ExcludeHive = false;              // the hive rectangle covers nearly the whole biome (see the patch 1 logs): a rectangle test would reject almost every pocket. Patch 3 relies on the ring-solid test instead.

        public double PoolSurfacePercentile = 0.5;    // 0.5 = median of (bottom - floor thickness). Lower = wider pool, but deeper in the middle.
        public int FloorNoiseAmplitude = 2;           // +/- tiles of smooth noise on the pool floor
        public int FloorNoiseStep = 8;                // columns between noise knots
        public int ObstacleColumnSpread = 3;          // columns excluded on each side of a chest or furniture
        public int MaxSealIterations = 8;

        public int ScanMarginTop = 5;                 // scan this many rows above the rectangle
        public int ScanMarginBottom = 10;             // and this many below it
        public int MinColumnHeight = 20;              // a column shorter than this is invalid
        public int SmoothRadius = 2;                  // moving average of 2 * radius + 1 = 5 columns

        private readonly string[] anchorNames = { "Settle Liquids Again", "Settle Liquids", "Tile Cleanup", "Final Cleanup" };
        private string usedAnchor = "none";

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            usedAnchor = "none";

            for (int a = 0; a < anchorNames.Length; a++)
            {
                int index = tasks.FindIndex(t => t.Name.Equals(anchorNames[a]));
                if (index == -1)
                    continue;

                // The two settle passes: run right after them so nothing disturbs our liquid afterwards.
                // The cleanup fallbacks: run right before them.
                int insertAt = a < 2 ? index + 1 : index;
                tasks.Insert(insertAt, new PassLegacy("Factorraria Underground Desert Oil", GenerateOil, 30));
                usedAnchor = anchorNames[a];
                Mod.Logger.Info("[OilGen] pass inserted, anchor=" + usedAnchor);
                return;
            }

            Mod.Logger.Warn("[OilGen] none of the anchor passes was found, no oil will be generated");
        }

        private void GenerateOil(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Pooling oil in the desert";

            // ---- Step A: bail-outs ----
            if (!Enabled)
                return;

            Rectangle r = GenVars.UndergroundDesertLocation;
            if (r.Width <= 0 || r.Height <= 0)
            {
                Mod.Logger.Warn("[OilGen] Underground Desert rectangle is empty (" + r + "), skipping");
                return;
            }

            int oilType = ModLiquidLib.ModLoader.LiquidLoader.LiquidType<OilLiquid>();
            if (oilType <= 0)
            {
                Mod.Logger.Warn("[OilGen] oil liquid type is " + oilType + ", skipping");
                return;
            }

            progress.Set(0.1f);

            // ---- Step B: desert mask and per-column limits ----
            int width = r.Width;
            int[] top = new int[width];
            int[] bottom = new int[width];
            bool[] valid = new bool[width];

            long tileOnly = 0, wallOnly = 0, both = 0;

            int scanTop = r.Top - ScanMarginTop;
            int scanBottom = r.Bottom + ScanMarginBottom;

            for (int i = 0; i < width; i++)
            {
                int x = r.Left + i;
                int first = -1;
                int last = -1;

                for (int y = scanTop; y <= scanBottom; y++)
                {
                    if (!IsDesertCell(x, y, out bool byTile, out bool byWall))
                        continue;

                    if (byTile && byWall) both++;
                    else if (byTile) tileOnly++;
                    else wallOnly++;

                    if (first == -1)
                        first = y;
                    last = y;
                }

                top[i] = first;
                bottom[i] = last;
                valid[i] = first != -1 && last - first >= MinColumnHeight;
            }

            progress.Set(0.5f);

            // Smooth top and bottom over valid neighbours so single-tile spikes do not make jagged pools.
            int[] smoothTop = new int[width];
            int[] smoothBottom = new int[width];
            for (int i = 0; i < width; i++)
            {
                if (!valid[i])
                    continue;

                long sumTop = 0, sumBottom = 0;
                int count = 0;
                for (int k = i - SmoothRadius; k <= i + SmoothRadius; k++)
                {
                    if (k < 0 || k >= width || !valid[k])
                        continue;
                    sumTop += top[k];
                    sumBottom += bottom[k];
                    count++;
                }

                smoothTop[i] = (int)Math.Round(sumTop / (double)count);
                smoothBottom[i] = (int)Math.Round(sumBottom / (double)count);
            }

            progress.Set(0.9f);

            // ---- Diagnostic log (temporary, remove in Phase 10) ----
            List<int> topValues = new List<int>();
            List<int> bottomValues = new List<int>();
            for (int i = 0; i < width; i++)
            {
                if (!valid[i])
                    continue;
                topValues.Add(smoothTop[i]);
                bottomValues.Add(smoothBottom[i]);
            }

            string columns = "valid columns=" + topValues.Count + "/" + width;
            string stats = topValues.Count == 0
                ? "top/bottom: no valid column"
                : "top min/med/max=" + Describe(topValues) + ", bottom min/med/max=" + Describe(bottomValues);

            Mod.Logger.Info("[OilGen] anchor=" + usedAnchor
                + ", world=" + Main.maxTilesX + "x" + Main.maxTilesY
                + ", desert rect=" + r
                + ", hive rect=" + GenVars.UndergroundDesertHiveLocation
                + ", oilType=" + oilType
                + ", " + columns
                + ", " + stats
                + ", cells by tile only/wall only/both=" + tileOnly + "/" + wallOnly + "/" + both
                + " (patch 2: carving the pool)");

            GeneratePool(progress, r, oilType, valid, smoothBottom);
            progress.Set(1f);
        }

        // ---- Step C: the pool (patch 2) ----
        private void GeneratePool(GenerationProgress progress, Rectangle r, int oilType, bool[] valid, int[] bottom)
        {
            int width = r.Width;

            // 1. One flat surface level for the whole pool (a liquid surface must be level).
            List<int> floors = new List<int>();
            for (int i = 0; i < width; i++)
            {
                if (valid[i])
                    floors.Add(bottom[i] - FloorThickness);
            }
            if (floors.Count == 0)
                return;
            floors.Sort();
            int pick = Math.Clamp((int)(floors.Count * PoolSurfacePercentile), 0, floors.Count - 1);
            int poolSurfaceY = floors[pick] - PoolDepth;

            // 2. Per-column floor: biome bottom minus the solid layer, plus smooth 1D value noise (not white noise).
            int[] floor = new int[width];
            int step = Math.Max(1, FloorNoiseStep);
            int[] knots = new int[width / step + 2];
            for (int k = 0; k < knots.Length; k++)
                knots[k] = WorldGen.genRand.Next(-FloorNoiseAmplitude, FloorNoiseAmplitude + 1);

            int maxFloor = int.MinValue;
            for (int i = 0; i < width; i++)
            {
                double f = (i % step) / (double)step;
                double noise = knots[i / step] * (1 - f) + knots[i / step + 1] * f;
                floor[i] = bottom[i] - FloorThickness + (int)Math.Round(noise);
                if (valid[i] && floor[i] > maxFloor)
                    maxFloor = floor[i];
            }

            int rows = maxFloor - poolSurfaceY + 1;
            if (rows <= 0)
            {
                Mod.Logger.Warn("[OilGen] pool: poolSurfaceY=" + poolSurfaceY + " is below every floor, no pool");
                return;
            }

            progress.Set(0.2f);

            bool[,] inR = new bool[width, rows];
            bool[] excluded = new bool[width];

            bool InR(int x, int y)
            {
                int i = x - r.Left;
                int j = y - poolSurfaceY;
                return i >= 0 && i < width && j >= 0 && j < rows && inR[i, j];
            }

            void ExcludeAround(int i)
            {
                for (int k = i - ObstacleColumnSpread; k <= i + ObstacleColumnSpread; k++)
                {
                    if (k >= 0 && k < width)
                        excluded[k] = true;
                }
            }

            // 3. Candidate region R. Returns true when an obstacle forced new column exclusions.
            bool BuildRegion()
            {
                Array.Clear(inR, 0, inR.Length);
                bool changed = false;

                for (int i = 0; i < width; i++)
                {
                    if (!valid[i] || excluded[i] || floor[i] - poolSurfaceY < MinPoolDepthHere)
                        continue;

                    int x = r.Left + i;
                    for (int y = poolSurfaceY; y <= floor[i]; y++)
                    {
                        if (!WorldGen.InWorld(x, y, 10))
                            continue;

                        // Rim test: stay RimMargin tiles away from the biome edge.
                        if (!IsDesertCell(x - RimMargin, y, out _, out _) || !IsDesertCell(x + RimMargin, y, out _, out _))
                            continue;

                        Tile tile = Main.tile[x, y];
                        if (IsObstacle(tile))
                        {
                            ExcludeAround(i);
                            changed = true;
                            break;
                        }

                        if (IsClearable(tile))
                            inR[i, y - poolSurfaceY] = true;
                    }
                }

                return changed;
            }

            // The ring: 4-neighbours of R that are not in R, except the cell above each top-row cell (the free surface).
            List<Point> BuildRing()
            {
                HashSet<Point> ring = new HashSet<Point>();
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < rows; j++)
                    {
                        if (!inR[i, j])
                            continue;

                        int x = r.Left + i;
                        int y = poolSurfaceY + j;
                        if (j != 0 && !InR(x, y - 1)) ring.Add(new Point(x, y - 1));
                        if (!InR(x, y + 1)) ring.Add(new Point(x, y + 1));
                        if (!InR(x - 1, y)) ring.Add(new Point(x - 1, y));
                        if (!InR(x + 1, y)) ring.Add(new Point(x + 1, y));
                    }
                }
                return new List<Point>(ring);
            }

            // 4 + 5. Obstacles, then seal check and plugging, repeated until stable.
            int plugged = 0;
            int iterations = 0;
            bool converged = false;
            List<Point> ringCells = null;

            for (; iterations < MaxSealIterations; iterations++)
            {
                if (BuildRegion())
                    continue;

                ringCells = BuildRing();
                bool again = false;

                foreach (Point p in ringCells)
                {
                    if (!WorldGen.InWorld(p.X, p.Y, 10))
                    {
                        ExcludeNeighbourColumns(p);
                        again = true;
                        continue;
                    }

                    Tile tile = Main.tile[p.X, p.Y];
                    if (IsObstacle(tile))
                    {
                        ExcludeAround(p.X - r.Left);
                        again = true;
                        continue;
                    }

                    if (IsSolidBlock(tile))
                        continue;

                    // Breach: plug it with Sandstone.
                    tile.LiquidAmount = 0;
                    WorldGen.PlaceTile(p.X, p.Y, TileID.Sandstone, true, true);
                    plugged++;

                    if (!IsSolidBlock(Main.tile[p.X, p.Y]))
                    {
                        ExcludeNeighbourColumns(p);
                        again = true;
                    }
                }

                if (!again)
                {
                    converged = true;
                    break;
                }
            }

            void ExcludeNeighbourColumns(Point cell)
            {
                if (InR(cell.X, cell.Y - 1) || InR(cell.X, cell.Y + 1))
                    excluded[Math.Clamp(cell.X - r.Left, 0, width - 1)] = true;
                if (InR(cell.X - 1, cell.Y)) excluded[Math.Clamp(cell.X - 1 - r.Left, 0, width - 1)] = true;
                if (InR(cell.X + 1, cell.Y)) excluded[Math.Clamp(cell.X + 1 - r.Left, 0, width - 1)] = true;
            }

            if (!converged)
            {
                Mod.Logger.Warn("[OilGen] pool: could not seal the pool in " + MaxSealIterations + " passes, nothing carved (plugged=" + plugged + ")");
                return;
            }

            progress.Set(0.7f);

            // 6. Commit. Sand directly above oil is turned into Sandstone first (falling sand would otherwise drop into the pool).
            int cells = 0;
            int sandConverted = 0;
            int columnsWithOil = 0;
            int maxDepth = 0;

            for (int i = 0; i < width; i++)
            {
                int depth = 0;
                int x = r.Left + i;

                for (int j = 0; j < rows; j++)
                {
                    if (!inR[i, j])
                        continue;

                    int y = poolSurfaceY + j;

                    if (!InR(x, y - 1))
                    {
                        Tile above = Main.tile[x, y - 1];
                        if (above.HasTile && Main.tileSand[above.TileType])
                        {
                            above.TileType = TileID.Sandstone;
                            sandConverted++;
                        }
                    }

                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && !IsDesertBlock(tile))
                        WorldGen.KillTile(x, y, false, false, true);   // pots, torches, plants: removes whole objects

                    tile = Main.tile[x, y];
                    tile.HasTile = false;
                    tile.LiquidType = oilType;
                    tile.LiquidAmount = byte.MaxValue;

                    cells++;
                    depth++;
                }

                if (depth > 0)
                    columnsWithOil++;
                if (depth > maxDepth)
                    maxDepth = depth;
            }

            // 7. Re-frame the block edges around the pool.
            foreach (Point p in ringCells)
            {
                if (WorldGen.InWorld(p.X, p.Y, 10))
                    WorldGen.SquareTileFrame(p.X, p.Y, true);
            }

            int excludedCount = 0;
            for (int i = 0; i < width; i++)
            {
                if (excluded[i])
                    excludedCount++;
            }

            Mod.Logger.Info("[OilGen] pool: poolSurfaceY=" + poolSurfaceY + ", pool cells=" + cells
                + ", columns with oil=" + columnsWithOil + "/" + width + ", max depth=" + maxDepth
                + ", plugged=" + plugged + ", sand ceilings converted=" + sandConverted
                + ", excluded columns=" + excludedCount + ", seal passes=" + (iterations + 1));

            if (cells <= 100)
                Mod.Logger.Warn("[OilGen] pool has only " + cells + " cells: it will NOT act as an infinite source (needs more than 100)");
            else if (cells < 400)
                Mod.Logger.Warn("[OilGen] pool is small (" + cells + " cells), tiny desert?");
        }

        private static bool IsSolidBlock(Tile tile)
        {
            return tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType] && !tile.IsActuated;
        }

        private static bool IsDesertBlock(Tile tile)
        {
            if (!tile.HasTile)
                return false;
            int type = tile.TileType;
            return TileID.Sets.Conversion.Sand[type] || TileID.Sets.Conversion.HardenedSand[type] || TileID.Sets.Conversion.Sandstone[type];
        }

        // Multi-tile or entity-like non-solid things (chests, furniture, statues...) are never destroyed.
        // Pots and torches are the decorations we allow to be cleared.
        private static bool IsObstacle(Tile tile)
        {
            if (!tile.HasTile)
                return false;
            int type = tile.TileType;
            return Main.tileFrameImportant[type] && !Main.tileSolid[type] && type != TileID.Pots && type != TileID.Torches;
        }

        // Air, desert blocks, pots, torches and plain non-solid plants can be carved. Everything else stays and acts as a wall.
        private static bool IsClearable(Tile tile)
        {
            if (!tile.HasTile)
                return true;
            int type = tile.TileType;
            if (IsDesertBlock(tile) || type == TileID.Pots || type == TileID.Torches)
                return true;
            return !Main.tileSolid[type] && !Main.tileFrameImportant[type];
        }
        // True when the cell is in the world and is a desert block (any sand, hardened sand or sandstone tile)
        // or carries a desert wall. Walls are used too: carved caves keep their walls, so they identify the biome
        // even where the tile is air.
        private bool IsDesertCell(int x, int y, out bool byTile, out bool byWall)
        {
            byTile = false;
            byWall = false;

            if (!WorldGen.InWorld(x, y, 10))
                return false;

            Tile tile = Main.tile[x, y];

            if (tile.HasTile)
            {
                int type = tile.TileType;
                byTile = TileID.Sets.Conversion.Sand[type]
                      || TileID.Sets.Conversion.HardenedSand[type]
                      || TileID.Sets.Conversion.Sandstone[type];
            }

            byWall = WallID.Sets.Conversion.Sandstone[tile.WallType];

            return byTile || byWall;
        }

        private static string Describe(List<int> values)
        {
            int[] sorted = values.ToArray();
            Array.Sort(sorted);
            return sorted[0] + "/" + sorted[sorted.Length / 2] + "/" + sorted[sorted.Length - 1];
        }
    }
}