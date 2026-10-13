using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    public static class MagicMaterializerRegistry
    {
        public static readonly LiquidFuelTable LiquidFuels = new();

        // Shimmer's liquid id only exists after PostSetupContent, so this runs from PostAddRecipes like the other registries.
        public static void BuildRecipes()
        {
            LiquidFuels.Clear();
            LiquidFuels.Add(LiquidTypeRegistry.Shimmer, 1f);
        }
    }
}