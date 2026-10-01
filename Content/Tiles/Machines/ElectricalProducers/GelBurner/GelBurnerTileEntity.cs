using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GelBurner
{
    public class GelBurnerTileEntity : ElectricProducerMachine
    {
        public override int ValidTileType => TileID.SteampunkBoiler;
        public override float PowerSupply => 500f;

        // 1 gel = 20 seconds of power (the burner uses 1 unit per tick)
        public static readonly FuelTable GelFuels = CreateFuels();
        protected override FuelTable AcceptedFuels => GelFuels;

        static FuelTable CreateFuels()
        {
            FuelTable table = new FuelTable().Add(ItemID.Gel, 3 * 60);
            table.StackLimitRule = _ => 10;   // conveyors may buffer up to 10 gel
            return table;
        }
        public override void Update()
        {
            base.Update();

            // isWorking == true is determined when the generator has valid & enough fuel to burn & product slot is not full, so count its power output
            // isOn is set to false by the PowerNetwork not the machine when the grid is overloaded, so stop consuming fuel and turn off, but you could still be working!
            // i.e. have enough fuel to work once the grid is not overloaded
            // Supply only counts while there is gel to burn
            isWorking = Fuel.HasFuelAvailable(InputSlots);

            if (!isOn || !isWorking)
            {
                return;
            }

            Fuel.TryEnsureFuel(InputSlots);   // take a gel if the burner is empty
            Fuel.Consume();                   // burn 1 unit this tick
        }
    }
}
