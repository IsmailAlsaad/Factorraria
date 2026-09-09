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
        bool debugLiquidsSeeded = false;

        public override void Update()
        {
            if (!debugLiquidsSeeded)
            {
                InputLiquids[0] = new LiquidStack();
                InputLiquids[1] = new LiquidStack();
                debugLiquidsSeeded = true;
            }

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

        int counter = 0;
        public override void OnRightClick(int i, int j)
        {
            counter = (counter + 1) % 5;

            switch (counter)
            {
                case 0:
                    InputLiquids[0] = new LiquidStack(LiquidTypeRegistry.Water, 50f);
                    break;
                case 1:
                    InputLiquids[0] = new LiquidStack(LiquidTypeRegistry.Lava, 50f);
                    break;
                case 2:
                    InputLiquids[0] = new LiquidStack(LiquidTypeRegistry.Honey, 50f);
                    break;
                case 3:
                    InputLiquids[0] = new LiquidStack(LiquidTypeRegistry.Shimmer, 50f);
                    break;
                case 4:
                    InputLiquids[0] = new LiquidStack(LiquidTypeRegistry.Oil, 50f);
                    break;
            }
        }
    }
}
