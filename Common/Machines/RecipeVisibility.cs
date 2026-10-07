using Factorraria.Common.Knowledge;
using Factorraria.Content.Configs;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Machines
{
    /// <summary>Resolved state of one recipe group for the local player. Higher value = more revealed.</summary>
    public enum RecipeState
    {
        Hidden,
        FadedIngredient,   // holding an input right now (live, per player, never saved)
        FadedParchment,    // unlocked by a parchment (saved, world-wide)
        Crafted            // a machine finished it (saved, world-wide)
    }

    /// <summary>
    /// One place that answers "how much of this recipe may the local player see?" (see RecipeState).
    /// Used by the recipe book and the machine browsers. Kept as its own class so later rules
    /// (creative mode, knowledge items, ...) never touch the UI.
    /// </summary>
    public static class RecipeVisibility
    {

        // ------------------------------------------------------------------------------------------
        // Four-state resolver (roadmap section 3). The recipe book uses it now; the browser panel in Phase 5.
        // ------------------------------------------------------------------------------------------

        // itemType -> catalog keys of every recipe group that takes it as an INPUT (fuel is not an input).
        static readonly Dictionary<int, List<string>> ingredientIndex = new Dictionary<int, List<string>>();

        // Catalog keys whose inputs the local player holds, rescanned about once per second.
        static HashSet<string> heldKeys = new HashSet<string>();
        static HashSet<string> scratchKeys = new HashSet<string>();
        static uint lastHeldScan;
        static bool heldScanned;

        /// <summary>Bumped whenever the set of held-ingredient recipes changes. Pair with RecipeKnowledgeSystem.Version for UI rebuilds.</summary>
        public static int HeldVersion { get; private set; }

        /// <summary>Call once after every machine book is built and registered in RecipeCatalog.</summary>
        public static void BuildIngredientIndex()
        {
            ClearIndex();
            foreach (RecipeCatalog.Entry e in RecipeCatalog.Entries())
                foreach (CustomRecipe recipe in e.Group.Recipes)
                    foreach (RecipeIngredient input in recipe.Inputs)
                    {
                        if (!ingredientIndex.TryGetValue(input.Type, out List<string> keys))
                            ingredientIndex[input.Type] = keys = new List<string>();
                        if (!keys.Contains(e.Key)) keys.Add(e.Key);
                    }
        }

        public static void ClearIndex()
        {
            ingredientIndex.Clear();
            heldKeys.Clear();
            heldScanned = false;
            HeldVersion++;
        }

        /// <summary>Forces the next GetState to rescan the inventory (e.g. when a panel opens).</summary>
        public static void InvalidateHeld() => heldScanned = false;

        static void EnsureHeldScan()
        {
            if (Main.dedServ) return;
            if (heldScanned && Main.GameUpdateCount - lastHeldScan < 60) return;
            heldScanned = true;
            lastHeldScan = Main.GameUpdateCount;

            scratchKeys.Clear();
            Player player = Main.LocalPlayer;
            if (player != null)
            {
                // 0-49 main inventory + hotbar, 50-53 coins, 54-57 ammo. Slot 58 is the mouse slot (Main.mouseItem below).
                // Piggy bank, safe, void bag and equipment are deliberately not scanned.
                for (int i = 0; i < 58; i++) AddHeld(player.inventory[i]);
                AddHeld(Main.mouseItem);
            }

            if (!scratchKeys.SetEquals(heldKeys)) HeldVersion++;
            (heldKeys, scratchKeys) = (scratchKeys, heldKeys);
        }

        static void AddHeld(Item item)
        {
            if (item == null || item.IsAir) return;
            if (!ingredientIndex.TryGetValue(item.type, out List<string> keys)) return;
            foreach (string key in keys) scratchKeys.Add(key);
        }

        public static RecipeState GetState(string machineName, RecipeOutputGroup group) =>
            GetState(RecipeCatalog.KeyFor(machineName, group));

        /// <summary>First match wins: Crafted, FadedParchment, FadedIngredient, Hidden. Never writes to the saved sets.</summary>
        public static RecipeState GetState(string catalogKey)
        {
            if (RecipeKnowledgeSystem.IsCrafted(catalogKey)) return RecipeState.Crafted;
            if (RecipeKnowledgeSystem.IsUnlocked(catalogKey)) return RecipeState.FadedParchment;

            EnsureHeldScan();
            if (heldKeys.Contains(catalogKey)) return RecipeState.FadedIngredient;

            // Debug reveal: at least faded in the browser. It never touches the saved sets and never reaches the book (see InBook).
            if (ModContent.GetInstance<FurnaceOffsetConfig>().RevealAllRecipes) return RecipeState.FadedIngredient;
            return RecipeState.Hidden;
        }

        /// <summary>
        /// Runs the (throttled) inventory scan so HeldVersion stays current. A panel that reacts to held-item
        /// changes must call this every frame while it is visible, because the scan is otherwise lazy.
        /// </summary>
        public static void Poll() => EnsureHeldScan();

        /// <summary>True if the local player holds an input of this recipe right now.</summary>
        public static bool IsHeld(string catalogKey)
        {
            EnsureHeldScan();
            return catalogKey != null && heldKeys.Contains(catalogKey);
        }

        /// <summary>
        /// Machine browser only: may the product's NAME be shown? Only when the recipe is Crafted. Every faded row
        /// (held ingredient, parchment, or the selected Hidden one) shows "???" until it has been crafted.
        /// The debug RevealAllRecipes flag still shows real names.
        /// </summary>
        public static bool IsNameRevealedInBrowser(string machineName, RecipeOutputGroup group, RecipeState state)
        {
            if (state == RecipeState.Crafted) return true;
            return ModContent.GetInstance<FurnaceOffsetConfig>().RevealAllRecipes;
        }

        /// <summary>The recipe book lists only parchment-unlocked and crafted recipes.</summary>
        public static bool InBook(RecipeState state) => state == RecipeState.Crafted || state == RecipeState.FadedParchment;
    }
}