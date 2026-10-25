using Factorraria.Common.Machines;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Utilities;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// One named list of recipes a scroll can teach. Entries are REFERENCES (machine name + product), not resolved
    /// recipes: they are looked up lazily because the RecipeCatalog is only filled in PostAddRecipes.
    /// Build pools in ScrollPoolDefinitions, never here.
    /// </summary>
    public class ScrollPool
    {
        public struct Entry
        {
            public string Machine;          // catalog machine name, e.g. "Furnace"
            public RecipeOutputKey Output;  // the product (item or liquid)
            public float Weight;            // relative, default 1
            public string ArtName;          // set for art entries (see RecipeArtRegistry); Machine and Output are unused then
            public string CatalogKey => ArtName != null ? RecipeArtRegistry.KeyFor(ArtName) : RecipeCatalog.KeyFor(Machine, Output);
        }

        readonly List<Entry> entries = new List<Entry>();

        public string Name { get; }
        public string Hint { get; private set; }
        public IReadOnlyList<Entry> Entries => entries;

        public ScrollPool(string name) { Name = name; }

        /// <summary>Vague tooltip line for scrolls/parchments of this pool. Never name the machine or recipe.</summary>
        public ScrollPool WithHint(string hint)
        {
            Hint = hint;
            return this;
        }

        /// <summary>Adds an item product made on the named machine.</summary>
        public ScrollPool Add(string machine, int itemType, float weight = 1f)
        {
            entries.Add(new Entry { Machine = machine, Output = RecipeOutputKey.ForItem(itemType), Weight = weight });
            return this;
        }

        /// <summary>Adds a liquid product made on the named machine (use LiquidTypeRegistry ids).</summary>
        public ScrollPool AddLiquid(string machine, int liquidType, float weight = 1f)
        {
            entries.Add(new Entry { Machine = machine, Output = RecipeOutputKey.ForLiquid(liquidType), Weight = weight });
            return this;
        }

        /// <summary>Adds a recipe book art entry (a PNG registered in RecipeArtDefinitions). artName = the PNG's file name without extension.</summary>
        public ScrollPool AddArt(string artName, float weight = 1f)
        {
            entries.Add(new Entry { Machine = RecipeArtRegistry.Machine, ArtName = artName, Weight = weight });
            return this;
        }
    }

    /// <summary>
    /// Registry of scroll pools + the roll used when a parchment is read.
    /// A scroll/parchment stores only its pool NAME (null = global). Roll order:
    ///   1. the pool's own recipes that are not unlocked and not crafted (weighted)
    ///   2. otherwise every recipe in the catalog that is not unlocked and not crafted (uniform)
    /// Nothing is "reserved" by a roll: a recipe only leaves the pool once it is unlocked (parchment read) or crafted,
    /// so throwing away an unread parchment loses nothing.
    /// </summary>
    public static class ScrollPools
    {
        static readonly Dictionary<string, ScrollPool> byName = new Dictionary<string, ScrollPool>(StringComparer.OrdinalIgnoreCase);
        static readonly List<ScrollPool> ordered = new List<ScrollPool>();

        public static IReadOnlyList<ScrollPool> All => ordered;

        /// <summary>Starts (or reopens) a pool. Name lookup ignores case.</summary>
        public static ScrollPool Define(string name, string hint = null)
        {
            if (byName.TryGetValue(name, out ScrollPool existing))
                return hint == null ? existing : existing.WithHint(hint);

            var pool = new ScrollPool(name).WithHint(hint);
            byName[name] = pool;
            ordered.Add(pool);
            return pool;
        }

        public static bool TryGet(string name, out ScrollPool pool)
        {
            pool = null;
            return name != null && byName.TryGetValue(name, out pool);
        }

        public static string HintFor(string poolName) =>
            TryGet(poolName, out ScrollPool pool) && !string.IsNullOrEmpty(pool.Hint) ? pool.Hint : null;

        public static void Clear()
        {
            byName.Clear();
            ordered.Clear();
        }

        /// <summary>Logs every pool entry that matches no catalog recipe (typo, missing machine, removed recipe). Call after the catalog is built.</summary>
        public static void Validate(Action<string> warn)
        {
            foreach (ScrollPool pool in ordered)
                foreach (ScrollPool.Entry e in pool.Entries)
                    if (!RecipeCatalog.Exists(e.CatalogKey))
                        warn($"[ScrollPools] pool '{pool.Name}': no recipe or art '{e.CatalogKey}' in the catalog (typo, unknown machine name, or the recipe/art does not exist). Entry skipped.");

            foreach (string problem in RecipeArtRegistry.Problems) warn("[ScrollPools] " + problem);

            foreach (RecipeArtRegistry.Entry art in RecipeArtRegistry.All)
            {
                if (!Main.dedServ && !Terraria.ModLoader.ModContent.HasAsset(art.TexturePath))
                    warn($"[ScrollPools] art '{art.Name}': no PNG at '{art.TexturePath}'. The book page will say the art is missing.");

                bool pooled = false;
                foreach (ScrollPool pool in ordered)
                    foreach (ScrollPool.Entry e in pool.Entries)
                        if (e.ArtName == art.Name) pooled = true;
                if (!pooled)
                    warn($"[ScrollPools] art '{art.Name}' is in no scroll pool, so no parchment can ever unlock it (add .AddArt(\"{art.Name}\") to a pool in ScrollPoolDefinitions).");
            }
        }

        /// <summary>Pool entries that resolve and are neither unlocked nor crafted, with their weights.</summary>
        static List<KeyValuePair<string, float>> Candidates(ScrollPool pool)
        {
            var result = new List<KeyValuePair<string, float>>();
            foreach (ScrollPool.Entry e in pool.Entries)
            {
                if (e.Weight <= 0f) continue;
                string key = e.CatalogKey;
                if (!RecipeCatalog.Exists(key)) continue;
                if (RecipeKnowledgeSystem.IsUnlocked(key) || RecipeKnowledgeSystem.IsCrafted(key)) continue;
                if (result.Exists(r => r.Key == key)) continue;   // same recipe listed twice
                result.Add(new KeyValuePair<string, float>(key, e.Weight));
            }
            return result;
        }

        /// <summary>How many of this pool's recipes are still available to roll.</summary>
        public static int RemainingCount(ScrollPool pool) => Candidates(pool).Count;

        /// <summary>
        /// Picks the recipe a parchment teaches. poolName null or unknown = global pool.
        /// False only if the whole book has nothing left to unlock.
        /// </summary>
        public static bool TryRoll(string poolName, UnifiedRandom rand, out string catalogKey)
        {
            if (TryGet(poolName, out ScrollPool pool))
            {
                List<KeyValuePair<string, float>> local = Candidates(pool);
                if (local.Count > 0)
                {
                    float total = 0f;
                    foreach (var c in local) total += c.Value;
                    float roll = rand.NextFloat() * total;
                    foreach (var c in local)
                    {
                        roll -= c.Value;
                        if (roll < 0f) { catalogKey = c.Key; return true; }
                    }
                    catalogKey = local[local.Count - 1].Key;   // float edge case
                    return true;
                }
            }

            // Pool exhausted (or no pool): any recipe in the whole book.
            return RecipeCatalog.TryRollLocked(rand, out catalogKey);
        }
    }
}