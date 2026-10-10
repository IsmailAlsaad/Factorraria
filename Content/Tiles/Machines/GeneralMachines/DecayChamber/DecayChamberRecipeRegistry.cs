using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.DecayChamber
{
    public static class DecayChamberRecipeRegistry
    {
        public static readonly RecipeBook Book = new();

        const float LiquidPerCraft = 50f;
        const int CraftTicks = 120;

        public static void BuildRecipes()
        {
            Book.Clear();

            // Crimson water corrupts a gold/platinum bar into a Crimtane bar
            AddInfusion(ItemID.GoldBar, LiquidTypeRegistry.CrimsonWater, ItemID.CrimtaneBar);
            AddInfusion(ItemID.PlatinumBar, LiquidTypeRegistry.CrimsonWater, ItemID.CrimtaneBar);

            // Corruption water corrupts a gold/platinum bar into a Demonite bar
            AddInfusion(ItemID.GoldBar, LiquidTypeRegistry.CorruptionWater, ItemID.DemoniteBar);
            AddInfusion(ItemID.PlatinumBar, LiquidTypeRegistry.CorruptionWater, ItemID.DemoniteBar);

            Book.Rebuild();
        }

        static void AddInfusion(int inputBar, int liquidType, int outputBar)
        {
            Book.Add(new CustomRecipe()
                .WithInput(inputBar, 1)
                .WithLiquidInput(liquidType, LiquidPerCraft)
                .WithOutput(outputBar, 1)
                .TakesTicks(CraftTicks));
        }
    }
}