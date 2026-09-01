using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.Furnace
{
    public struct RecipeData
    {
        public int InputItemCount;
        public int OutputItemID;
        public int OutputItemCount;

        public RecipeData(int _InputItemCount, int _OutputItemID, int _OutputItemCount)
        {
            InputItemCount = _InputItemCount;
            OutputItemID = _OutputItemID;
            OutputItemCount = _OutputItemCount;
        }
    }

    public static class FurnaceRecipeRegistry
    {
        // (int InputItemID, RecipeData
        public static Dictionary<int, RecipeData> SmeltingRecipes = new Dictionary<int, RecipeData>();
        // (int FuelItemID, int number of smelts)
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

                if(recipe.requiredItem.Count != 1)
                {
                    continue;
                }

                int InputItemID = recipe.requiredItem[0].type;
                int InputItemCount = recipe.requiredItem[0].stack;
                int OutputItemID = recipe.createItem.type;
                int OutputItemCount = recipe.createItem.stack;

                RecipeData data = new RecipeData(InputItemCount, OutputItemID, OutputItemCount);

                if (!SmeltingRecipes.ContainsKey(InputItemID))
                {
                    SmeltingRecipes.Add(InputItemID, data);
                }

                recipe.DisableRecipe();
            }

            RegisterManualRecipes();
            RegisterValidFuels();
        }

        static void RegisterManualRecipes()
        {
            // wood -> charcoal, plants -> ash
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
