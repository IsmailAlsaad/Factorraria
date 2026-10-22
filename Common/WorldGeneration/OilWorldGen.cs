using System;
using System.Collections.Generic;
using Factorraria.Content.Liquids.Oil;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ObjectData;
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
        // ShellStartFraction (the line below this block) is no longer used by the code and can be deleted.
        public int ShellThickness = 5;                // full Sandstone thickness (lower part of the oval)
        public double ShellFullFraction = 0.8;        // fraction of the oval height (0 = top, 1 = bottom): from here down the shell is full thickness
        public double ShellEndFraction = 0.6;         // the shell tapers linearly to zero thickness here; above it there is no shell
        public int OvalInset = 8;                     // shrink the ellipse by this many tiles on every side

        public int ScanMarginTop = 5;                 // scan this many rows above the rectangle
        public int ScanMarginBottom = 10;             // and this many below it
        public int MinColumnHeight = 20;              // a column shorter than this is invalid
        public int SmoothRadius = 2;                  // moving average of 2 * radius + 1 = 5 columns

        private readonly string[] anchorNames = { "Settle Liquids Again", "Settle Liquids", "Tile Cleanup", "Final Cleanup" };
        private string usedAnchor = "none";
        private Rectangle checkArea;                  // pool bounding box, for the liquid readback
        private Rectangle checkAbove;                 // oval interior above the pool
        private readonly List<Point> poolCellList = new List<Point>();   // every oil cell we wrote, for the restore pass
        private long lastProbeOil;
        private int probeOilType;

        // Saved by GenerateOval for the foreign-liquid sweep and the leftover check (the restore and check passes run later).
        public int MaxLiquidBodyCells = 6000;         // a foreign liquid body bigger than this is cleared only inside the oval, not as a whole
        private Rectangle ovalRect;
        private int ovalPoolTopY;
        private double ovalCx, ovalCy, ovalA, ovalB;
        private bool ovalValid;
        private byte[,] footKind;                     // 0 = untouched, 1 = shell, 2 = pool (same grid as GenerateOval)
        private int footLeft, footTop;

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
                poolCellList.Clear();
                lastProbeOil = 0;
                ovalValid = false;
                footKind = null;

                // A tiny probe after every later pass: logs only when the oil count in the pool box changes, naming the pass that changed it.
                for (int i = tasks.Count - 1; i > insertAt; i--)
                {
                    string passName = tasks[i].Name;
                    tasks.Insert(i + 1, new PassLegacy("Factorraria Oil Probe (" + passName + ")", (p, c) => ProbeAfter(passName), 0.01));
                }

                tasks.Add(new PassLegacy("Factorraria Oil Restore", RestoreOil, 1));
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
        //  - shell: a Sandstone band just inside the ellipse edge (like the shimmer's stone oval): full thickness below ShellFullFraction, tapering to zero at ShellEndFraction;
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
            int shellTopY = r.Top + (int)Math.Round(ShellEndFraction * r.Height);
            poolCellList.Clear();
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

                    double t = (y + 0.5 - r.Top) / r.Height;   // 0 = top of the oval, 1 = bottom
                    double thickness = t >= ShellFullFraction
                        ? ShellThickness
                        : ShellThickness * (t - ShellEndFraction) / Math.Max(0.0001, ShellFullFraction - ShellEndFraction);
                    if (thickness < 0.5)
                        thickness = 0;

                    double rowA = a - thickness;
                    double rowB = b - thickness;
                    double inner = dx * dx / (rowA * rowA) + dy * dy / (rowB * rowB);
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
                        objectsKilled += RemoveObjectAt(x, y);
                    }

                    // Whatever stands on the pool surface row (the cell above a pool cell) would be left floating: remove it too.
                    if (kind[i, j] == 2 && (j == 0 || kind[i, j - 1] == 0) && y > 0)
                    {
                        Tile surface = Main.tile[x, y - 1];
                        if (surface.HasTile && !Main.tileSolid[surface.TileType])
                            objectsKilled += RemoveObjectAt(x, y - 1);
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
                        poolCellList.Add(new Point(x, y));
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

            ovalRect = r;
            ovalPoolTopY = poolTopY;
            ovalCx = cx;
            ovalCy = cy;
            ovalA = a;
            ovalB = b;
            ovalValid = true;
            footKind = kind;
            footLeft = r.Left;
            footTop = yMin;
            int sweptNow = SweepForeignLiquid(oilType);
            Mod.Logger.Info("[OilGen] foreign liquid swept above the pool: " + sweptNow + " cells; leftovers in footprint: " + CountLeftovers());

            checkArea = new Rectangle(r.Left, poolTopY, r.Width, yMax - poolTopY + 1);
            checkAbove = new Rectangle(r.Left, shellTopY, r.Width, Math.Max(0, poolTopY - shellTopY));
            Mod.Logger.Info("[OilGen] readback right after carving (oilType=" + oilType + "): pool box [" + CountLiquids(checkArea, oilType)
                + "], above pool [" + CountLiquids(checkAbove, oilType) + "]");

            probeOilType = oilType;
            lastProbeOil = CountOil(checkArea, oilType);
        }

        // Removes the WHOLE object that owns the tile at (x, y): every tile of it (even outside the footprint), its chest or sign data
        // and its tile entities. Returns 1 if something was removed. Chest contents are discarded (Main.chest[i] = null).
        private int RemoveObjectAt(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile)
                return 0;

            int type = tile.TileType;
            int left = x, top = y, width = 1, height = 1;
            TileObjectData data = TileObjectData.GetTileData(tile);
            if (data != null)
            {
                Point16 topLeft = TileObjectData.TopLeft(x, y);
                left = topLeft.X;
                top = topLeft.Y;
                width = data.Width;
                height = data.Height;
            }

            if (Main.tileContainer[type])
            {
                int chestIndex = Chest.FindChest(left, top);
                if (chestIndex >= 0)
                    Main.chest[chestIndex] = null;
            }

            if (Main.tileSign[type])
                Sign.KillSign(left, top);

            for (int cx = left; cx < left + width; cx++)
            {
                for (int cy = top; cy < top + height; cy++)
                {
                    if (!WorldGen.InWorld(cx, cy, 10))
                        continue;

                    Point16 pos = new Point16(cx, cy);
                    if (TileEntity.ByPosition.TryGetValue(pos, out TileEntity entity))
                    {
                        lock (TileEntity.EntityCreationLock)
                        {
                            TileEntity.ByID.Remove(entity.ID);
                            TileEntity.ByPosition.Remove(pos);
                        }
                    }

                    Tile part = Main.tile[cx, cy];
                    if (part.HasTile && part.TileType == type)
                        part.HasTile = false;
                }
            }

            return 1;
        }

        private bool InsideOval(int x, int y)
        {
            double dx = x + 0.5 - ovalCx;
            double dy = y + 0.5 - ovalCy;
            return dx * dx / (ovalA * ovalA) + dy * dy / (ovalB * ovalB) <= 1.0;
        }

        // Removes every non-oil liquid in the oval above the pool. A liquid body (4-connected, one type) that touches the oval is removed WHOLE
        // (also the part outside the oval) so no neighbour is left that could flow in later; a body bigger than MaxLiquidBodyCells is only cleared inside the oval.
        // Returns the number of cells cleared.
        private int SweepForeignLiquid(int oilType)
        {
            if (!ovalValid)
                return 0;

            int cleared = 0;
            bool bigFound = false;
            int mapW = Main.maxTilesX;
            HashSet<int> visited = new HashSet<int>();
            List<Point> found = new List<Point>();
            Queue<Point> queue = new Queue<Point>();
            Point[] steps = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };

            for (int x = ovalRect.Left; x < ovalRect.Right; x++)
            {
                for (int y = ovalRect.Top; y < ovalPoolTopY; y++)
                {
                    if (!WorldGen.InWorld(x, y, 10) || !InsideOval(x, y))
                        continue;

                    Tile seed = Main.tile[x, y];
                    if (seed.LiquidAmount == 0 || seed.LiquidType == oilType)
                        continue;

                    if (bigFound)
                    {
                        seed.LiquidAmount = 0;
                        cleared++;
                        continue;
                    }

                    int liquidType = seed.LiquidType;
                    visited.Clear();
                    found.Clear();
                    queue.Clear();
                    queue.Enqueue(new Point(x, y));
                    visited.Add(y * mapW + x);
                    found.Add(new Point(x, y));
                    bool tooBig = false;

                    while (queue.Count > 0 && !tooBig)
                    {
                        Point p = queue.Dequeue();
                        for (int s = 0; s < 4; s++)
                        {
                            int nx = p.X + steps[s].X;
                            int ny = p.Y + steps[s].Y;
                            if (!WorldGen.InWorld(nx, ny, 10))
                                continue;

                            Tile n = Main.tile[nx, ny];
                            if (n.LiquidAmount == 0 || n.LiquidType != liquidType)
                                continue;

                            if (!visited.Add(ny * mapW + nx))
                                continue;

                            found.Add(new Point(nx, ny));
                            queue.Enqueue(new Point(nx, ny));
                            if (found.Count > MaxLiquidBodyCells)
                            {
                                tooBig = true;
                                break;
                            }
                        }
                    }

                    if (tooBig)
                        bigFound = true;

                    foreach (Point p in found)
                    {
                        if (tooBig && !InsideOval(p.X, p.Y))
                            continue;

                        Tile clearTile = Main.tile[p.X, p.Y];
                        clearTile.LiquidAmount = 0;
                        cleared++;
                    }
                }
            }

            return cleared;
        }

        private static bool IsLiquidMergeBlock(int tileType)
        {
            return tileType == TileID.TeamBlockRed || tileType == TileID.TeamBlockBlue || tileType == TileID.TeamBlockYellow
                || tileType == TileID.TeamBlockPink || tileType == TileID.TeamBlockWhite;
        }

        private bool InFootprint(int x, int y)
        {
            int i = x - footLeft;
            int j = y - footTop;
            return i >= 0 && j >= 0 && i < footKind.GetLength(0) && j < footKind.GetLength(1) && footKind[i, j] != 0;
        }

        private bool AnyInFootprint(int x, int y, int width, int height)
        {
            for (int dx = 0; dx < width; dx++)
                for (int dy = 0; dy < height; dy++)
                    if (InFootprint(x + dx, y + dy))
                        return true;
            return false;
        }

        // Diagnostic: containers, signs and tile entities still registered inside the footprint, and merge blocks left in pool cells. All should be 0.
        private string CountLeftovers()
        {
            if (footKind == null)
                return "no footprint";

            int chests = 0, signs = 0, entities = 0, blocks = 0;
            for (int i = 0; i < Main.chest.Length; i++)
            {
                Chest chest = Main.chest[i];
                if (chest != null && AnyInFootprint(chest.x, chest.y, 3, 2))
                    chests++;
            }

            for (int i = 0; i < Main.sign.Length; i++)
            {
                Sign sign = Main.sign[i];
                if (sign != null && AnyInFootprint(sign.x, sign.y, 2, 2))
                    signs++;
            }

            foreach (Point16 pos in TileEntity.ByPosition.Keys)
            {
                if (InFootprint(pos.X, pos.Y))
                    entities++;
            }

            foreach (Point p in poolCellList)
            {
                Tile tile = Main.tile[p.X, p.Y];
                if (tile.HasTile)
                    blocks++;
            }

            return "chests=" + chests + " signs=" + signs + " tileEntities=" + entities + " tilesInPool=" + blocks;
        }

        private long CountOil(Rectangle area, int oilType)
        {
            long oil = 0;
            for (int x = area.Left; x < area.Right; x++)
            {
                for (int y = area.Top; y < area.Bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 10))
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (tile.LiquidAmount > 0 && tile.LiquidType == oilType)
                        oil++;
                }
            }
            return oil;
        }

        private void ProbeAfter(string passName)
        {
            if (checkArea.Width <= 0)
                return;

            long oil = CountOil(checkArea, probeOilType);
            if (oil != lastProbeOil)
            {
                Mod.Logger.Info("[OilGen] probe: oil in pool box went " + lastProbeOil + " -> " + oil + " during/after pass '" + passName + "', pool box now [" + CountLiquids(checkArea, probeOilType) + "]");
                lastProbeOil = oil;
            }
        }

        // Workaround: re-applies oil to every pool cell that no longer holds it, after all other passes.
        private void RestoreOil(GenerationProgress progress, GameConfiguration configuration)
        {
            if (poolCellList.Count == 0)
                return;

            int oilType = ModLiquidLib.ModLoader.LiquidLoader.LiquidType<OilLiquid>();
            int fixedCells = 0;
            int teamBlocksRemoved = 0;
            foreach (Point p in poolCellList)
            {
                Tile tile = Main.tile[p.X, p.Y];
                if (tile.HasTile)
                {
                    // A team block in a pool cell is what oil leaves when it meets another liquid: take it out, the cell becomes oil again.
                    if (!IsLiquidMergeBlock(tile.TileType))
                        continue;

                    tile.HasTile = false;
                    teamBlocksRemoved++;
                }

                if (tile.LiquidType != oilType || tile.LiquidAmount < byte.MaxValue)
                {
                    tile.LiquidType = oilType;
                    tile.LiquidAmount = byte.MaxValue;
                    fixedCells++;
                }
            }

            int sweptLate = SweepForeignLiquid(oilType);
            Mod.Logger.Info("[OilGen] restore pass: re-applied oil to " + fixedCells + " of " + poolCellList.Count + " pool cells, removed " + teamBlocksRemoved
                + " merge blocks, swept " + sweptLate + " foreign liquid cells above the pool");
        }

        // Runs after every other worldgen pass: shows whether a later pass changed the liquid we wrote.
        private void CheckOil(GenerationProgress progress, GameConfiguration configuration)
        {
            if (checkArea.Width <= 0)
                return;

            int oilType = ModLiquidLib.ModLoader.LiquidLoader.LiquidType<OilLiquid>();
            Mod.Logger.Info("[OilGen] END leftovers in footprint: " + CountLeftovers());
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