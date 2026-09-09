using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.Solidifier
{
    public class SolidifierTileEntity : ElectricConsumerMachine
    {
        public override int ValidTileType => TileID.Solidifier;
        public override float PowerDemand => 100f;

        protected override int InputLiquidCount => 2;
        public override void Update()
        {
            // isWorking == true is determined when the generator has valid & enough fuel to burn & product slot is not full, so count its power output
            // isOn is set to false by the PowerNetwork not the machine when the grid is overloaded, so stop consuming fuel and turn off, but you could still be working!
            // i.e. have enough fuel to work once the grid is not overloaded
            
            if(InputLiquids[0] != null && InputLiquids[1] != null && InputLiquids[0].Amount > 0 && InputLiquids[1].Amount > 0)
            {
                isWorking = true;
            }
            else
            {
                isWorking = false;
            }

            if (!isOn)
            {
                return;
            }
        }
    }
}
