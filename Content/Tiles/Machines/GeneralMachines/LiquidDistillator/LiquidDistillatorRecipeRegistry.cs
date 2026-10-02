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
                .WithLiquidOutput(LiquidTypeRegistry.Fuel, 75f)
                .TakesTicks(60));

            Book.Rebuild();
        }
    }
}
