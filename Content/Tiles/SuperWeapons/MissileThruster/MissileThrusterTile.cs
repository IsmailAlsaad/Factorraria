using Factorraria.Common.SuperWeapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileThruster
{
    // One of the missile's three thrusters (super weapons patch S2.3). Tile footprint 1x1 (16x16 px); the art is 32x16 and drawn facing right, so
    // a rear plate sticks 16 px out BEHIND the footprint (the footprint is the right half of the art, the plate the left half). Style 0 faces right,
    // style 1 (alternate placement) faces left, picked by the way the player faces when placing, exactly like the drill. No TileEntity, no state:
    // placed, mined and synced by vanilla tile code; OverhangDrawSystem draws the art after the tiles (see its header).
    //
    // Art (Ismail): Content/Tiles/SuperWeapons/MissileThruster/MissileThruster.png, 32x16, facing right: the right 16 px are the footprint
    // (the nozzle side of the weapon's back face), the left 16 px stick out behind it. Until it exists the part draws as a grey footprint block
    // with a lighter block for the rear plate.
    public class MissileThrusterTile : ModTile
    {
        // Only here so the loader has a sheet to request (avoids the vanilla Tile_105 lookup); PreDraw returns false, so it is never drawn.
        public override string Texture => "Factorraria/Content/Tiles/SuperWeapons/MissileThruster/MissileThruster";

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileSolid[Type] = false;
            TileID.Sets.DisableSmartCursor[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
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

            DustType = DustID.Iron;
            AddMapEntry(new Color(150, 150, 160), CreateMapEntryName());
            RegisterItemDrop(ModContent.ItemType<MissileThrusterItem>());   // no style list = fallback for both facings

            ModContent.GetInstance<OverhangDrawSystem>().Register(Type, "Factorraria/Content/Tiles/SuperWeapons/MissileThruster/MissileThruster", 1, 1, 32, 16, -16, 0);
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
        {
            return false;   // OverhangDrawSystem draws the part
        }
    }
}