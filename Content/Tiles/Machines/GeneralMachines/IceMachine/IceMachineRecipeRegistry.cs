using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Content.Items.Materials;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine
{
    public static class IceMachineRecipeRegistry
    {
        public static readonly RecipeBook Book = new();

        public static void BuildRecipes()
        {
            Book.Clear();

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.Lava, 50f)
                .WithOutput(ItemID.Obsidian, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithAnyWaterInput(100f)
                .WithOutput(ItemID.SlushBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.Oil, 50f)
                .WithOutput(ModContent.ItemType<CokeItem>(), 1)
                .TakesTicks(60));

            Book.Rebuild();
        }
    }
}
