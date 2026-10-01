using Factorraria.Common.Machines;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace
{
    public static class FurnaceRecipeRegistry
    {
        public static readonly RecipeBook Book = new();
        public static readonly FuelTable Fuels = new();

        public static void BuildFromExistingRecipes()
        {
            Book.Clear();
            Book.ImportVanillaRecipes(TileID.Furnaces);
            RegisterManualRecipes();
            Book.Rebuild();

            Fuels.Clear();
            RegisterManualFuels();
            Fuels.StackLimitRule = units => Math.Max(10 - units, 1);
        }

        static void RegisterManualRecipes()
        {
            if (RecipeGroup.recipeGroups.TryGetValue(RecipeGroupID.Wood, out RecipeGroup woodGroup))
            {
                foreach (int itemID in woodGroup.ValidItems)
                {
                    Book.Add(new CustomRecipe(new List<Item> { new Item(itemID, 3) }, new Item(ItemID.Coal, 1)));
                }
            }
        }

        static void RegisterManualFuels()
        {
            Fuels.Add(ItemID.Gel, 3);
            Fuels.Add(ItemID.Coal, 8);
        }
    }
}
