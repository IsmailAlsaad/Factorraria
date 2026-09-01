using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.Autohammer
{
    public class AutohammerTileEntity : ElectricConsumerMachine
    {
        public override int ValidTileType => TileID.Autohammer;
        public override float PowerDemand => 100f;
        protected override int InputSlotCount => 1;
        protected override int OutputSlotCount => 1;

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
