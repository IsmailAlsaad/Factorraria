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
        DebugRevealed,     // RevealAllRecipes debug flag: faded in the machine browser only, never listed in the book
        FadedIngredient,   // the player has held an input of it at least once (saved, world-wide)
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

        // product -> catalog keys of every recipe group that MAKES it (item or liquid), in catalog order.
        static readonly Dictionary<RecipeOutputKey, List<string>> producerIndex = new Dictionary<RecipeOutputKey, List<string>>();

        // Catalog keys with at least one input the player has SEEN (held once in this world). Derived from
        // RecipeKnowledgeSystem.SeenItems and rebuilt when that set changes. The inventory is rescanned about once per second.
        static HashSet<string> heldKeys = new HashSet<string>();

        // Item types the player has seen that are an input of some recipe (same derivation as heldKeys).
        static HashSet<int> heldItems = new HashSet<int>();
        static uint lastHeldScan;
        static bool heldScanned;
        static int seenSyncVersion = int.MinValue;   // RecipeKnowledgeSystem.SeenVersion that heldKeys/heldItems were built from

        /// <summary>Bumped whenever the set of held-ingredient recipes changes. Pair with RecipeKnowledgeSystem.Version for UI rebuilds.</summary>
        public static int HeldVersion { get; private set; }

        /// <summary>Call once after every machine book is built and registered in RecipeCatalog.</summary>
        public static void BuildIngredientIndex()
        {
            ClearIndex();
            foreach (RecipeCatalog.Entry e in RecipeCatalog.Entries())
            {
                if (!producerIndex.TryGetValue(e.Group.Key, out List<string> makers))
                    producerIndex[e.Group.Key] = makers = new List<string>();
                makers.Add(e.Key);
            }

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
            producerIndex.Clear();
            heldKeys.Clear();
            heldItems.Clear();
            heldScanned = false;
            seenSyncVersion = int.MinValue;
            HeldVersion++;
        }

        /// <summary>Forces the next GetState to rescan the inventory (e.g. when a panel opens).</summary>
        public static void InvalidateHeld() => heldScanned = false;

        static void EnsureHeldScan()
        {
            if (Main.dedServ) return;
            if (heldScanned && seenSyncVersion == RecipeKnowledgeSystem.SeenVersion && Main.GameUpdateCount - lastHeldScan < 60) return;
            heldScanned = true;
            lastHeldScan = Main.GameUpdateCount;

            // Record every recipe input the player holds right now. Once seen it stays seen for this world,
            // even after the item leaves the inventory (the player may keep materials in a chest).
            Player player = Main.LocalPlayer;
            if (player != null)
            {
                // 0-49 main inventory + hotbar, 50-53 coins, 54-57 ammo. Slot 58 is the mouse slot (Main.mouseItem below).
                // Piggy bank, safe, void bag and equipment are deliberately not scanned.
                for (int i = 0; i < 58; i++) MarkIfIngredient(player.inventory[i]);
                MarkIfIngredient(Main.mouseItem);
            }

            if (seenSyncVersion != RecipeKnowledgeSystem.SeenVersion) RebuildSeenCaches();
        }

        static void MarkIfIngredient(Item item)
        {
            if (item == null || item.IsAir) return;
            if (!ingredientIndex.ContainsKey(item.type)) return;
            RecipeKnowledgeSystem.MarkSeen(item.type);
        }

        /// <summary>Rebuilds heldItems/heldKeys from the saved seen set (also covers world load and a debug reset).</summary>
        static void RebuildSeenCaches()
        {
            heldKeys.Clear();
            heldItems.Clear();
            foreach (int type in RecipeKnowledgeSystem.SeenItems)
            {
                if (!ingredientIndex.TryGetValue(type, out List<string> keys)) continue;
                heldItems.Add(type);
                foreach (string key in keys) heldKeys.Add(key);
            }
            seenSyncVersion = RecipeKnowledgeSystem.SeenVersion;
            HeldVersion++;
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
            if (ModContent.GetInstance<FurnaceOffsetConfig>().RevealAllRecipes) return RecipeState.DebugRevealed;
            return RecipeState.Hidden;
        }

        /// <summary>
        /// Runs the (throttled) inventory scan so HeldVersion stays current. A panel that reacts to held-item
        /// changes must call this every frame while it is visible, because the scan is otherwise lazy.
        /// </summary>
        public static void Poll() => EnsureHeldScan();

        /// <summary>True if the player has held this item type at least once in this world AND it is an input of some recipe. (Name kept from the old live-inventory rule.)</summary>
        public static bool IsItemHeld(int itemType)
        {
            EnsureHeldScan();
            return heldItems.Contains(itemType);
        }

        /// <summary>True if the player has held an input of this recipe at least once in this world.</summary>
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

        // ------------------------------------------------------------------------------------------
        // Recipe book detail page: which ingredients may be shown, and where an ingredient's own recipe lives
        // ------------------------------------------------------------------------------------------

        /// <summary>True if some recipe that makes this product (item or liquid) has been crafted in this world.</summary>
        public static bool IsProductCrafted(RecipeOutputKey product)
        {
            if (!producerIndex.TryGetValue(product, out List<string> makers)) return false;
            foreach (string key in makers)
                if (RecipeKnowledgeSystem.IsCrafted(key)) return true;
            return false;
        }

        /// <summary>An item ingredient may be shown once the player has held it at least once in this world OR has crafted it before.</summary>
        public static bool IsIngredientRevealed(int itemType) =>
            IsItemHeld(itemType) || IsProductCrafted(RecipeOutputKey.ForItem(itemType));

        /// <summary>
        /// Liquids cannot be held. A liquid some recipe makes stays hidden until it has been crafted; a liquid no recipe
        /// makes (world water, lava, ...) is raw and always shown.
        /// </summary>
        public static bool IsLiquidRevealed(int liquidType)
        {
            RecipeOutputKey k = RecipeOutputKey.ForLiquid(liquidType);
            return !producerIndex.ContainsKey(k) || IsProductCrafted(k);
        }

        /// <summary>
        /// Catalog key of a recipe that makes this product AND is listed in the recipe book (crafted, parchment or held).
        /// A crafted one wins over a faded one. Null if there is none. excludeKey (the recipe already on screen) is skipped.
        /// </summary>
        public static string FindBookRecipeFor(RecipeOutputKey product, string excludeKey)
        {
            if (!producerIndex.TryGetValue(product, out List<string> makers)) return null;
            string fallback = null;
            foreach (string key in makers)
            {
                if (key == excludeKey) continue;
                RecipeState s = GetState(key);
                if (s == RecipeState.Crafted) return key;
                if (fallback == null && InBook(s)) fallback = key;
            }
            return fallback;
        }

        /// <summary>The recipe book lists crafted, parchment-unlocked and held-ingredient recipes. DebugRevealed never appears.</summary>
        public static bool InBook(RecipeState state) =>
            state == RecipeState.Crafted || state == RecipeState.FadedParchment || state == RecipeState.FadedIngredient;
    }
}