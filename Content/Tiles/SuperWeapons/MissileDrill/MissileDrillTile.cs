using Factorraria.Common.SuperWeapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileDrill
{
    // The missile's drill head (super weapons patch S2.1). Tile footprint 2 wide x 3 tall (32x48 px); the art is 48x48 and drawn facing right,
    // so its tip sticks 16 px out past the footprint on the facing side. Style 0 faces right, style 1 (alternate placement) faces left, picked by
    // the way the player faces when placing. No TileEntity, no state: placed, mined and synced by vanilla tile code. The tile's own draw is off
    // (PreDraw returns false); OverhangDrawSystem draws the whole art after the tiles, see its header.
    //
    // Art (Ismail): Content/Tiles/SuperWeapons/MissileDrill/MissileDrill.png, 48x48, facing right, tip on the right, the 32 left pixels are the
    // footprint. Until it exists the part draws as a grey footprint block with a lighter tip block.
    public class MissileDrillTile : ModTile
    {
        // Only here so the loader has a sheet to request (avoids the vanilla Tile_105 lookup); PreDraw returns false, so it is never drawn.
        public override string Texture => "Factorraria/Content/Tiles/SuperWeapons/MissileDrill/MissileDrill";

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileSolid[Type] = false;
            TileID.Sets.DisableSmartCursor[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorTop = AnchorData.Empty;       // D6: parts can be built anywhere
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
            TileObjectData.newTile.AnchorLeft = AnchorData.Empty;
            TileObjectData.newTile.AnchorRight = AnchorData.Empty;
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.StyleMultiplier = 1;
            TileObjectData.newTile.Direction = TileObjectDirection.PlaceRight;   // style 0 = facing right
            TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
            TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceLeft;
            TileObjectData.addAlternate(1);                                       // style 1 = facing left
            TileObjectData.addTile(Type);

            DustType = DustID.Demonite;
            AddMapEntry(new Color(150, 150, 160), CreateMapEntryName());
            RegisterItemDrop(ModContent.ItemType<MissileDrillItem>());   // no style list = fallback for both facings

            ModContent.GetInstance<OverhangDrawSystem>().Register(Type, "Factorraria/Content/Tiles/SuperWeapons/MissileDrill/MissileDrill", 2, 3, 48, 48, 0, 0);
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
        {
            return false;   // OverhangDrawSystem draws the part
        }
    }
}