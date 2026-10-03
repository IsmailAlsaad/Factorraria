using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Hellforge
{
    public static class HellforgeRecipeRegistry
    {
        public static readonly RecipeBook Book = new();
        public static readonly LiquidFuelTable LiquidFuels = new();

        public static void BuildRecipes()
        {
            Book.Clear();
            Book.ImportVanillaRecipes(TileID.Hellforge);
            Book.ImportVanillaRecipes(TileID.Furnaces);
            Book.Rebuild();

            LiquidFuels.Clear();
            RegisterLiquidFuels();
        }

        static void RegisterLiquidFuels()
        {
            // 75 lava = 1 fuel unit, and the forge burns 1 unit per craft -> 75 lava per craft.
            // To also accept another liquid, add a line, e.g. LiquidFuels.Add(LiquidTypeRegistry.Fuel, 10f);
            LiquidFuels.Add(LiquidTypeRegistry.Lava, 75f);
        }
    }
}
