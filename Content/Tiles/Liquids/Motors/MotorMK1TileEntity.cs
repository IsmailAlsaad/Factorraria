using Factorraria.Common.Machines;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Liquids.Motors
{
    public class MotorMK1TileEntity : MotorTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<MotorMK1Tile>();
        public override float PumpStrength => 50f;
        public override float PowerDemand => 30f;
    }
}