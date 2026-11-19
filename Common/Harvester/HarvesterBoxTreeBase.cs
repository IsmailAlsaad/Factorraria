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

        // Vanilla's trunk sheet (Tiles_5) holds every vanilla tree's trunk side by side. A non-forest tree without its own PNG cuts its slice out of it once,
        // at first use (forest, index 0, uses the sheet as is). Unverified: that the sheet is TrunkSheetCount equal slices and that the order matches TopStyle.
        // The log line printed when a slice is cut shows the sheet size, so both can be checked.
        protected virtual int TrunkSheetCount => 7;
        protected virtual int TrunkSheetIndex => TopStyle;

        bool hasOwnTrunk;
        Asset<Texture2D> trunkSlice;
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

            hasOwnTrunk = ModContent.RequestIfExists<Texture2D>(TrunkTexturePath, out trunk, AssetRequestMode.AsyncLoad);
            if (!hasOwnTrunk)
                trunk = ModContent.Request<Texture2D>("Terraria/Images/Tiles_" + TileID.Trees, AssetRequestMode.AsyncLoad);

            tops = ModContent.Request<Texture2D>("Terraria/Images/Tree_Tops_" + TopStyle, AssetRequestMode.AsyncLoad);
            branches = ModContent.Request<Texture2D>("Terraria/Images/Tree_Branches_" + TopStyle, AssetRequestMode.AsyncLoad);
        }

        public override Asset<Texture2D> GetTexture()
        {
            if (hasOwnTrunk || TrunkSheetIndex <= 0)
                return trunk;

            if (trunkSlice != null)
                return trunkSlice;

            if (!trunk.IsLoaded)
                return trunk;   // still loading: the first frames draw the whole sheet

            Texture2D sheet = trunk.Value;
            int count = System.Math.Max(1, TrunkSheetCount);
            int width = sheet.Width / count;
            int index = System.Math.Min(TrunkSheetIndex, count - 1);
            ModLoader.GetMod("Factorraria").Logger.Info($"[Harvester] {GetType().Name}: trunk sheet {sheet.Width}x{sheet.Height}, slice {index} of {count} = {width}px wide, from x {index * width}");

            var data = new Microsoft.Xna.Framework.Color[width * sheet.Height];
            sheet.GetData(0, new Microsoft.Xna.Framework.Rectangle(index * width, 0, width, sheet.Height), data, 0, data.Length);

            // The sheet is premultiplied and the PNG loader premultiplies again, so undo it before saving.
            for (int p = 0; p < data.Length; p++)
            {
                Microsoft.Xna.Framework.Color c = data[p];
                if (c.A > 0 && c.A < 255)
                    data[p] = new Microsoft.Xna.Framework.Color(System.Math.Min(255, c.R * 255 / c.A), System.Math.Min(255, c.G * 255 / c.A), System.Math.Min(255, c.B * 255 / c.A), (int)c.A);
            }

            using var raw = new Texture2D(Main.instance.GraphicsDevice, width, sheet.Height);
            raw.SetData(data);
            using var stream = new System.IO.MemoryStream();
            raw.SaveAsPng(stream, width, sheet.Height);
            stream.Position = 0;
            trunkSlice = Main.Assets.CreateUntracked<Texture2D>(stream, ".png");
            return trunkSlice;
        }

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