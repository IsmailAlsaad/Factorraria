using System;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Phase 5: vanilla trees on the Harvester Boxes. No tree or sapling ModTile and no ModTree: the boxes are only made to count as vanilla soil.
    // Each box kind names the vanilla soil it stands in for (HarvesterBoxKinds.TreeSoil: grass, jungle grass, corruption grass, crimson grass, snow),
    // and there are two tools:
    //  - IsTileTypeFitForTree says yes for a box, so vanilla's own "does this tree still stand" checks keep working.
    //  - Disguise (Begin / End): for the length of ONE vanilla call the box tile's type is the vanilla soil (the same trick HarvesterBoxHerbHooks uses for the
    //    planter box). Vanilla then reads soil lists, tree look, wood and acorn rules exactly as it does for that soil. The real type, frame and neighbours'
    //    frames are always put back.
    // Everything reads tile data every client and the server already have, so the hooks need no packet. Growing, felling and planting run on the server
    // (single-player included); clients only see the vanilla tile messages.
    public static class HarvesterBoxTrees
    {
        public struct Disguise
        {
            public bool Active;
            public int X, Y, Soil;
            public ushort Real;
            public short FrameX, FrameY;
            public bool Fake;   // a temporary tile made in an empty cell (End takes it away again)
        }

        // True only while TryGrowSapling calls vanilla's growth, so the hook below can tell our growth from vanilla's own random tick.
        public static bool OwnGrowth;

        // > 0 while RunAsTreeSoilWithSides runs (our own felling, or a manual break already wrapped by the KillTile hook), so the hook never wraps twice.
        public static int SideDepth;

        static bool GrowOwn(int x, int bottomY)
        {
            HarvesterBoxTreeHooks.DbgReset();
            OwnGrowth = true;
            try
            {
                return WorldGen.AttemptToGrowTreeFromSapling(x, bottomY, false);
            }
            finally
            {
                OwnGrowth = false;
            }
        }

        public static bool IsTreeBox(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && HarvesterBoxRegistry.BoxTreeSoil.ContainsKey(tile.TileType);
        }

        // Starts a disguise (Active stays false when (x, y) is not a tree box, so End does nothing).
        public static Disguise Begin(int x, int y)
        {
            if (!IsTreeBox(x, y))
                return default;

            Tile soil = Main.tile[x, y];
            Disguise d = new Disguise
            {
                Active = true,
                X = x,
                Y = y,
                Real = soil.TileType,
                FrameX = soil.TileFrameX,
                FrameY = soil.TileFrameY,
                Soil = HarvesterBoxRegistry.BoxTreeSoil[soil.TileType]
            };
            soil.TileType = (ushort)d.Soil;
            return d;
        }

        // Puts the box back. reframeRow: vanilla may have framed the disguised tile (a sapling or tree frames its soil row), and its box neighbours read it
        // as a foreign tile while it was disguised, so the three boxes are framed again (the box's own TileFrame sets the merge cell).
        public static void End(Disguise d, bool reframeRow)
        {
            if (!d.Active)
                return;

            Tile soil = Main.tile[d.X, d.Y];
            if (soil.HasTile && soil.TileType == d.Soil)
            {
                soil.TileType = d.Real;
                soil.TileFrameX = d.FrameX;
                soil.TileFrameY = d.FrameY;
                if (d.Fake)
                    soil.HasTile = false;
            }

            if (!reframeRow)
                return;

            for (int dx = -1; dx <= 1; dx++)
            {
                int x = d.X + dx;
                if (WorldGen.InWorld(x, d.Y, 1) && Main.tile[x, d.Y].HasTile && HarvesterBoxRegistry.BoxHerbs.ContainsKey(Main.tile[x, d.Y].TileType))
                    WorldGen.TileFrame(x, d.Y, false, true);
            }
        }

        public static void RunAsTreeSoil(int x, int y, Action vanilla)
        {
            Disguise d = Begin(x, y);
            try
            {
                vanilla();
            }
            finally
            {
                End(d, true);
            }
        }

        // Felling a tree. Vanilla's branch and root pieces look down THEIR OWN column (x - 1 and x + 1) for the first solid tile and read the tree type from it
        // when they drop an acorn or wood. Beside a box that tile is stone, a neighbour box or nothing a tree grows on, so vanilla indexes outside its tables,
        // the exception aborts the fell halfway and the remaining branches float. So for the length of the call the surface tiles of both side columns (from 40
        // tiles above the box down to the first solid tile at or under the box row) stand in for soil too: a neighbour box for its own soil, any other tile that
        // is not already tree soil for the middle box's soil. Types, frames are put back afterwards.
        public static void RunAsTreeSoilWithSides(int x, int y, Action vanilla)
        {
            Disguise middle = Begin(x, y);
            int soilType = middle.Active ? middle.Soil : TileID.Grass;
            System.Collections.Generic.List<Disguise> sides = new System.Collections.Generic.List<Disguise>();

            SideDepth++;
            try
            {
                DisguiseSideColumn(x - 1, y, soilType, sides);
                DisguiseSideColumn(x + 1, y, soilType, sides);
                sides.Add(Begin(x - 2, y));   // a felled piece next to a neighbour tree reframes it, and the tree two boxes away must see its soil too
                sides.Add(Begin(x + 2, y));
                vanilla();
            }
            finally
            {
                SideDepth--;
                for (int i = sides.Count - 1; i >= 0; i--)
                    End(sides[i], false);

                End(middle, true);
            }
        }

        static bool IsSolidGround(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        static void DisguiseSideColumn(int x, int soilY, int soilType, System.Collections.Generic.List<Disguise> list)
        {
            // A box is solid-TOP (Main.tileSolidTop), which vanilla's downward search walks straight past, so a box is disguised whatever its own solid flags say.
            for (int y = Math.Max(1, soilY - 40); y < soilY + 100 && WorldGen.InWorld(x, y, 1); y++)
            {
                Tile tile = Main.tile[x, y];
                int soil = 0;
                if (tile.HasTile && HarvesterBoxRegistry.BoxTreeSoil.TryGetValue(tile.TileType, out int boxSoil))
                    soil = boxSoil;
                else if (IsSolidGround(x, y))
                {
                    if (!WorldGen.IsTileTypeFitForTree(tile.TileType))
                        soil = soilType;
                }
                else
                    continue;

                if (soil != 0)
                {
                    list.Add(new Disguise { Active = true, X = x, Y = y, Real = tile.TileType, FrameX = tile.TileFrameX, FrameY = tile.TileFrameY, Soil = soil });
                    tile.TileType = (ushort)soil;
                }

                if (y >= soilY)
                    return;

                // skip the rest of this solid stretch; the next surface under it serves the pieces below
                while (y + 1 < soilY + 100 && WorldGen.InWorld(x, y + 1, 1) && IsSolidGround(x, y + 1))
                    y++;
            }

            // No ground in this column at or under the box row (a lone box, or the end of a row, in the open): vanilla's search for the soil under a branch would
            // run out of the world and throw. A temporary soil tile in the empty cell beside the box stops it; it is removed again by End.
            if (WorldGen.InWorld(x, soilY, 1) && !Main.tile[x, soilY].HasTile)
            {
                Tile cell = Main.tile[x, soilY];
                list.Add(new Disguise { Active = true, Fake = true, X = x, Y = soilY, Real = cell.TileType, FrameX = cell.TileFrameX, FrameY = cell.TileFrameY, Soil = soilType });
                cell.TileType = (ushort)soilType;
                cell.HasTile = true;
            }
        }

        // The first tree box under (x, y), looking down through air and trunk tiles only (what vanilla's own soil search walks through).
        public static bool TryFindSoilBelow(int x, int y, int maxDown, out int soilY)
        {
            soilY = 0;
            for (int k = 0; k < maxDown; k++)
            {
                int yy = y + k;
                if (!WorldGen.InWorld(x, yy, 1))
                    return false;

                Tile tile = Main.tile[x, yy];
                if (!tile.HasTile)
                    continue;

                if (HarvesterBoxRegistry.BoxTreeSoil.ContainsKey(tile.TileType))
                {
                    soilY = yy;
                    return true;
                }

                if (tile.TileType != TileID.Trees)
                    return false;
            }

            return false;
        }

        // The tree box a tree tile belongs to, for a tile broken from outside our own code (a player's axe, an explosion). A trunk or root piece looks down its own
        // column; a branch piece has no box under its column, so the trunk beside it is tried too. Walks only through air and trunk tiles, like vanilla's soil search.
        public static bool TryFindBoxForTreeTile(int i, int j, out int boxX, out int boxY)
        {
            boxX = 0;
            boxY = 0;
            for (int n = 0; n < 3; n++)
            {
                int c = n == 0 ? i : (n == 1 ? i - 1 : i + 1);
                if (!WorldGen.InWorld(c, j, 1))
                    continue;

                Tile tile = Main.tile[c, j];
                if (!tile.HasTile || tile.TileType != TileID.Trees)
                    continue;

                if (TryFindSoilBelow(c, j, 100, out boxY))
                {
                    boxX = c;
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- saplings

        public static bool IsSapling(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == TileID.Saplings;
        }

        // A sapling is two tiles tall: (x, bottomY - 1) and (x, bottomY), bottomY being the tile right above the box.
        public static bool CanPlantSapling(int x, int bottomY)
        {
            if (!WorldGen.InWorld(x, bottomY - 1, 4) || !IsTreeBox(x, bottomY + 1))
                return false;

            for (int y = bottomY - 1; y <= bottomY; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile || tile.LiquidAmount > 0)
                    return false;
            }

            return true;
        }

        // Writes the two sapling tiles the way vanilla's object placement does (frameX = style * 54 + a random variant * 18, frameY 0 for the top tile and 18
        // for the bottom tile; Check1x2 fixes the style from the soil on its first call anyway). Server and single-player only; the server sends the tiles.
        public static void PlaceSapling(int x, int bottomY, int style)
        {
            short frameX = (short)(style * 54 + WorldGen.genRand.Next(3) * 18);

            Tile top = Main.tile[x, bottomY - 1];
            top.HasTile = true;
            top.TileType = TileID.Saplings;
            top.TileFrameX = frameX;
            top.TileFrameY = 0;

            Tile bottom = Main.tile[x, bottomY];
            bottom.HasTile = true;
            bottom.TileType = TileID.Saplings;
            bottom.TileFrameX = frameX;
            bottom.TileFrameY = 18;

            WorldGen.SquareTileFrame(x, bottomY);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x, bottomY - 1, 3);
        }

        // One growth attempt for the sapling whose bottom tile is (x, bottomY). Server and single-player. The box under the sapling AND the boxes beside it
        // stand in for their vanilla soil for the call, so a box row grows a tree with the root flare over its neighbours, like a row of grass blocks.
        // Vanilla's GrowTree also needs a soil tile beside the one under the sapling, so a LONE box would never grow a tree. When neither side has one,
        // an EMPTY side cell is made an actuated tile of the vanilla soil for the length of the call: it passes "is there a soil tile beside it" (active) and
        // fails the root flare test (not actuated), so the tree grows with no roots and nothing is left behind. A box between two foreign blocks has no
        // empty side and never grows a tree.
        public static bool TryGrowSapling(int x, int bottomY)
        {
            int soilY = bottomY + 1;
            if (!IsSapling(x, bottomY) || !IsTreeBox(x, soilY) || Main.netMode == NetmodeID.MultiplayerClient)
                return false;

            int soilType = HarvesterBoxRegistry.BoxTreeSoil[Main.tile[x, soilY].TileType];

            Disguise middle = Begin(x, soilY);
            Disguise left = Begin(x - 1, soilY);
            Disguise right = Begin(x + 1, soilY);
            Disguise farLeft = Begin(x - 2, soilY);   // GrowTree frames x - 2 .. x + 2, so a tree standing two boxes away is checked too
            Disguise farRight = Begin(x + 2, soilY);

            int fakeX = int.MinValue;
            Tile fake = default;
            ushort fakeOldType = 0;
            bool fakeOldActuated = false;
            bool grew = false;

            try
            {
                if (!SideFits(x - 1, soilY) && !SideFits(x + 1, soilY))
                {
                    if (WorldGen.InWorld(x - 1, soilY, 1) && !Main.tile[x - 1, soilY].HasTile)
                        fakeX = x - 1;
                    else if (WorldGen.InWorld(x + 1, soilY, 1) && !Main.tile[x + 1, soilY].HasTile)
                        fakeX = x + 1;
                }

                if (fakeX != int.MinValue)
                {
                    fake = Main.tile[fakeX, soilY];
                    fakeOldType = fake.TileType;
                    fakeOldActuated = fake.IsActuated;
                    fake.TileType = (ushort)soilType;
                    fake.IsActuated = true;
                    fake.HasTile = true;
                }

                HarvesterBoxTreeHooks.DbgHeader = $"sapling ({x},{bottomY}) soil={soilType} boxTile={Main.tile[x, soilY].TileType} disguised[mid={middle.Active} L={left.Active} R={right.Active} LL={farLeft.Active} RR={farRight.Active}] fakeX={(fakeX == int.MinValue ? "none" : fakeX.ToString())} sideFits[L={SideFits(x - 1, soilY)} R={SideFits(x + 1, soilY)}]";

                if (fakeX != int.MinValue || SideFits(x - 1, soilY) || SideFits(x + 1, soilY))
                    grew = GrowOwn(x, bottomY);
            }
            finally
            {
                if (fakeX != int.MinValue)
                {
                    fake = Main.tile[fakeX, soilY];
                    fake.HasTile = false;
                    fake.IsActuated = fakeOldActuated;
                    fake.TileType = fakeOldType;
                }

                End(farRight, false);
                End(farLeft, false);
                End(right, false);
                End(left, false);
                End(middle, true);
            }

            HarvesterBoxTreeHooks.DbgEnd(x, bottomY, grew);
            return grew;
        }

        static bool SideFits(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && WorldGen.IsTileTypeFitForTree(tile.TileType);
        }

        // ---------------------------------------------------------------- the tree

        // A grown tree: the tile above the box is a trunk tile and the column goes up, trunk tile after trunk tile, to a top (frameX 22, frameY >= 198,
        // the same test vanilla uses for the acorn piece). A root piece of a neighbouring tree (a trunk tile with air above it) is not a tree.
        public static bool IsTreeReady(int x, int bottomY)
        {
            if (!WorldGen.InWorld(x, bottomY - 80, 1))
                return false;

            for (int k = 0; k <= 80; k++)
            {
                Tile tile = Main.tile[x, bottomY - k];
                // No top frame: a tall trunk column with air above it is still a (topless) tree and gets felled. A lone root piece of a neighbouring tree is one tile.
                if (!tile.HasTile || tile.TileType != TileID.Trees)
                    return k >= 4;

                if (tile.TileFrameX == 22 && tile.TileFrameY >= 198)
                    return true;
            }

            return false;
        }
    }

    // The vanilla hooks that make a Harvester Box look like vanilla soil to the tree code. Each one reads only tile data, so they behave the same on every side.
    public class HarvesterBoxTreeHooks : ModSystem
    {
        public override void Load()
        {
            On_WorldGen.IsTileTypeFitForTree += IsTileTypeFitForTreeHook;
            On_WorldGen.Check1x2 += Check1x2Hook;
            On_WorldGen.GetCommonTreeFoliageData += GetCommonTreeFoliageDataHook;
            On_TileDrawing.GetTreeVariant += GetTreeVariantHook;
            On_WorldGen.AttemptToGrowTreeFromSapling += AttemptToGrowTreeFromSaplingHook;
            On_WorldGen.CheckTree += CheckTreeDebugHook;
            On_WorldGen.KillTile += KillTileDebugHook;
            On_WorldGen.KillTile += KillTileTreeHook;
        }

        public override void Unload()
        {
            On_WorldGen.IsTileTypeFitForTree -= IsTileTypeFitForTreeHook;
            On_WorldGen.Check1x2 -= Check1x2Hook;
            On_WorldGen.GetCommonTreeFoliageData -= GetCommonTreeFoliageDataHook;
            On_TileDrawing.GetTreeVariant -= GetTreeVariantHook;
            On_WorldGen.AttemptToGrowTreeFromSapling -= AttemptToGrowTreeFromSaplingHook;
            On_WorldGen.CheckTree -= CheckTreeDebugHook;
            On_WorldGen.KillTile -= KillTileDebugHook;
            On_WorldGen.KillTile -= KillTileTreeHook;
        }

        // A tree tile broken by anything but our own code (a player's axe or pickaxe, an explosion): vanilla's drop code reads the soil under the tree, and the
        // raw box tile gives plain wood, and a branch beside a box walks out of the world (IndexOutOfRange in KillTile_GetTreeDrops). So the break runs with the
        // box and its side columns disguised, exactly like our own felling, and the cascade of the falling tree runs inside the same disguise. Only the final
        // break (not a hit that only damages the tile). It reads and swaps tile data only, so the server (which handles a client's break) and single-player behave
        // the same.
        static void KillTileTreeHook(On_WorldGen.orig_KillTile orig, int i, int j, bool fail, bool effectOnly, bool noItem)
        {
            if (!fail && !effectOnly && HarvesterBoxTrees.SideDepth == 0 && !HarvesterBoxTrees.OwnGrowth && HarvesterBoxRegistry.BoxTreeSoil.Count > 0
                && WorldGen.InWorld(i, j, 4) && Main.tile[i, j].HasTile && Main.tile[i, j].TileType == TileID.Trees
                && HarvesterBoxTrees.TryFindBoxForTreeTile(i, j, out int boxX, out int boxY))
            {
                HarvesterBoxTrees.RunAsTreeSoilWithSides(boxX, boxY, () => orig(i, j, fail, effectOnly, noItem));
                return;
            }

            orig(i, j, fail, effectOnly, noItem);
        }

        // ---------------------------------------------------------------- TEMPORARY growth debug (patch_harvester_trees_debug). Remove once the growth bug is fixed.
        // Only active while EnableDebugs is on AND our own growth call runs. Logs the first kills of tree tiles during one growth: which tile died, and the
        // CheckTree call that killed it (its tile, frame and the four tiles around it, with their type, frame, active and actuated state; BOX! marks a box
        // tile that was NOT disguised at that moment).
        public static string DbgHeader = "";
        static int dbgKills;
        static readonly System.Collections.Generic.List<string> dbgStack = new System.Collections.Generic.List<string>();

        static bool DbgActive
        {
            get
            {
                if (!HarvesterBoxTrees.OwnGrowth)
                    return false;

                global::Factorraria.Content.Configs.FurnaceOffsetConfig cfg = ModContent.GetInstance<global::Factorraria.Content.Configs.FurnaceOffsetConfig>();
                return cfg != null && cfg.EnableDebugs;
            }
        }

        public static void DbgReset()
        {
            dbgKills = 0;
            dbgStack.Clear();
        }

        static string DbgShort(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return "out";

            Tile t = Main.tile[x, y];
            return t.HasTile ? "t" + t.TileType + ":" + t.TileFrameX + "," + t.TileFrameY : "-";
        }

        static string Dbg(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return "outside";

            Tile t = Main.tile[x, y];
            if (!t.HasTile)
                return "air";

            string s = "t" + t.TileType + " f" + t.TileFrameX + "," + t.TileFrameY + (t.IsActuated ? " ACTUATED" : "");
            if (HarvesterBoxRegistry.BoxTreeSoil.ContainsKey(t.TileType))
                s += " BOX!";

            return s;
        }

        static string DbgCheck(int i, int j)
        {
            return $"({i},{j}) [{Dbg(i, j)}] up[{Dbg(i, j - 1)}] below[{Dbg(i, j + 1)}] left[{Dbg(i - 1, j)}] right[{Dbg(i + 1, j)}]";
        }

        static void DbgLog(string text)
        {
            ModContent.GetInstance<HarvesterBoxTreeHooks>().Mod.Logger.Warn("[HarvesterTrees] " + text);
        }

        // One line after every growth attempt: did it grow, how many tree tiles were killed, and does the column have a top.
        public static void DbgEnd(int x, int bottomY, bool grew)
        {
            global::Factorraria.Content.Configs.FurnaceOffsetConfig cfg = ModContent.GetInstance<global::Factorraria.Content.Configs.FurnaceOffsetConfig>();
            if (cfg == null || !cfg.EnableDebugs)
                return;

            int height = 0;
            string topFrame = "none";
            for (int k = 0; k <= 80 && WorldGen.InWorld(x, bottomY - k, 1); k++)
            {
                Tile t = Main.tile[x, bottomY - k];
                if (!t.HasTile || t.TileType != TileID.Trees)
                    break;

                height++;
                topFrame = t.TileFrameX + "," + t.TileFrameY;
            }

            // A tree whose highest trunk tile is not a top frame (22,198+): dump the tiles around its top (5 columns, rows from 3 above the top to 1 below it).
            string grid = "";
            if (!topFrame.StartsWith("22,"))
            {
                int topY = bottomY - Math.Max(0, height - 1);
                for (int yy = topY - 3; yy <= topY + 1; yy++)
                {
                    grid += "\n    y" + yy + ":";
                    for (int xx = x - 2; xx <= x + 2; xx++)
                        grid += " [" + DbgShort(xx, yy) + "]";
                }
            }

            DbgLog($"result for ({x},{bottomY}): grew={grew} killsLogged={dbgKills} trunkTiles={height} highestTrunkFrame={topFrame} ready={HarvesterBoxTrees.IsTreeReady(x, bottomY)} | {DbgHeader}{grid}");
        }

        static void CheckTreeDebugHook(On_WorldGen.orig_CheckTree orig, int i, int j)
        {
            if (!DbgActive)
            {
                orig(i, j);
                return;
            }

            dbgStack.Add(DbgCheck(i, j));
            try
            {
                orig(i, j);
            }
            finally
            {
                dbgStack.RemoveAt(dbgStack.Count - 1);
            }
        }

        static void KillTileDebugHook(On_WorldGen.orig_KillTile orig, int i, int j, bool fail, bool effectOnly, bool noItem)
        {
            if (dbgStack.Count > 0 && dbgKills < 8 && DbgActive && WorldGen.InWorld(i, j, 1) && Main.tile[i, j].HasTile && Main.tile[i, j].TileType == TileID.Trees)
            {
                dbgKills++;
                DbgLog($"KILL #{dbgKills} tree tile {Dbg(i, j)} at ({i},{j}), checkDepth={dbgStack.Count}\n    innermost CheckTree: {dbgStack[dbgStack.Count - 1]}\n    outermost CheckTree: {dbgStack[0]}\n    growth: {DbgHeader}");
            }

            orig(i, j, fail, effectOnly, noItem);
        }

        // Vanilla's random tick also grows saplings, and on a box it would do it with no soil disguise: the tree is built, then its own CheckTree pass kills
        // the pieces that do not see tree soil (the IndexOutOfRange in KillTile_GetTreeDrops) and leaves some floating. Box saplings grow only through
        // HarvesterBoxTrees.TryGrowSapling. The call may name the top or the bottom tile of the sapling, so both box rows are checked.
        static bool AttemptToGrowTreeFromSaplingHook(On_WorldGen.orig_AttemptToGrowTreeFromSapling orig, int x, int y, bool underground)
        {
            if (!HarvesterBoxTrees.OwnGrowth && HarvesterBoxRegistry.BoxTreeSoil.Count > 0 && (HarvesterBoxTrees.IsTreeBox(x, y + 1) || HarvesterBoxTrees.IsTreeBox(x, y + 2)))
                return false;

            return orig(x, y, underground);
        }

        // A tree on a box keeps standing (vanilla's trunk and sapling checks ask this about the soil), and a box beside a box counts as the soil beside it.
        static bool IsTileTypeFitForTreeHook(On_WorldGen.orig_IsTileTypeFitForTree orig, ushort type)
        {
            return HarvesterBoxRegistry.BoxTreeSoil.ContainsKey(type) || orig(type);
        }

        // A sapling survives and keeps its style: Check1x2 reads the soil two tiles under the sapling's top tile (one under its bottom tile).
        static void Check1x2Hook(On_WorldGen.orig_Check1x2 orig, int x, int j, ushort type)
        {
            if (HarvesterBoxRegistry.BoxTreeSoil.Count > 0 && type == TileID.Saplings)
            {
                for (int k = 1; k <= 2; k++)
                {
                    if (HarvesterBoxTrees.IsTreeBox(x, j + k))
                    {
                        HarvesterBoxTrees.RunAsTreeSoil(x, j + k, () => orig(x, j, type));
                        return;
                    }
                }
            }

            orig(x, j, type);
        }

        // Tops and branches: vanilla walks down the trunk's column to the first soil tile and takes the tree style from it.
        static bool GetCommonTreeFoliageDataHook(On_WorldGen.orig_GetCommonTreeFoliageData orig, int i, int j, int xoffset, ref int treeFrame, ref int treeStyle, out int floorY, out int topTextureFrameWidth, out int topTextureFrameHeight)
        {
            if (HarvesterBoxRegistry.BoxTreeSoil.Count > 0 && HarvesterBoxTrees.TryFindSoilBelow(i + xoffset, j, 100, out int soilY))
            {
                HarvesterBoxTrees.Disguise d = HarvesterBoxTrees.Begin(i + xoffset, soilY);
                try
                {
                    return orig(i, j, xoffset, ref treeFrame, ref treeStyle, out floorY, out topTextureFrameWidth, out topTextureFrameHeight);
                }
                finally
                {
                    HarvesterBoxTrees.End(d, false);
                }
            }

            return orig(i, j, xoffset, ref treeFrame, ref treeStyle, out floorY, out topTextureFrameWidth, out topTextureFrameHeight);
        }

        // The trunk's biome (which column of the trunk sheet is drawn) comes from the soil row under the trunk.
        static int GetTreeVariantHook(On_TileDrawing.orig_GetTreeVariant orig, int x, int y)
        {
            if (HarvesterBoxRegistry.BoxTreeSoil.Count > 0 && HarvesterBoxTrees.TryFindSoilBelow(x, y, 100, out int soilY))
            {
                HarvesterBoxTrees.Disguise d = HarvesterBoxTrees.Begin(x, soilY);
                try
                {
                    return orig(x, y);
                }
                finally
                {
                    HarvesterBoxTrees.End(d, false);
                }
            }

            return orig(x, y);
        }
    }
}