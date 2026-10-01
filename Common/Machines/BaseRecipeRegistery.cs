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
        readonly HashSet<Recipe> imported = new();   // a recipe needing two imported stations must not be added twice
        public List<CustomRecipe> All { get; } = new();
        public List<RecipeOutputGroup> Groups { get; } = new();   // same list instance forever; the UI holds a reference to it
        public int MaxIngredientCount { get; private set; } = 0;
        public int MinIngredientCount { get; private set; } = 0;

        readonly HashSet<int> inputLiquidTypes = new();
        public bool ConsumesLiquid(int liquidType) => inputLiquidTypes.Contains(liquidType);

        public virtual RecipeBook Add(CustomRecipe recipe) { All.Add(recipe); return this; }

        // Pull vanilla recipes that need this station into the book.
        //   filter:           extra per-call test, false = skip that recipe
        //   disableOriginals: false = copy it but keep the vanilla recipe craftable by hand
        public void ImportVanillaRecipes(int requiredTile, Func<Recipe, bool> filter = null, bool disableOriginals = true)
        {
            for (int i = 0; i < Main.recipe.Length; i++)
            {
                Recipe recipe = Main.recipe[i];
                if (recipe == null || !recipe.requiredTile.Contains(requiredTile)) continue;
                if (imported.Contains(recipe)) continue;
                if (!ShouldImport(recipe) || (filter != null && !filter(recipe))) continue;

                foreach (CustomRecipe converted in ConvertVanillaRecipe(recipe))   // one vanilla recipe can now become several
                    Add(converted);
                imported.Add(recipe);

                if (disableOriginals && ShouldDisableOriginal(recipe)) recipe.DisableRecipe();
            }
        }

        // Per-book policy hooks. Override in a RecipeBook subclass.
        protected virtual bool ShouldImport(Recipe recipe) => true;
        protected virtual bool ShouldDisableOriginal(Recipe recipe) => true;
        const int MaxGroupExpansion = 256;   // safety cap on how many CustomRecipes one vanilla recipe may become

        // One vanilla recipe -> one CustomRecipe per combination of group members.
        protected virtual IEnumerable<CustomRecipe> ConvertVanillaRecipe(Recipe recipe)
        {
            var types = new List<int>();            // the representative item per ingredient
            var stacks = new List<int>();
            var options = new List<List<int>>();    // every item type that can fill that ingredient
            long combos = 1;

            foreach (Item req in recipe.requiredItem)
            {
                if (req == null || req.IsAir) continue;
                types.Add(req.type);
                stacks.Add(req.stack);
                List<int> alts = AlternativesFor(recipe, req.type);
                options.Add(alts);
                combos *= alts.Count;
            }

            if (combos > MaxGroupExpansion)         // pathological recipe: fall back to the representative items
            {
                options.Clear();
                foreach (int t in types) options.Add(new List<int> { t });
            }

            var result = new List<CustomRecipe>();
            ExpandInto(result, recipe, options, stacks, new int[types.Count], 0);
            return result;
        }

        static List<int> AlternativesFor(Recipe recipe, int representativeType)
        {
            var alts = new List<int>();
            foreach (int groupId in recipe.acceptedGroups)
            {
                if (!RecipeGroup.recipeGroups.TryGetValue(groupId, out RecipeGroup group)) continue;
                if (!group.ValidItems.Contains(representativeType)) continue;   // this group belongs to a different ingredient

                foreach (int type in group.ValidItems)
                    if (!alts.Contains(type)) alts.Add(type);
            }
            if (alts.Count == 0) alts.Add(representativeType);                  // plain, non-group ingredient
            return alts;
        }

        static void ExpandInto(List<CustomRecipe> result, Recipe recipe, List<List<int>> options, List<int> stacks, int[] picked, int depth)
        {
            if (depth == options.Count)
            {
                var inputs = new List<RecipeIngredient>();
                for (int i = 0; i < picked.Length; i++)
                    inputs.Add(new RecipeIngredient(picked[i], stacks[i]));

                result.Add(new CustomRecipe(inputs, new RecipeIngredient(recipe.createItem.type, recipe.createItem.stack)));
                return;
            }

            foreach (int type in options[depth])
            {
                if (Array.IndexOf(picked, type, 0, depth) >= 0) continue;   // the same item in two slots would break exact-slot matching
                picked[depth] = type;
                ExpandInto(result, recipe, options, stacks, picked, depth + 1);
            }
        }

        // Call once after the last Add. Rebuilds the browser groups and the ingredient-slot count.
        public virtual void Rebuild()
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
            inputLiquidTypes.Clear();
            foreach (CustomRecipe r in All)
                foreach (LiquidIngredient need in r.LiquidInputs)
                    inputLiquidTypes.Add(need.LiquidType);

            if (MinIngredientCount == int.MaxValue) MinIngredientCount = 0;       // no recipe uses items at all
        }

        public void Clear() { All.Clear(); inputLiquidTypes.Clear(); Groups.Clear(); MaxIngredientCount = 0; MinIngredientCount = 0; imported.Clear(); }
    }
}
