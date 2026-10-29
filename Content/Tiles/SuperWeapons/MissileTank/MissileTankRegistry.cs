using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileTank
{
    public static class MissileTankRegistry
    {
        // Fuel only. The amount per unit does not matter (nothing burns it here); the launch in a later patch reads the tank directly.
        public static readonly LiquidFuelTable LiquidFuels = new LiquidFuelTable();

        // Called from MachineRegisterationSystem.PostAddRecipes, after LiquidTypeRegistrationSystem has assigned LiquidTypeRegistry.Fuel.
        public static void Build()
        {
            LiquidFuels.Clear();
            LiquidFuels.Add(LiquidTypeRegistry.Fuel, 1f);
        }
    }
}