using Factorraria.Common.Machines;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Motors
{
    public class IronMotorTileEntity : MotorTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<IronMotorTile>();
        public override float PumpStrength => 10 * 3600f;   // placeholder balance: twice the lead motor
        public override float PowerDemand => 60f;           // placeholder balance: twice the lead motor

        // Matches IronPipeTile.MaxFlowRate (duplicated on purpose, same reason as the lead motor).
        public override float PipeEquivalentMaxFlowRate => 40f * 3600f;

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