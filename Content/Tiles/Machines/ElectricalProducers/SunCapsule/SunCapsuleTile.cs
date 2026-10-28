using Factorraria.Content.Items.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.SunCapsule
{
    // 2 wide, 3 tall. Drawn as an overhang canvas by MachineOverhangSystem (see the registration in MachineRegisterationSystem), so the Off sheet
    // only has to exist as the tile's own texture.
    public class SunCapsuleTile : ModTile
    {
        public override string Texture => "Factorraria/Content/Tiles/Machines/ElectricalProducers/SunCapsule/SunCapsule_Off";

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
            TileObjectData.newTile.Origin = new Point16(0, 2);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table, 2, 0);
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<SunCapsuleTileEntity>().Hook_AfterPlacement, -1, 0, true);
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(230, 170, 60), CreateMapEntryName());

            DustType = DustID.GoldCoin;
            RegisterItemDrop(ModContent.ItemType<SunCapsuleItem>());
        }
    }
}