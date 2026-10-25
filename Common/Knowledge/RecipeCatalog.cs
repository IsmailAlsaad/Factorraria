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
        /// <summary>One thing the book shows in the fuel slot: a single item or liquid, or a named group of items ("Any Wood").</summary>
        public class FuelEntry
        {
            public RecipeOutputKey Icon;        // the sprite shown
            public string LabelKey;             // null = the item/liquid's own name; else a Mods.Factorraria.RecipeBook.<LabelKey> text (a group)
            public int[] Items = new int[0];    // item types that make this entry count as held: one for a single item, every member for a group. Empty for liquids.
        }

        /// <summary>Several fuel items shown as ONE entry (for example every wood). Members the machine does not actually burn are ignored.</summary>
        public class FuelGroup
        {
            public readonly string LabelKey;    // RecipeBook.<LabelKey> in the hjson
            public readonly int IconItem;       // the sprite shown
            public readonly HashSet<int> Members;

            public FuelGroup(string labelKey, int iconItem, IEnumerable<int> members)
            {
                LabelKey = labelKey;
                IconItem = iconItem;
                Members = new HashSet<int>(members);
            }
        }

        static readonly Dictionary<string, List<FuelEntry>> fuelsByMachine = new Dictionary<string, List<FuelEntry>>();

        /// <summary>
        /// Tells the book which fuels a machine burns. Pass the registry's own tables (null = that kind has none).
        /// groups = items to show as one entry; the group sits where its first member was in the table.
        /// Call after BuildRecipes(), so the tables are already filled. Liquid ids must already exist (PostAddRecipes is fine).
        /// </summary>
        public static void RegisterFuels(string machineName, FuelTable itemFuels, LiquidFuelTable liquidFuels, params FuelGroup[] groups)
        {
            var list = new List<FuelEntry>();
            var emitted = new HashSet<FuelGroup>();

            if (itemFuels != null)
                foreach (int type in itemFuels.ItemTypes)
                {
                    FuelGroup group = null;
                    if (groups != null)
                        foreach (FuelGroup g in groups)
                            if (g.Members.Contains(type)) { group = g; break; }

                    if (group == null)
                    {
                        list.Add(new FuelEntry { Icon = RecipeOutputKey.ForItem(type), Items = new[] { type } });
                        continue;
                    }

                    if (!emitted.Add(group)) continue;
                    var members = new List<int>();
                    foreach (int t in itemFuels.ItemTypes)
                        if (group.Members.Contains(t)) members.Add(t);
                    list.Add(new FuelEntry { Icon = RecipeOutputKey.ForItem(group.IconItem), LabelKey = group.LabelKey, Items = members.ToArray() });
                }

            if (liquidFuels != null)
                foreach (int liquid in liquidFuels.LiquidTypes)
                    list.Add(new FuelEntry { Icon = RecipeOutputKey.ForLiquid(liquid) });

            fuelsByMachine[machineName] = list;
        }

        /// <summary>The machine's fuel entries (items first, then liquids), or null if none were registered.</summary>
        public static List<FuelEntry> FuelsOf(string machineName) =>
            machineName != null && fuelsByMachine.TryGetValue(machineName, out List<FuelEntry> list) ? list : null;

        /// <summary>Every item type that is a fuel of some machine. RecipeVisibility tracks these as "seen" so a held fuel reveals.</summary>
        public static IEnumerable<int> AllFuelItemTypes()
        {
            foreach (List<FuelEntry> list in fuelsByMachine.Values)
                foreach (FuelEntry fuel in list)
                    foreach (int type in fuel.Items) yield return type;
        }

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