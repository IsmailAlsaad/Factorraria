using Factorraria.Common.Liquids;
using Factorraria.Content.Items.Liquids.Motors;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Motors
{
    [LegacyName("MotorMK1Tile")]
    public class LeadMotorTile : MotorTileBase<LeadMotorTileEntity>
    {
        protected override PipeMaterial Material => PipeMaterial.Lead;
        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            RegisterItemDrop(ModContent.ItemType<LeadMotorItem>());
        }
    }
}