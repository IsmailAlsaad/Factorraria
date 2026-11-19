using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox;
using Terraria;

namespace Factorraria.Common.Harvester
{
    // Phase 5 (docs/harvester_box/ROADMAP.md 6g). One ModTree per box tile: the soil is the box, so a tree on it has its own wood, acorns, shake loot and look.
    // The trunk is read from our own PNG when it exists (the same layout as vanilla's Tile_5.png), otherwise from vanilla's forest trunk; tops and
    // branches use vanilla sheets (Tree_Tops_N / Tree_Branches_N). Every tunable is expression-bodied.
    public abstract class HarvesterBoxTreeBase<TTile> : ModTree where TTile : ModTile
    {
        protected abstract HarvesterBoxKind Kind { get; }
        protected abstract TreeTypes TreeKind { get; }        // which vanilla shake loot table the tree uses
        protected abstract int WoodItem { get; }
        protected abstract int SaplingRow { get; }            // the row of vanilla's sapling sheet (the sapling style): 0 forest, 1 snow, 2 jungle, 3 corrupt, 4 crimson
        protected abstract int TopStyle { get; }              // N of Images/Tree_Tops_N and Images/Tree_Branches_N

        protected virtual int TopFrameWidth => 80;
        protected virtual int TopFrameHeight => 80;
        protected virtual string TrunkTexturePath => "Factorraria/Content/Tiles/Machines/ElectricalConsumers/HarvesterBox/" + Kind + "HarvesterBoxTree";

        Asset<Texture2D> trunk;
        Asset<Texture2D> tops;
        Asset<Texture2D> branches;

        // The same settings tModLoader's ExampleMod copied from vanilla's forest tree.
        public override TreePaintingSettings TreeShaderSettings => new TreePaintingSettings
        {
            UseSpecialGroups = true,
            SpecialGroupMinimalHueValue = 11f / 72f,
            SpecialGroupMaximumHueValue = 0.25f,
            SpecialGroupMinimumSaturationValue = 0.88f,
            SpecialGroupMaximumSaturationValue = 1f
        };

        public override void SetStaticDefaults()
        {
            int box = ModContent.TileType<TTile>();
            GrowsOnTileId = new int[] { box };
            HarvesterBoxTrees.TreeBoxes.Add(box);

            if (!ModContent.RequestIfExists<Texture2D>(TrunkTexturePath, out trunk, AssetRequestMode.AsyncLoad))
                trunk = ModContent.Request<Texture2D>("Terraria/Images/Tiles_" + TileID.Trees, AssetRequestMode.AsyncLoad);

            tops = ModContent.Request<Texture2D>("Terraria/Images/Tree_Tops_" + TopStyle, AssetRequestMode.AsyncLoad);
            branches = ModContent.Request<Texture2D>("Terraria/Images/Tree_Branches_" + TopStyle, AssetRequestMode.AsyncLoad);
        }

        public override Asset<Texture2D> GetTexture() => trunk;

        public override Asset<Texture2D> GetTopTextures() => tops;

        public override Asset<Texture2D> GetBranchTextures() => branches;

        public override TreeTypes CountsAsTreeType => TreeKind;

        public override int DropWood() => WoodItem;

        // An acorn placed on this box is turned into our sapling (PlantLoader.CheckAndInjectModSapling), in the vanilla row of this tree's biome.
        public override int SaplingGrowthType(ref int style)
        {
            style = SaplingRow;
            return ModContent.TileType<HarvesterBoxSapling>();
        }

        public override void SetTreeFoliageSettings(int i, int j, Tile tile, int xoffset, ref int treeFrame, int floorY, ref int topTextureFrameWidth, ref int topTextureFrameHeight)
        {
            topTextureFrameWidth = TopFrameWidth;
            topTextureFrameHeight = TopFrameHeight;
        }
    }
}