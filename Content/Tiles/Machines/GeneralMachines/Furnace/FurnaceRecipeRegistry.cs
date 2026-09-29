using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace
{
    public static class FurnaceRecipeRegistry
    {
        // List<Item> InputItems, Item OutputItem
        public static List<CustomRecipe> SmeltingRecipes = new List<CustomRecipe>();

        // int FuelItemID, int number of smelts
        public static Dictionary<int, int> ValidFuels = new Dictionary<int, int>();

        public static void BuildFromExistingRecipes()
        {
            SmeltingRecipes.Clear();

            for (int i = 0; i < Main.recipe.Length; i++)
            {
                Recipe recipe = Main.recipe[i];

                if(recipe == null)
                {
                    continue;
                }

                if (!recipe.requiredTile.Contains(TileID.Furnaces))
                {
                    continue;
                }

                CustomRecipe recipeData = new CustomRecipe(recipe.requiredItem, recipe.createItem);
                SmeltingRecipes.Add(recipeData);

                recipe.DisableRecipe();
            }

            RegisterManualRecipes();
            RegisterValidFuels();
        }

        static void RegisterManualRecipes()
        {
            // any wood -> coal
            if(RecipeGroup.recipeGroups.TryGetValue(RecipeGroupID.Wood, out RecipeGroup woodGroup))
            {
                foreach (int itemID in woodGroup.ValidItems)
                {
                    Item item = new Item(itemID, 3);
                    
                    SmeltingRecipes.Add(new CustomRecipe(new List<Item> { item }, new Item(ItemID.Coal, 1)));
                }
            }

            // any plant -> ash
        }

        static void RegisterValidFuels()
        {
            ValidFuels = new Dictionary<int, int>
            {
                {ItemID.Gel, 3},
                {ItemID.Coal, 8}
            };
        }
    }
}
