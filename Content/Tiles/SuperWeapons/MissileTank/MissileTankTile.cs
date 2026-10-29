using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileTank
{
    // The missile's fuel tank (super weapons patch S2.2): 4 wide x 3 tall, a real ModTile with a MissileTankTileEntity. Plain static art, no
    // On/Off look: the same sheet is registered for both in MachineRegisterationSystem.
    //
    // Art (Ismail): Content/Tiles/SuperWeapons/MissileTank/MissileTank.png, the footprint's normal tile layout: 4x3 cells of 16 px with 2 px
    // gaps (72x54). Until it exists the tile borrows the vanilla statue sheet (also 72x54), so the mod loads without the art.
    public class MissileTankTile : ModTile
    {
        public const string ArtPath = "Factorraria/Content/Tiles/SuperWeapons/MissileTank/MissileTank";

        /// <summary>The sheet the tile and its machine registration use: the real art once it exists, else a vanilla placeholder.</summary>
        public static string SheetPath() => ModContent.HasAsset(ArtPath) ? ArtPath : "Terraria/Images/Tile_" + TileID.Statues;

        public override string Texture => SheetPath();

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileSolid[Type] = false;
            TileID.Sets.DisableSmartCursor[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorTop = AnchorData.Empty;       // D6: parts can be built anywhere
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
            TileObjectData.newTile.AnchorLeft = AnchorData.Empty;
            TileObjectData.newTile.AnchorRight = AnchorData.Empty;
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<MissileTankTileEntity>().Hook_AfterPlacement, -1, 0, true);
            TileObjectData.addTile(Type);

            DustType = DustID.Glass;
            AddMapEntry(new Color(150, 150, 160), CreateMapEntryName());
            RegisterItemDrop(ModContent.ItemType<MissileTankItem>());
        }
    }
}