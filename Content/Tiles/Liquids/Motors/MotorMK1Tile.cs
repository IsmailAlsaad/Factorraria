using Factorraria.Common.Liquids;
using Factorraria.Content.Items.Liquids.Motors;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Liquids.Motors
{
    public class MotorMK1Tile : MotorTileBase<MotorMK1TileEntity>
    {
        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            RegisterItemDrop(ModContent.ItemType<MotorMK1Item>());
        }
    }
}