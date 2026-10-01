using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;

namespace Factorraria.Common.Machines
{
    public class RecipeBook
    {
        public List<CustomRecipe> All { get; } = new();
        public List<RecipeOutputGroup> Groups { get; } = new();   // same list instance forever; the UI holds a reference to it
        public int MaxIngredientCount { get; private set; } = 0;
        public int MinIngredientCount { get; private set; } = 0;

        public RecipeBook Add(CustomRecipe recipe) { All.Add(recipe); return this; }

        // pull every vanilla recipe that needs this crafting station, convert it, and disable the vanilla one.
        public void ImportVanillaRecipes(int requiredTile)
        {
            for (int i = 0; i < Main.recipe.Length; i++)
            {
                Recipe recipe = Main.recipe[i];
                if (recipe == null || !recipe.requiredTile.Contains(requiredTile)) continue;

                Add(new CustomRecipe(recipe.requiredItem, recipe.createItem));
                recipe.DisableRecipe();
            }
        }

        // Call once after the last Add. Rebuilds the browser groups and the ingredient-slot count.
        public void Rebuild()
        {
            Groups.Clear();
            Groups.AddRange(RecipeOutputGroup.Build(All));

            MaxIngredientCount = 0;
            MinIngredientCount = int.MaxValue;
            foreach (CustomRecipe r in All)
            {
                int n = r.Inputs.Count;
                MaxIngredientCount = Math.Max(MaxIngredientCount, n);
                if (n > 0) MinIngredientCount = Math.Min(MinIngredientCount, n);  // liquid-only recipes must not drag the min to 0
            }
            if (MinIngredientCount == int.MaxValue) MinIngredientCount = 0;       // no recipe uses items at all
        }

        public void Clear() { All.Clear(); Groups.Clear(); MaxIngredientCount = 0; MinIngredientCount = 0; }
    }
}
