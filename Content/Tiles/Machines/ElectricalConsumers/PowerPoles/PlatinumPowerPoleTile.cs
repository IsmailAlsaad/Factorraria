using Factorraria.Common.Machines;
using Factorraria.Content.Items.Machines;
using Microsoft.Xna.Framework;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    public class PlatinumPowerPoleTile : PowerPoleTileBase
    {
        protected override int PoleHeight => 16;
        protected override string ArtName => "PlatinumPowerPole";
        public override PowerPoleTileEntityBase PlacementEntity => ModContent.GetInstance<PlatinumPowerPoleTileEntity>();
        protected override int DropItemType => ModContent.ItemType<PlatinumPowerPoleItem>();
        protected override Color MapColor => new Color(170, 185, 200);
        protected override int BreakDust => DustID.Platinum;
    }
}