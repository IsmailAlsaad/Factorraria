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
    /// One place that answers "should the player see this recipe in a machine browser?".
    /// Today: only recipes the world has learned, plus a debug reveal-all. Kept as its own class so a
    /// later phase can add more rules (creative mode, knowledge items, ...) without touching the UI.
    /// </summary>
    public static class RecipeVisibility
    {
        public static bool IsVisible(RecipeOutputGroup group) =>
            RecipeKnowledgeSystem.IsKnown(group) ||
            ModContent.GetInstance<FurnaceOffsetConfig>().RevealAllRecipes;   // debug: show everything

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

        /// <summary>The recipe book lists only parchment-unlocked and crafted recipes.</summary>
        public static bool InBook(RecipeState state) => state == RecipeState.Crafted || state == RecipeState.FadedParchment;
    }
}