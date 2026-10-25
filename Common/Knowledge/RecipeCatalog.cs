using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria.Utilities;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// Every machine's RecipeBook under a stable machine name. Lets discovery code (scrolls, parchments,
    /// later the book UI) turn a saved key back into a real recipe group and roll a random one.
    ///
    /// Catalog key = "&lt;Machine&gt;|&lt;RecipeKey&gt;", e.g. "Furnace|i|IronBar". One group = one product on one machine.
    /// Filled in MachineRegisterationSystem.PostAddRecipes after every BuildRecipes().
    /// </summary>
    public static class RecipeCatalog
    {
        public class Entry
        {
            public string Machine;
            public RecipeOutputGroup Group;
            public string Key;
            public RecipeArtRegistry.Entry Art;   // art entries only (Group is null then); returned by EntriesWithArt(), never by Entries()
        }

        static readonly List<KeyValuePair<string, RecipeBook>> machines = new List<KeyValuePair<string, RecipeBook>>();

        public static void Register(string machineName, RecipeBook book)
        {
            if (book != null) machines.Add(new KeyValuePair<string, RecipeBook>(machineName, book));
        }

        public static void Clear()
        {
            machines.Clear();
            fuelsByMachine.Clear();
        }

        // ---- fuels shown by the recipe book (read from each machine's registry tables, never from a CustomRecipe) ----
        static readonly Dictionary<string, List<RecipeOutputKey>> fuelsByMachine = new Dictionary<string, List<RecipeOutputKey>>();

        /// <summary>
        /// Tells the book which fuels a machine burns. Pass the registry's own tables (null = that kind has none).
        /// Call after BuildRecipes(), so the tables are already filled. Liquid ids must already exist (PostAddRecipes is fine).
        /// </summary>
        public static void RegisterFuels(string machineName, FuelTable itemFuels, LiquidFuelTable liquidFuels)
        {
            var list = new List<RecipeOutputKey>();
            if (itemFuels != null)
                foreach (int type in itemFuels.ItemTypes) list.Add(RecipeOutputKey.ForItem(type));
            if (liquidFuels != null)
                foreach (int liquid in liquidFuels.LiquidTypes) list.Add(RecipeOutputKey.ForLiquid(liquid));
            fuelsByMachine[machineName] = list;
        }

        /// <summary>The machine's fuels (items first, then liquids), or null if none were registered.</summary>
        public static List<RecipeOutputKey> FuelsOf(string machineName) =>
            machineName != null && fuelsByMachine.TryGetValue(machineName, out List<RecipeOutputKey> list) ? list : null;

        // ---- art entries (see RecipeArtRegistry) ----

        /// <summary>Every recipe entry, then every art entry (Group == null, Art set). Only the recipe book list wants the art ones.</summary>
        public static IEnumerable<Entry> EntriesWithArt()
        {
            foreach (Entry e in Entries()) yield return e;
            foreach (RecipeArtRegistry.Entry art in RecipeArtRegistry.All)
                yield return new Entry { Machine = RecipeArtRegistry.Machine, Group = null, Key = art.Key, Art = art };
        }

        /// <summary>True if the key names a real recipe OR a registered art entry. Use this where a key only has to be valid (unlock checks, pool validation).</summary>
        public static bool Exists(string catalogKey) =>
            TryResolve(catalogKey, out _, out _) || RecipeArtRegistry.Contains(catalogKey);

        public static string KeyFor(string machineName, RecipeOutputGroup group) => machineName + "|" + RecipeKey.For(group);

        /// <summary>Same key without needing a group object (config files, crafted marking).</summary>
        public static string KeyFor(string machineName, RecipeOutputKey key) => machineName + "|" + RecipeKey.For(key);

        /// <summary>Reverse lookup: the catalog machine name a RecipeBook was registered under, or null if it is not in the catalog.</summary>
        public static string NameOf(RecipeBook book)
        {
            if (book == null) return null;
            foreach (KeyValuePair<string, RecipeBook> m in machines)
                if (ReferenceEquals(m.Value, book)) return m.Key;
            return null;
        }

        public static IEnumerable<Entry> Entries()
        {
            foreach (KeyValuePair<string, RecipeBook> m in machines)
                foreach (RecipeOutputGroup group in m.Value.Groups)
                    yield return new Entry { Machine = m.Key, Group = group, Key = KeyFor(m.Key, group) };
        }

        /// <summary>Resolves a saved key. False if the recipe no longer exists (removed or renamed in a later version).</summary>
        public static bool TryResolve(string catalogKey, out string machineName, out RecipeOutputGroup group)
        {
            machineName = null;
            group = null;
            if (string.IsNullOrEmpty(catalogKey)) return false;
            foreach (Entry e in Entries())
            {
                if (e.Key != catalogKey) continue;
                machineName = e.Machine;
                group = e.Group;
                return true;
            }
            return false;
        }

        /// <summary>The machine part of a key without resolving it (safe for tooltips).</summary>
        public static string MachineOf(string catalogKey)
        {
            if (string.IsNullOrEmpty(catalogKey)) return null;
            int bar = catalogKey.IndexOf('|');
            return bar < 0 ? null : catalogKey.Substring(0, bar);
        }

        /// <summary>
        /// Picks a random recipe (uniform) the world has neither unlocked by parchment nor crafted.
        /// </summary>
        public static bool TryRollLocked(UnifiedRandom rand, out string catalogKey)
        {
            var pool = new List<Entry>();
            foreach (Entry e in Entries())
                if (!RecipeKnowledgeSystem.IsUnlocked(e.Key) && !RecipeKnowledgeSystem.IsCrafted(e.Key)) pool.Add(e);

            if (pool.Count == 0) { catalogKey = null; return false; }
            catalogKey = pool[rand.Next(pool.Count)].Key;
            return true;
        }
    }
}