using Factorraria.Common.Machines;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.General_Machines.Motors
{
    public class MotorMK1TileEntity : MotorTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<MotorMK1Tile>();
        public override float PumpStrength => 5 * 3600f;
        public override float PowerDemand => 30f;

        // Matches PipeMK1Tile.MaxFlowRate — kept as a separate duplicated constant since
        // that property lives on a different ModTile instance and isn't cheaply shared.
        public override float PipeEquivalentMaxFlowRate => 20f * 3600f;

        protected override void UpdateMotor()
        {
            isWorking = true;

            if (!isOn)
            {
                return;
            }
        }
    }
}