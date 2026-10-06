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
        }

        static readonly List<KeyValuePair<string, RecipeBook>> machines = new List<KeyValuePair<string, RecipeBook>>();

        public static void Register(string machineName, RecipeBook book)
        {
            if (book != null) machines.Add(new KeyValuePair<string, RecipeBook>(machineName, book));
        }

        public static void Clear() => machines.Clear();

        public static string KeyFor(string machineName, RecipeOutputGroup group) => machineName + "|" + RecipeKey.For(group);

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
        /// Picks a random recipe the world has not unlocked by parchment yet.
        /// NOTE: it does not look at crafted recipes yet; that split arrives when the Phase 1 knowledge layer is completed.
        /// </summary>
        public static bool TryRollLocked(UnifiedRandom rand, out string catalogKey)
        {
            var pool = new List<Entry>();
            foreach (Entry e in Entries())
                if (!RecipeKnowledgeSystem.IsUnlocked(e.Key)) pool.Add(e);

            if (pool.Count == 0) { catalogKey = null; return false; }
            catalogKey = pool[rand.Next(pool.Count)].Key;
            return true;
        }
    }
}