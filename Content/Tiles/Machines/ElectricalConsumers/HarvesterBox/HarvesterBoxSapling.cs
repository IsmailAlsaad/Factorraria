using Factorraria.Common.Harvester;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox
{
    // Phase 5a. The sapling a Harvester Box grows trees from. A hand-placed acorn turns into it automatically on a box (the box trees' SaplingGrowthType), and
    // the box plants it again after felling a tree. Its tile data is copied from vanilla's sapling (tile 20), so it uses vanilla's sheet and frame layout,
    // one row per biome (the sapling style). It has no RandomUpdate: only the box grows it (HarvesterBoxTileEntityBase.TickSapling).
    public class HarvesterBoxSapling : ModTile
    {
        public override string Texture => "Terraria/Images/Tiles_" + TileID.Saplings;

        public override void SetStaticDefaults()
        {
            // Use vanilla's own loaded sapling sheet; the paint system re-requests the asset by name and fails on a borrowed path.
            TextureAssets.Tile[Type] = TextureAssets.Tile[TileID.Saplings];

            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.GetTileData(TileID.Saplings, 0));

            // A box is solid-top (a planter box look-alike), which AnchorType.SolidTile alone rejects, so both kinds are allowed.
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.AnchorValidTiles = new int[]
            {
                ModContent.TileType<DaybloomHarvesterBoxTile>(),
                ModContent.TileType<BlinkrootHarvesterBoxTile>(),
                ModContent.TileType<MoonglowHarvesterBoxTile>(),
                ModContent.TileType<CorruptHarvesterBoxTile>(),
                ModContent.TileType<CrimsonHarvesterBoxTile>(),
                ModContent.TileType<ShiverthornHarvesterBoxTile>()
            };
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(200, 200, 200), Language.GetText("MapObject.Sapling"));

            TileID.Sets.TreeSapling[Type] = true;
            TileID.Sets.CommonSapling[Type] = true;
            TileID.Sets.SwaysInWindBasic[Type] = true;

            DustType = DustID.Grass;
            AdjTiles = new int[] { TileID.Saplings };
            RegisterItemDrop(ItemID.Acorn);
        }

        public override void NumDust(int i, int j, bool fail, ref int num)
        {
            num = fail ? 1 : 3;
        }

        // Vanilla's way (same as ExampleMod): a chance on each random tile update, then WorldGen.GrowTree applies vanilla's own space and neighbour checks.
        public override void RandomUpdate(int i, int j)
        {
            if (!WorldGen.genRand.NextBool(20))
                return;

            HarvesterBoxTrees.TryGrow(i, j);
        }
    }
}