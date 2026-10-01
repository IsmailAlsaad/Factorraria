using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine
{
    public static class IceMachineRecipeRegistry
    {
        public static readonly RecipeBook Book = new();

        public static void BuildRecipes()
        {
            Book.Clear();

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.Lava, 100f)
                .WithOutput(ItemID.Obsidian, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.Water, 100f)
                .WithOutput(ItemID.SlushBlock, 1)
                .TakesTicks(60));

            Book.Rebuild();
        }
    }
}
