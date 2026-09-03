using Factorraria.Common.Machines;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Liquids.Motors
{
    public class MotorMK1TileEntity : MotorTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<MotorMK1Tile>();
        public override float PumpStrength => 50f;
        public override float PowerDemand => 30f;

        public override void Update()
        {
            // isWorking == true is determined when the generator has valid & enough fuel to burn & product slot is not full, so count its power output
            // isOn is set to false by the PowerNetwork not the machine when the grid is overloaded, so stop consuming fuel and turn off, but you could still be working!
            // i.e. have enough fuel to work once the grid is not overloaded
            isWorking = true;

            if (!isOn)
            {
                return;
            }
        }

    }
}