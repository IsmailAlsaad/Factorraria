using Factorraria.Common.Liquids;
using Factorraria.Content.Items.Liquids.Motors;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Motors
{
    public class IronMotorTile : MotorTileBase<IronMotorTileEntity>
    {
        protected override PipeMaterial Material => PipeMaterial.Iron;

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            RegisterItemDrop(ModContent.ItemType<IronMotorItem>());
        }
    }
}