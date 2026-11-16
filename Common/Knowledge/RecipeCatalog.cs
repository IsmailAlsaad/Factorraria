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
            public bool Station;                  // a vanilla station tab (VanillaStationBooks): never picked by a whole-book scroll roll
        }

        static readonly List<KeyValuePair<string, RecipeBook>> machines = new List<KeyValuePair<string, RecipeBook>>();

        static readonly HashSet<string> stationNames = new HashSet<string>();

        public static void Register(string machineName, RecipeBook book)
        {
            if (book != null) machines.Add(new KeyValuePair<string, RecipeBook>(machineName, book));
            siblings = null;
            tabOwner = null;
        }

        /// <summary>Registers a vanilla station tab (see VanillaStationBooks). Same as Register, but its entries are flagged Station.</summary>
        public static void RegisterStation(string stationName, RecipeBook book)
        {
            if (book == null) return;
            stationNames.Add(stationName);
            Register(stationName, book);
        }

        public static bool IsStation(string machineName) => machineName != null && stationNames.Contains(machineName);

        // ---- book tabs: a machine and its vanilla twin station share one tab ----
        static Dictionary<string, string> tabOwner;   // vanilla station catalog name -> the machine whose tab it joins. Built on first use.

        /// <summary>
        /// The tab a catalog machine name is listed under in the recipe book. A vanilla station that has a machine twin
        /// (RecipeTabDefinitions.Twins, only when both are registered) returns the machine's name; everything else returns itself.
        /// Display only: catalog keys, saves and the crafted/seen rules never change.
        /// </summary>
        public static string TabOf(string machineName)
        {
            if (machineName == null) return null;
            if (tabOwner == null) BuildTabs();
            return tabOwner.TryGetValue(machineName, out string owner) ? owner : machineName;
        }

        static Dictionary<string, List<string>> inherited = new Dictionary<string, List<string>>();   // machine tab -> the station tabs absorbed into it (RecipeTabDefinitions.Inherits). Built with tabOwner.
        static readonly string[] NoStations = new string[0];

        static void BuildTabs()
        {
            tabOwner = new Dictionary<string, string>();
            inherited = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, string> twin in RecipeTabDefinitions.Twins)
                if (HasBook(twin.Key) && stationNames.Contains(twin.Value)) tabOwner[twin.Value] = twin.Key;

            // Twins first, so a station that is some machine's twin is never taken by an inheriting machine.
            foreach (KeyValuePair<string, int[]> inherit in RecipeTabDefinitions.Inherits)
            {
                if (!HasBook(inherit.Key)) continue;
                foreach (int tile in inherit.Value)
                {
                    string station = VanillaStationBooks.NameFor(tile);
                    if (!stationNames.Contains(station) || tabOwner.ContainsKey(station)) continue;

                    tabOwner[station] = inherit.Key;
                    if (!inherited.TryGetValue(inherit.Key, out List<string> list)) inherited[inherit.Key] = list = new List<string>();
                    list.Add(station);
                }
            }
        }

        /// <summary>The vanilla station tabs (catalog names) absorbed into this machine's tab, in RecipeTabDefinitions.Inherits order. Empty for most tabs.</summary>
        public static IReadOnlyList<string> InheritedStations(string tabName)
        {
            if (tabName == null) return NoStations;
            if (tabOwner == null) BuildTabs();
            if (inherited.TryGetValue(tabName, out List<string> list)) return list;
            return NoStations;
        }

        /// <summary>
        /// Who can make this product inside the tab that `catalogMachine` is listed under: the tab's own machine (or its twin station)
        /// first, then each inherited station that also makes it. Catalog names ("Autohammer", "VanillaAnvils"); the detail page shows
        /// them joined by "or". Never empty.
        /// </summary>
        public static List<string> StationsFor(string catalogMachine, RecipeOutputKey product)
        {
            var result = new List<string>();
            if (catalogMachine == null) return result;
            if (tabOwner == null) BuildTabs();

            string tab = TabOf(catalogMachine);
            inherited.TryGetValue(tab, out List<string> inheritedList);

            bool own = false;
            var hits = new HashSet<string>();
            foreach (KeyValuePair<string, RecipeBook> m in machines)
            {
                if (TabOf(m.Key) != tab || !BookMakes(m.Value, product)) continue;
                if (inheritedList != null && inheritedList.Contains(m.Key)) hits.Add(m.Key);
                else own = true;
            }

            if (own || hits.Count == 0) result.Add(tab);
            if (inheritedList != null)
                foreach (string station in inheritedList)
                    if (hits.Contains(station)) result.Add(station);
            return result;
        }

        static bool BookMakes(RecipeBook book, RecipeOutputKey product)
        {
            foreach (RecipeOutputGroup group in book.Groups)
                if (group.Key.Equals(product)) return true;
            return false;
        }

        static bool HasBook(string machineName)
        {
            foreach (KeyValuePair<string, RecipeBook> m in machines)
                if (m.Key == machineName) return true;
            return false;
        }

        public static void Clear()
        {
            machines.Clear();
            fuelsByMachine.Clear();
            stationNames.Clear();
            siblings = null;
            tabOwner = null;
        }

        // ---- the same product made at several stations/machines ----
        static Dictionary<string, string[]> siblings;   // catalog key -> keys of the OTHER groups that make the same product (only keys that have any). Built on first use.

        /// <summary>
        /// Catalog keys of the other recipe groups (any machine or vanilla station) that make the same product, or null if there are none.
        /// RecipeKnowledgeSystem.IsCrafted uses it: crafting a product anywhere counts as crafted everywhere.
        /// </summary>
        public static string[] SiblingKeys(string catalogKey)
        {
            if (string.IsNullOrEmpty(catalogKey)) return null;
            if (siblings == null) BuildSiblings();
            return siblings.TryGetValue(catalogKey, out string[] others) ? others : null;
        }

        static void BuildSiblings()
        {
            var byProduct = new Dictionary<string, List<string>>();
            foreach (Entry e in Entries())
            {
                string product = RecipeKey.For(e.Group);
                if (!byProduct.TryGetValue(product, out List<string> members)) byProduct[product] = members = new List<string>();
                members.Add(e.Key);
            }

            siblings = new Dictionary<string, string[]>();
            foreach (List<string> members in byProduct.Values)
            {
                if (members.Count < 2) continue;
                foreach (string key in members)
                {
                    var others = new List<string>(members.Count - 1);
                    foreach (string other in members)
                        if (other != key) others.Add(other);
                    siblings[key] = others.ToArray();
                }
            }
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
                    yield return new Entry { Machine = m.Key, Group = group, Key = KeyFor(m.Key, group), Station = stationNames.Contains(m.Key) };
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
            {
                if (e.Station) continue;   // thousands of vanilla recipes would drown the machine recipes; a pool entry can still target one
                if (!RecipeKnowledgeSystem.IsUnlocked(e.Key) && !RecipeKnowledgeSystem.IsCrafted(e.Key)) pool.Add(e);
            }

            if (pool.Count == 0) { catalogKey = null; return false; }
            catalogKey = pool[rand.Next(pool.Count)].Key;
            return true;
        }
    }
}