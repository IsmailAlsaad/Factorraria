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
    // PATCH 1 = diagnostic skeleton only: pass insertion, bail-outs, desert mask, per-column limits, log line. NO carving yet.
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
        public bool ExcludeHive = true;

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
                + " (patch 1: no carving yet)");

            progress.Set(1f);
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