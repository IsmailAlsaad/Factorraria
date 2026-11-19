using Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Phase 5 (docs/harvester_box/ROADMAP.md 6g): what the boxes need to know about trees. Server and single-player only (the callers guard that).
    public static class HarvesterBoxTrees
    {
        // Box tile types that carry a ModTree (each HarvesterBoxTreeBase adds its box in SetStaticDefaults; HarvesterBoxRegistry.Clear empties it).
        public static HashSet<int> TreeBoxes = new();

        public static void Clear() => TreeBoxes.Clear();

        // A grown tree: its trunk base is at x, y (the tile above the box), with another tree tile right above it. A root flare beside a trunk has no tree
        // tile above it, so the box under a flare does not take it for a trunk.
        public static bool IsGrownTree(int x, int y, int boxTileType)
        {
            if (!TreeBoxes.Contains(boxTileType) || !WorldGen.InWorld(x, y, 2))
                return false;

            Tile soil = Main.tile[x, y + 1];
            if (!soil.HasTile || soil.TileType != boxTileType)
                return false;

            Tile baseTile = Main.tile[x, y];
            Tile above = Main.tile[x, y - 1];
            return baseTile.HasTile && baseTile.TileType == TileID.Trees && above.HasTile && above.TileType == TileID.Trees;
        }

        // One of our saplings, bottom tile at x, y, standing on that box.
        public static bool IsSapling(int x, int y, int boxTileType)
        {
            if (!TreeBoxes.Contains(boxTileType) || !WorldGen.InWorld(x, y, 2))
                return false;

            Tile soil = Main.tile[x, y + 1];
            if (!soil.HasTile || soil.TileType != boxTileType)
                return false;

            Tile plant = Main.tile[x, y];
            return plant.HasTile && plant.TileType == ModContent.TileType<HarvesterBoxSapling>();
        }

        // Asks vanilla to grow the sapling whose bottom tile is x, y (it calls HarvesterBoxSapling.GrowSapling). Not underground-only: a box grows anywhere.
        public static bool TryGrow(int x, int y)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return false;

            return WorldGen.AttemptToGrowTreeFromSapling(x, y, underground: false, treeHeightAddon: 0, ignoreWalls: false);
        }

        // Vanilla's GrowTree wants at least one SIDE neighbour of the soil that is a tree-fit tile (a lone box has none). The gate only reads "active and fit
        // type"; the root flare also needs "not a half block, no slope" (probe 5). So when neither side qualifies, the empty cell on the left is a fake
        // "grass, half block" for the length of the call: the gate passes, no flare is built over it. Everything touched is put back afterwards.
        public static bool GrowTree(int i, int j, int treeHeightAddon, bool ignoreWalls)
        {
            int soilY = j;
            while (WorldGen.InWorld(i, soilY, 2) && Main.tile[i, soilY].HasTile && TileID.Sets.TreeSapling[Main.tile[i, soilY].TileType])
                soilY++;

            if (!WorldGen.InWorld(i, soilY, 3))
                return false;

            if (SideIsFit(i - 1, soilY) || SideIsFit(i + 1, soilY))
                return WorldGen.GrowTree(i, j, treeHeightAddon, ignoreWalls);

            Tile cell = Main.tile[i - 1, soilY];
            bool hadTile = cell.HasTile;
            ushort type = cell.TileType;
            bool halfBlock = cell.IsHalfBlock;
            short frameX = cell.TileFrameX;
            short frameY = cell.TileFrameY;

            try
            {
                cell.TileType = TileID.Grass;
                cell.IsHalfBlock = true;
                cell.HasTile = true;
                return WorldGen.GrowTree(i, j, treeHeightAddon, ignoreWalls);
            }
            finally
            {
                cell.HasTile = hadTile;
                cell.TileType = type;
                cell.IsHalfBlock = halfBlock;
                cell.TileFrameX = frameX;
                cell.TileFrameY = frameY;
            }
        }

        static bool SideIsFit(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && WorldGen.IsTileTypeFitForTree(tile.TileType);
        }

        // Plants a new sapling for free, bottom tile at x, y (the tile above the box). Same steps as vanilla's Player.TryReplantingTree: the tile is first turned
        // into our sapling by CheckAndInjectModSapling (it reads the soil under y), then placed through TileObject so the frame and the random variant are right.
        public static bool TryReplant(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 3))
                return false;

            Tile here = Main.tile[x, y];
            if (here.HasTile && !Main.tileCut[here.TileType])
                return false;   // something is still standing there (a leftover stump, a block)

            int tileToCreate = TileID.Saplings;
            int style = 0;
            PlantLoader.CheckAndInjectModSapling(x, y, ref tileToCreate, ref style);
            if (tileToCreate == TileID.Saplings)
                return false;   // the soil carries no ModTree: nothing to plant

            if (!TileObject.CanPlace(x, y, tileToCreate, style, 1, out TileObject objectData))
                return false;

            if (!TileObject.Place(objectData))
                return false;

            WorldGen.SquareTileFrame(x, y);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x - 1, y - 2, 3, 4);

            return true;
        }
    }
}