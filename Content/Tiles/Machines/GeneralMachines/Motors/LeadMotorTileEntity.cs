using Factorraria.Common.Machines;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Motors
{
    [LegacyName("MotorMK1TileEntity")]
    public class LeadMotorTileEntity : MotorTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<LeadMotorTile>();
        public override float PumpStrength => 5 * 3600f;
        public override float PowerDemand => 30f;

        // Matches LeadPipeTile.MaxFlowRate — kept as a separate duplicated constant since
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

        protected override void OnAnimationFrameChanged(int newFrame, int previousFrame)
        {

        }
    }
}