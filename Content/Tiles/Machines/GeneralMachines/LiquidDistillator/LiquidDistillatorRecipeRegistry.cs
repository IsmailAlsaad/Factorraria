using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Content.Items.Materials;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.LiquidDistillator
{
    public static class LiquidDistillatorRecipeRegistry
    {
        public static readonly RecipeBook Book = new();

        public static void BuildRecipes()
        {
            Book.Clear();

            Book.Add(new CustomRecipe()
                .WithLiquidInput(LiquidTypeRegistry.Oil, 100f)
                .WithLiquidOutput(LiquidTypeRegistry.Fuel, 75f));

            // 3 Fireblossom + 100 of any water -> 50 lava
            Book.Add(new CustomRecipe()
                .WithInput(ItemID.Fireblossom, 3)
                .WithAnyWaterInput(100f)
                .WithLiquidOutput(LiquidTypeRegistry.Lava, 50f));

            // 3 Waterleaf -> 100 of the water of the biome the machine is in + 5 grass seeds
            Book.Add(new CustomRecipe()
                .WithInput(ItemID.Waterleaf, 3)
                .WithBiomeWaterOutput(100f)
                .WithOutput(ItemID.GrassSeeds, 5));

            Book.Rebuild();
        }
    }
}
