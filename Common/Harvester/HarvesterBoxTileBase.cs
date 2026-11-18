using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Harvester
{
    // Every Harvester Box (one per vanilla planter box) inherits this. A 1x1 ModTile with a powered machine entity, like the motors (MotorTileBase), that mimics a
    // vanilla planter box: platform-like top, merges sideways with boxes of the SAME type and never vertically.
    //
    // Why eight separate tiles and not one tile with styles: vanilla decides a tree's look and drops from the soil TILE TYPE under the trunk (roadmap 7c), so each box
    // needs its own tile id.
    //
    // Art (Ismail), next to the tile class: <Kind>HarvesterBoxTile.png, 72x16: four 16x16 frames with 2 px gaps, at x = 0, 18, 36, 54:
    //   0 = lone box (no box beside it), 1 = left end (a box on its right), 2 = middle (a box on both sides), 3 = right end (a box on its left).
    // Until it exists the tile borrows the vanilla planter box sheet, so the mod loads without the art.
    public abstract class HarvesterBoxTileBase<TEntity, TItem> : ModTile where TEntity : BaseMachine, new() where TItem : ModItem
    {
        protected abstract HarvesterBoxKind Kind { get; }

        public override string Texture => ModContent.HasAsset(base.Texture) ? base.Texture : "Terraria/Images/Tile_" + TileID.PlanterBox;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true; // like a vanilla planter box: stand on it, jump up through it from below
            Main.tileNoAttach[Type] = false;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<TEntity>().Hook_AfterPlacement, -1, 0, true);
            TileObjectData.newTile.AnchorInvalidTiles = new int[] { };

            // Prevent DivideByZeroException when external mods query tile style
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.StyleWrapLimit = 1;

            // Clear default floor requirement (placement is checked in CanPlace)
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;

            // MUST be called after setting AnchorBottom
            TileObjectData.addTile(Type);

            DustType = DustID.Silver;
            HitSound = SoundID.Tink;
            AddMapEntry(new Color(160, 110, 60), CreateMapEntryName());
            RegisterItemDrop(ModContent.ItemType<TItem>());

            HarvesterBoxRegistry.Register(Type, Kind);
        }

        public override bool CanPlace(int i, int j)
        {
            // Same rule as the motors: a wall behind it, or any tile touching it. No floating placement in mid-air.
            if (Main.tile[i, j].WallType > WallID.None)
                return true;

            return Main.tile[i - 1, j].HasTile || Main.tile[i + 1, j].HasTile || Main.tile[i, j - 1].HasTile || Main.tile[i, j + 1].HasTile;
        }

        // Merging: the frame comes from the boxes of the same type on the left and right (see the art note above). Terraria calls this for every tile around a
        // placed or broken tile, so neighbours reframe themselves. Frames are tile data, so every client frames the same way and nothing needs syncing.
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

        // Unhammerable: no slopes, no half blocks (the sprite and the herb rules assume a full tile).
        public override bool Slope(int i, int j) => false;
    }
}