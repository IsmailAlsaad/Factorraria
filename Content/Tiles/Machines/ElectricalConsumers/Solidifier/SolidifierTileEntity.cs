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
        //bool debugLiquidsSeeded = false;

        public override void Update()
        {
            //if (!debugLiquidsSeeded)
            //{
            //    InputLiquids[0] = new LiquidStack(LiquidTypeRegistry.Lava, 50f);
            //    InputLiquids[1] = new LiquidStack(LiquidTypeRegistry.Water, 50f);
            //    debugLiquidsSeeded = true;
            //}

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
