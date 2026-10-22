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
    // PATCH 2b = the oval: the desert is modelled as an ellipse; a thick Sandstone shell goes just inside its edge and the bottom 10% is filled with oil.
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

        public double PoolFraction = 0.10;            // the pool fills the bottom 10% of the oval's height
        public int ShellThickness = 8;                // thick Sandstone band just inside the oval edge
        public double ShellStartFraction = 0.75;      // the shell starts this far below the top of the oval (keeps it off the surface)
        public int OvalInset = 0;                     // shrink the ellipse by this many tiles on every side

        public int ScanMarginTop = 5;                 // scan this many rows above the rectangle
        public int ScanMarginBottom = 10;             // and this many below it
        public int MinColumnHeight = 20;              // a column shorter than this is invalid
        public int SmoothRadius = 2;                  // moving average of 2 * radius + 1 = 5 columns

        private readonly string[] anchorNames = { "Settle Liquids Again", "Settle Liquids", "Tile Cleanup", "Final Cleanup" };
        private string usedAnchor = "none";
        private Rectangle checkArea;                  // pool bounding box, for the liquid readback
        private Rectangle checkAbove;                 // oval interior above the pool

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
                checkArea = Rectangle.Empty;
                checkAbove = Rectangle.Empty;
                tasks.Add(new PassLegacy("Factorraria Oil Check", CheckOil, 1));   // runs after every other pass
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

            GenerateOval(progress, r, oilType);
            progress.Set(1f);
        }

        // ---- Step C (patch 2b): the oval ----
        // The desert is a vertically elongated oval. Model it as an ellipse inscribed in the desert rectangle, then:
        //  - shell: a thick Sandstone band just inside the ellipse edge (like the shimmer's stone oval), from ShellStartFraction down;
        //  - pool: everything inside the shell and in the bottom PoolFraction of the oval is cleared and filled with oil.
        private void GenerateOval(GenerationProgress progress, Rectangle r, int oilType)
        {
            double cx = r.X + r.Width / 2.0;
            double cy = r.Y + r.Height / 2.0;
            double a = r.Width / 2.0 - OvalInset;
            double b = r.Height / 2.0 - OvalInset;
            double ia = a - ShellThickness;
            double ib = b - ShellThickness;

            if (ia <= 4 || ib <= 4)
            {
                Mod.Logger.Warn("[OilGen] oval too small for a " + ShellThickness + " tile shell (a=" + a + ", b=" + b + "), skipping");
                return;
            }

            int poolTopY = r.Bottom - (int)Math.Round(PoolFraction * r.Height);
            int shellTopY = r.Top + (int)Math.Round(ShellStartFraction * r.Height);
            int yMin = shellTopY;
            int yMax = r.Bottom;

            // Pass 1: classify every cell. 0 = untouched, 1 = shell, 2 = pool.
            int w = r.Width;
            int h = yMax - yMin + 1;
            byte[,] kind = new byte[w, h];
            int shellCells = 0;
            int poolCells = 0;

            for (int i = 0; i < w; i++)
            {
                int x = r.Left + i;
                double dx = (x + 0.5 - cx);
                for (int j = 0; j < h; j++)
                {
                    int y = yMin + j;
                    if (!WorldGen.InWorld(x, y, 10))
                        continue;

                    double dy = (y + 0.5 - cy);
                    double outer = dx * dx / (a * a) + dy * dy / (b * b);
                    if (outer > 1.0)
                        continue;

                    double inner = dx * dx / (ia * ia) + dy * dy / (ib * ib);
                    if (inner > 1.0)
                    {
                        kind[i, j] = 1;
                        shellCells++;
                    }
                    else if (y >= poolTopY)
                    {
                        kind[i, j] = 2;
                        poolCells++;
                    }
                }
            }

            progress.Set(0.3f);

            // Pass 2: remove non-solid objects (pots, torches, chests, plants...) in the whole footprint, as whole objects,
            // so no half-destroyed multi-tile object is left behind.
            int objectsKilled = 0;
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    if (kind[i, j] == 0)
                        continue;

                    int x = r.Left + i;
                    int y = yMin + j;
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && !Main.tileSolid[tile.TileType])
                    {
                        WorldGen.KillTile(x, y, false, false, true);
                        objectsKilled++;
                    }
                }
            }

            progress.Set(0.5f);

            // Pass 3: write the shell and the pool.
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    if (kind[i, j] == 0)
                        continue;

                    int x = r.Left + i;
                    int y = yMin + j;
                    Tile tile = Main.tile[x, y];

                    if (kind[i, j] == 1)
                    {
                        tile.LiquidAmount = 0;
                        tile.HasTile = true;
                        tile.TileType = TileID.Sandstone;
                        tile.IsActuated = false;
                        tile.IsHalfBlock = false;
                        tile.Slope = SlopeType.Solid;
                    }
                    else
                    {
                        tile.HasTile = false;
                        tile.LiquidType = oilType;
                        tile.LiquidAmount = byte.MaxValue;
                    }
                }
            }

            progress.Set(0.75f);

            // Pass 4: any Sand block sitting directly above the oil would fall into it, so turn it into Sandstone.
            int sandConverted = 0;
            for (int i = 0; i < w; i++)
            {
                for (int j = 1; j < h; j++)
                {
                    if (kind[i, j] != 2 || kind[i, j - 1] == 2)
                        continue;

                    Tile above = Main.tile[r.Left + i, yMin + j - 1];
                    if (above.HasTile && Main.tileSand[above.TileType])
                    {
                        above.TileType = TileID.Sandstone;
                        sandConverted++;
                    }
                }
            }

            // Pass 5: re-frame every changed block so the Sandstone connects visually.
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    if (kind[i, j] == 1)
                        WorldGen.SquareTileFrame(r.Left + i, yMin + j, true);
                }
            }

            Mod.Logger.Info("[OilGen] oval: center=(" + (int)cx + "," + (int)cy + "), semi-axes=" + (int)a + "x" + (int)b
                + ", shell thickness=" + ShellThickness + ", shell from y=" + shellTopY + ", pool top y=" + poolTopY + ", pool bottom y=" + yMax
                + ", shell cells=" + shellCells + ", pool cells=" + poolCells
                + ", objects removed=" + objectsKilled + ", sand ceilings converted=" + sandConverted);

            if (poolCells <= 100)
                Mod.Logger.Warn("[OilGen] pool has only " + poolCells + " cells: it will NOT act as an infinite source (needs more than 100)");

            checkArea = new Rectangle(r.Left, poolTopY, r.Width, yMax - poolTopY + 1);
            checkAbove = new Rectangle(r.Left, shellTopY, r.Width, Math.Max(0, poolTopY - shellTopY));
            Mod.Logger.Info("[OilGen] readback right after carving (oilType=" + oilType + "): pool box [" + CountLiquids(checkArea, oilType)
                + "], above pool [" + CountLiquids(checkAbove, oilType) + "]");
        }

        // Runs after every other worldgen pass: shows whether a later pass changed the liquid we wrote.
        private void CheckOil(GenerationProgress progress, GameConfiguration configuration)
        {
            if (checkArea.Width <= 0)
                return;

            int oilType = ModLiquidLib.ModLoader.LiquidLoader.LiquidType<OilLiquid>();
            Tile sample = Main.tile[checkArea.Center.X, checkArea.Bottom - 10];
            Mod.Logger.Info("[OilGen] readback at END of worldgen (oilType=" + oilType + "): pool box [" + CountLiquids(checkArea, oilType)
                + "], above pool [" + CountLiquids(checkAbove, oilType) + "], sample cell type=" + sample.LiquidType + " amount=" + sample.LiquidAmount + " hasTile=" + sample.HasTile);
        }

        private string CountLiquids(Rectangle area, int oilType)
        {
            long oil = 0, water = 0, lava = 0, honey = 0, shimmer = 0, other = 0;
            for (int x = area.Left; x < area.Right; x++)
            {
                for (int y = area.Top; y < area.Bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 10))
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (tile.LiquidAmount == 0)
                        continue;

                    int type = tile.LiquidType;
                    if (type == oilType) oil++;
                    else if (type == LiquidID.Water) water++;
                    else if (type == LiquidID.Lava) lava++;
                    else if (type == LiquidID.Honey) honey++;
                    else if (type == LiquidID.Shimmer) shimmer++;
                    else other++;
                }
            }
            return "oil=" + oil + " lava=" + lava + " water=" + water + " honey=" + honey + " shimmer=" + shimmer + " other=" + other;
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