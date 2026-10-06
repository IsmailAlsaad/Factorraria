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
    /// A recipe is a KEY = one recipe group (one output type), e.g. "i|Terraria/IronBar".
    /// Learn() it when a machine finishes that recipe; the UI then stops hiding it.
    /// Hidden-by-default: a fresh world knows nothing until something is crafted.
    /// </summary>
    public class RecipeKnowledgeSystem : ModSystem
    {
        static readonly HashSet<string> known = new HashSet<string>();

        // Parchment unlocks. Entries are catalog keys: "<Machine>|<recipe key>" (see RecipeCatalog.KeyFor).
        // Separate from `known` so a parchment can unlock a recipe the world has not crafted.
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

        /// <summary>True if the world has learned this output group. UI uses this to filter the browser.</summary>
        public static bool IsKnown(RecipeOutputGroup group) => group != null && known.Contains(RecipeKey.For(group));

        public static bool IsKnown(RecipeOutputKey key) => known.Contains(RecipeKey.For(key));

        /// <summary>Learns a single output group (the world "discovers" that recipe).</summary>
        public static void Learn(RecipeOutputGroup group) => Learn(group.Key);

        /// <summary>Learns one output key. Called when a machine finishes a craft.</summary>
        public static void Learn(RecipeOutputKey key)
        {
            if (!known.Add(RecipeKey.For(key))) return;
            if (!Main.dedServ) Main.NewText($"Discovered a new recipe: {key.DisplayName}", 110, 220, 110);
            // TODO (later phase): send a packet so the host can tell clients too.
        }

        /// <summary>Learns every group in a book. Safe to call once per machine book after recipes are built.</summary>
        public static void LearnBook(RecipeBook book)
        {
            if (book == null) return;
            foreach (RecipeOutputGroup group in book.Groups) Learn(group);
        }

        public static void Forget(RecipeOutputKey key) => known.Remove(RecipeKey.For(key));

        // Persistence: just the list of learned keys. Absent = everything hidden.
        public override void SaveWorldData(TagCompound tag)
        {
            tag["KnownRecipeKeys"] = new List<string>(known);
            tag["UnlockedRecipeKeys"] = new List<string>(unlocked);
            tag["CraftedRecipeKeys"] = new List<string>(crafted);
        }

        public override void LoadWorldData(TagCompound tag)
        {
            known.Clear();
            unlocked.Clear();
            crafted.Clear();
            Version++;
            if (tag.ContainsKey("CraftedRecipeKeys"))
                foreach (string key in tag.Get<List<string>>("CraftedRecipeKeys")) crafted.Add(key);
            if (tag.ContainsKey("UnlockedRecipeKeys"))
                foreach (string key in tag.Get<List<string>>("UnlockedRecipeKeys")) unlocked.Add(key);
            if (!tag.ContainsKey("KnownRecipeKeys")) return;
            foreach (string key in tag.Get<List<string>>("KnownRecipeKeys")) known.Add(key);
        }

        public override void OnWorldUnload()
        {
            known.Clear();
            unlocked.Clear();
            crafted.Clear();
            Version++;
        }

        public override void Unload()
        {
            known.Clear();
            unlocked.Clear();
            crafted.Clear();
        }
    }
}