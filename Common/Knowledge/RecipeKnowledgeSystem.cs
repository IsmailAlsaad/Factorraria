using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// Which recipes this WORLD has learned. Discovery is world-level (shared by every player who
    /// joins the world) and does not depend on mods the host happens to have enabled.
    ///
    /// Two saved sets of catalog keys (Machine|recipe key, see RecipeCatalog.KeyFor):
    /// unlocked (read from a parchment) and crafted (a machine finished it). Hidden-by-default.
    /// The held-ingredient state lives in RecipeVisibility and is never saved.
    /// </summary>
    public class RecipeKnowledgeSystem : ModSystem
    {
        // Parchment unlocks. Entries are catalog keys: "<Machine>|<recipe key>" (see RecipeCatalog.KeyFor).
        // A parchment can unlock a recipe the world has not crafted.
        static readonly HashSet<string> unlocked = new HashSet<string>();

        public static bool IsUnlocked(string catalogKey) => catalogKey != null && unlocked.Contains(catalogKey);

        // Crafted recipes: a machine finished this recipe at least once in this world. Catalog keys, like `unlocked`.
        static readonly HashSet<string> crafted = new HashSet<string>();

        /// <summary>
        /// Bumped on every change to the unlocked/crafted sets and on world load/unload.
        /// UI polls it (one int compare per frame) to know when to rebuild, so no event plumbing is needed.
        /// </summary>
        public static int Version { get; private set; }

        public static bool IsCrafted(string catalogKey) => catalogKey != null && crafted.Contains(catalogKey);

        /// <summary>Marks one recipe as crafted by catalog key. Returns true if it was newly marked.</summary>
        public static bool MarkCrafted(string catalogKey)
        {
            if (string.IsNullOrEmpty(catalogKey) || !crafted.Add(catalogKey)) return false;
            Version++;
            return true;
        }

        public static IReadOnlyCollection<string> CraftedKeys => crafted;

        public static void ResetCrafted()
        {
            crafted.Clear();
            Version++;
        }

        /// <summary>Unlocks one recipe by catalog key. Returns true if it was newly unlocked.</summary>
        public static bool Unlock(string catalogKey)
        {
            if (string.IsNullOrEmpty(catalogKey) || !unlocked.Add(catalogKey)) return false;
            Version++;
            return true;
        }

        public static IReadOnlyCollection<string> UnlockedKeys => unlocked;

        public static void ResetUnlocked()
        {
            unlocked.Clear();
            Version++;
        }


        // Persistence: the unlocked and crafted sets. Absent = everything hidden.
        public override void SaveWorldData(TagCompound tag)
        {

            tag["UnlockedRecipeKeys"] = new List<string>(unlocked);
            tag["CraftedRecipeKeys"] = new List<string>(crafted);
        }

        public override void LoadWorldData(TagCompound tag)
        {
            unlocked.Clear();
            crafted.Clear();
            Version++;
            if (tag.ContainsKey("CraftedRecipeKeys"))
                foreach (string key in tag.Get<List<string>>("CraftedRecipeKeys")) crafted.Add(key);
            if (tag.ContainsKey("UnlockedRecipeKeys"))
                foreach (string key in tag.Get<List<string>>("UnlockedRecipeKeys")) unlocked.Add(key);
            // Legacy "KnownRecipeKeys" (the old show-everything system) is intentionally ignored.
        }

        public override void OnWorldUnload()
        {
            unlocked.Clear();
            crafted.Clear();
            Version++;
        }

        public override void Unload()
        {
            unlocked.Clear();
            crafted.Clear();
        }
    }
}