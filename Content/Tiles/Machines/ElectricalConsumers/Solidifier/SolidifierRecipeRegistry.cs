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
                .WithAnyWaterInput(100f)
                .WithLiquidInput(LiquidTypeRegistry.Oil, 25f)
                .WithOutput(ItemID.AsphaltBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithAnyWaterInput(100f)
                .WithLiquidInput(LiquidTypeRegistry.Honey, 50f)
                .WithOutput(ItemID.HoneyBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithAnyWaterInput(100f)
                .WithLiquidInput(LiquidTypeRegistry.Shimmer, 50f)
                .WithOutput(ItemID.ShimmerBlock, 1)
                .TakesTicks(60));


            // Biome specific water recipes
            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.Water, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.StoneBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.UndergroundWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.SiltBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.DesertWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.DesertFossil, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.OasisWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.DesertFossil, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.JungleWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.MudstoneBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.SnowWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.SlushBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.CorruptionWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.EbonstoneBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.CrimsonWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.CrimstoneBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.HallowWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.PearlstoneBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.CavernWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.SiltBlock, 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.OceanWater, 50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.StoneBlock, 1)
                .TakesTicks(60));


            Book.Add(new CustomRecipe()
                .WithAnyWaterInput(50f)
                .WithLiquidInput(LiquidTypeRegistry.Lava, 25f)
                .WithOutput(ItemID.StoneBlock, 1)
                .TakesTicks(60));

            Book.Rebuild();
        }
    }
}
