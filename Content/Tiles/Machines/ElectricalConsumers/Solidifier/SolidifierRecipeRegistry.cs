using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.Solidifier
{
    public static class SolidifierRecipeRegistry
    {
        public static readonly RecipeBook Book = new();

        public static void BuildRecipes()
        {
            Book.Clear();

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.Water, 100f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 100f)
                .WithOutput(ItemID.Obsidian, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.Water, 100f)
                .WithLiquidInput(LiquidTypeRegistry.Oil, 25f)
                .WithOutput(ItemID.AsphaltBlock, 1)
                .TakesTicks(60));

            Book.Rebuild();
        }
    }
}
