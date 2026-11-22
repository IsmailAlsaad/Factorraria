using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Harvester
{
    // Base of every plain custom planter box (no power, no entity): a 1x1 ModTile that mimics a vanilla planter box. Platform-like top, merges sideways with boxes
    // of the SAME type and never vertically. Same frame rule as the Harvester Boxes (HarvesterBoxTileBase), minus the machine entity.
    // See docs/planter_boxes/ROADMAP.md.
    //
    // Art (Ismail), next to the tile class: <Name>Tile.png, 72x16: four 16x16 frames with 2 px gaps, at x = 0, 18, 36, 54:
    //   0 = lone box, 1 = left end (a box on its right), 2 = middle (a box on both sides), 3 = right end (a box on its left).
    // Until it exists the tile borrows the vanilla planter box sheet, so the mod loads without the art.
    public abstract class PlanterBoxTileBase<TItem> : ModTile where TItem : ModItem
    {
        public override string Texture => ModContent.HasAsset(base.Texture) ? base.Texture : "Terraria/Images/Tile_" + TileID.PlanterBox;

        // The tile whose break dust this box uses (its own grass or block, see PlanterBoxDust). Wood unless a box says otherwise.
        protected virtual int DustTile => TileID.WoodBlock;

        // The sound when it is hit and broken.
        protected virtual SoundStyle BreakSound => SoundID.Dig;

        // The vanilla soil this box stands in for in tree and plant rules (HarvesterBoxRegistry.BoxTreeSoil). 0 = it is no tree box. Only the Mushroom box says
        // anything yet (mushroom grass); Hallowed and Stone follow in Phase 5.
        protected virtual int TreeSoil => 0;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true; // like a vanilla planter box: stand on it, jump up through it from below
            Main.tileNoAttach[Type] = false;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.AnchorInvalidTiles = new int[] { };

            // Prevent DivideByZeroException when external mods query tile style
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.StyleWrapLimit = 1;

            // Clear default floor requirement (placement is checked in CanPlace)
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;

            // MUST be called after setting AnchorBottom
            TileObjectData.addTile(Type);

            DustType = DustID.WoodFurniture;
            HitSound = BreakSound;
            AddMapEntry(new Color(160, 110, 60), CreateMapEntryName());
            RegisterItemDrop(ModContent.ItemType<TItem>());

            // No plain custom planter box grows a herb: HarvesterBoxRules.HerbAllowedOnSoil answers false for every herb (planting is refused, a herb that is somehow
            // there breaks). The herb boxes are the vanilla planter boxes and the Harvester Boxes.
            HarvesterBoxRegistry.NoHerbBoxes.Add(Type);

            if (TreeSoil != 0)
                HarvesterBoxRegistry.BoxTreeSoil[Type] = TreeSoil;
        }

        // Replace the default dust with the dust of this box's own soil.
        public override bool CreateDust(int i, int j, ref int type)
        {
            PlanterBoxDust.Spawn(i, j, DustTile);
            return false;
        }

        public override bool CanPlace(int i, int j)
        {
            // Same rule as the Harvester Boxes and motors: a wall behind it, or any tile touching it. No floating placement in mid-air.
            if (Main.tile[i, j].WallType > WallID.None)
                return true;

            return Main.tile[i - 1, j].HasTile || Main.tile[i + 1, j].HasTile || Main.tile[i, j - 1].HasTile || Main.tile[i, j + 1].HasTile;
        }

        // Merging: the frame comes from the boxes of the same type on the left and right. Frames are tile data, so every client frames the same way.
        public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
        {
            bool left = IsSameBox(i - 1, j);
            bool right = IsSameBox(i + 1, j);
            int cell = left ? (right ? 2 : 3) : (right ? 1 : 0);

            Main.tile[i, j].TileFrameX = (short)(cell * 18);
            Main.tile[i, j].TileFrameY = 0;
            return false;
        }

        bool IsSameBox(int i, int j)
        {
            if (!WorldGen.InWorld(i, j, 1))
                return false;

            Tile tile = Main.tile[i, j];
            return tile.HasTile && tile.TileType == Type;
        }

        // Unhammerable: no slopes, no half blocks.
        public override bool Slope(int i, int j) => false;
    }
}