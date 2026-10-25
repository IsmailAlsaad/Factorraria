using Factorraria.Common.Machines;
using Factorraria.Content.Items.Machines;
using Microsoft.Xna.Framework;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    public class WoodenPowerPoleTile : PowerPoleTileBase
    {
        protected override int PoleHeight => 14;
        protected override string ArtName => "WoodenPowerPole";
        public override PowerPoleTileEntityBase PlacementEntity => ModContent.GetInstance<WoodenPowerPoleTileEntity>();
        protected override int DropItemType => ModContent.ItemType<WoodenPowerPoleItem>();
        protected override Color MapColor => new Color(140, 100, 60);
        protected override int BreakDust => DustID.WoodFurniture;
    }
}