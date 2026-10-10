using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
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
    /// A third saved set, seenItems, records recipe-input item types the player has held at least once (scanned by RecipeVisibility).
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

        // Seen items: recipe-input item types the player has held at least once in this world. Saved by item name.
        // Once seen, an ingredient stays revealed even when it is no longer in the inventory (chest runs are fine).
        static readonly HashSet<int> seenItems = new HashSet<int>();

        /// <summary>Bumped when the seen set changes or is reloaded. RecipeVisibility uses it to know when to rebuild its caches.</summary>
        public static int SeenVersion { get; private set; }

        public static bool IsSeen(int itemType) => seenItems.Contains(itemType);

        public static IReadOnlyCollection<int> SeenItems => seenItems;

        /// <summary>Marks an item type as seen. Returns true if it was newly marked.</summary>
        public static bool MarkSeen(int itemType)
        {
            if (itemType <= 0 || !seenItems.Add(itemType)) return false;
            SeenVersion++;
            return true;
        }

        public static void ResetSeen()
        {
            seenItems.Clear();
            SeenVersion++;
        }


        // Persistence: the unlocked and crafted sets. Absent = everything hidden.
        public override void SaveWorldData(TagCompound tag)
        {

            tag["UnlockedRecipeKeys"] = new List<string>(unlocked);
            tag["CraftedRecipeKeys"] = new List<string>(crafted);

            List<string> seenNames = new List<string>();
            foreach (int type in seenItems)
            {
                string name = ItemID.Search.GetName(type);
                if (!string.IsNullOrEmpty(name)) seenNames.Add(name);
            }
            tag["SeenItemKeys"] = seenNames;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            unlocked.Clear();
            crafted.Clear();
            seenItems.Clear();
            SeenVersion++;
            Version++;
            if (tag.ContainsKey("CraftedRecipeKeys"))
                foreach (string key in tag.Get<List<string>>("CraftedRecipeKeys")) crafted.Add(key);
            if (tag.ContainsKey("UnlockedRecipeKeys"))
                foreach (string key in tag.Get<List<string>>("UnlockedRecipeKeys")) unlocked.Add(key);
            if (tag.ContainsKey("SeenItemKeys"))
                foreach (string name in tag.Get<List<string>>("SeenItemKeys"))
                    if (ItemID.Search.TryGetId(name, out int seenType)) seenItems.Add(seenType);   // item from a removed mod: silently dropped
            // Legacy "KnownRecipeKeys" (the old show-everything system) is intentionally ignored.
        }

        public override void OnWorldUnload()
        {
            unlocked.Clear();
            crafted.Clear();
            seenItems.Clear();
            SeenVersion++;
            Version++;
        }

        public override void Unload()
        {
            unlocked.Clear();
            crafted.Clear();
            seenItems.Clear();
        }
    }
}