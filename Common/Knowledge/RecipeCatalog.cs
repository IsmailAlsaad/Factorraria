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
            string full = FullTabOf(machineName);
            if (full == machineName || splitStations.Count == 0 || !splitStations.Contains(machineName)) return full;

            // R1.6: a split station is its own tab until the machine that inherits it is discovered. Depends on the discovery state,
            // so this is no longer constant: never cache the result across frames.
            return RecipeTabDiscovery.IsOwnDiscovered(full) ? full : machineName;
        }

        /// <summary>The tab with every inherited station already merged in (what TabOf returned before R1.6). Static; discovery ids and tables are built from it.</summary>
        public static string FullTabOf(string machineName)
        {
            if (machineName == null) return null;
            if (tabOwner == null) BuildTabs();
            return tabOwner.TryGetValue(machineName, out string owner) ? owner : machineName;
        }

        static HashSet<string> splitStations = new HashSet<string>();   // inherited stations that stay their own tab until their owner is discovered (RecipeTabDefinitions.SplitUntilOwnerDiscovered). Built with tabOwner.

        /// <summary>True for a vanilla station that is its own tab until the machine inheriting it is discovered (Work Benches, Heavy Work Bench).</summary>
        public static bool IsSplitStation(string stationName)
        {
            if (stationName == null) return false;
            if (tabOwner == null) BuildTabs();
            return splitStations.Contains(stationName);
        }

        static Dictionary<string, List<string>> inherited = new Dictionary<string, List<string>>();   // machine tab -> the inherited units absorbed into it (a vanilla station, or a whole machine tab, RecipeTabDefinitions.Inherits / InheritedTabs). Built with tabOwner.
        static Dictionary<string, string> unitOf = new Dictionary<string, string>();                    // catalog name of a member (station, machine, or the machine's twin station) -> its inherited unit. Built with tabOwner.
        static readonly string[] NoStations = new string[0];

        static void BuildTabs()
        {
            tabOwner = new Dictionary<string, string>();
            inherited = new Dictionary<string, List<string>>();
            unitOf = new Dictionary<string, string>();
            splitStations = new HashSet<string>();
            foreach (KeyValuePair<string, string> twin in RecipeTabDefinitions.Twins)
                if (HasBook(twin.Key) && stationNames.Contains(twin.Value)) tabOwner[twin.Value] = twin.Key;

            // Twins first, so a station that is some machine's twin is never taken by an inheriting machine.
            foreach (KeyValuePair<string, int[]> inherit in RecipeTabDefinitions.Inherits)
            {
                if (!HasBook(inherit.Key)) continue;
                bool split = System.Array.IndexOf(RecipeTabDefinitions.SplitUntilOwnerDiscovered, inherit.Key) >= 0;
                foreach (int tile in inherit.Value)
                {
                    string station = VanillaStationBooks.NameFor(tile);
                    if (!stationNames.Contains(station) || tabOwner.ContainsKey(station)) continue;

                    tabOwner[station] = inherit.Key;
                    unitOf[station] = station;
                    if (split) splitStations.Add(station);
                    if (!inherited.TryGetValue(inherit.Key, out List<string> list)) inherited[inherit.Key] = list = new List<string>();
                    list.Add(station);
                }
            }

            // Whole tabs (Hellforge inherits the Furnace): the machine, and its twin station, join the owner's tab as ONE unit.
            foreach (KeyValuePair<string, string> pair in RecipeTabDefinitions.InheritedTabs)
            {
                string owner = pair.Key, unit = pair.Value;
                if (!HasBook(owner) || !HasBook(unit) || tabOwner.ContainsKey(unit)) continue;

                tabOwner[unit] = owner;
                unitOf[unit] = unit;
                foreach (KeyValuePair<string, string> twin in RecipeTabDefinitions.Twins)
                    if (twin.Key == unit && tabOwner.TryGetValue(twin.Value, out string twinOwner) && twinOwner == unit)
                    {
                        tabOwner[twin.Value] = owner;
                        unitOf[twin.Value] = unit;
                    }
                if (!inherited.TryGetValue(owner, out List<string> list)) inherited[owner] = list = new List<string>();
                list.Add(unit);
            }
        }

        /// <summary>The tabs (catalog names) that inherit something, in no particular order.</summary>
        public static IEnumerable<string> InheritingTabs()
        {
            if (tabOwner == null) BuildTabs();
            return inherited.Keys;
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
                if (inheritedList != null && unitOf.TryGetValue(m.Key, out string unit) && inheritedList.Contains(unit)) hits.Add(unit);
                else own = true;
            }

            if (own || hits.Count == 0) result.Add(tab);
            if (inheritedList != null)
                foreach (string station in inheritedList)
                    if (hits.Contains(station)) result.Add(station);
            return result;
        }

        /// <summary>One recipe of a product as the detail page lists it, with every machine of the tab that can do it (registration order).</summary>
        public class MergedRecipe
        {
            public CustomRecipe Recipe;
            public readonly List<string> Machines = new List<string>();
        }

        /// <summary>
        /// The recipes of one product inside the tab that `catalogMachine` is listed under: the machine's own group first, then the
        /// groups of the other machines and stations of the tab that make the same product. Identical recipes (same inputs, liquids and
        /// outputs) are ONE entry that remembers every machine that does it (the Furnace and the Hellforge both smelt an Iron Bar);
        /// a recipe only one machine has stays its own entry. The detail page cycles these and shows each machine's fuel.
        /// </summary>
        public static List<MergedRecipe> MergedRecipes(string catalogMachine, RecipeOutputGroup selected)
        {
            var result = new List<MergedRecipe>();
            if (catalogMachine == null || selected == null) return result;

            var bySignature = new Dictionary<string, MergedRecipe>();
            void Take(string machine, RecipeOutputGroup group)
            {
                foreach (CustomRecipe recipe in group.Recipes)
                {
                    string signature = Signature(recipe);
                    if (!bySignature.TryGetValue(signature, out MergedRecipe merged))
                    {
                        merged = new MergedRecipe { Recipe = recipe };
                        bySignature[signature] = merged;
                        result.Add(merged);
                    }
                    if (!merged.Machines.Contains(machine)) merged.Machines.Add(machine);
                }
            }

            string tab = TabOf(catalogMachine);
            Take(catalogMachine, selected);
            foreach (KeyValuePair<string, RecipeBook> m in machines)
            {
                if (m.Key == catalogMachine || TabOf(m.Key) != tab) continue;
                foreach (RecipeOutputGroup group in m.Value.Groups)
                    if (group.Key.Equals(selected.Key)) Take(m.Key, group);
            }

            var order = new List<string>();
            foreach (KeyValuePair<string, RecipeBook> m in machines) order.Add(m.Key);
            foreach (MergedRecipe merged in result)
                merged.Machines.Sort((a, b) => order.IndexOf(a).CompareTo(order.IndexOf(b)));
            return result;
        }

        static string Signature(CustomRecipe recipe)
        {
            var parts = new List<string>();
            foreach (RecipeIngredient i in recipe.Inputs) parts.Add("i" + i.Type + ":" + i.Stack);
            foreach (LiquidIngredient l in recipe.LiquidInputs) parts.Add("li" + l.LiquidType + ":" + l.Amount);
            parts.Sort(System.StringComparer.Ordinal);
            var outs = new List<string>();
            foreach (RecipeIngredient o in recipe.Outputs) outs.Add("o" + o.Type + ":" + o.Stack);
            foreach (LiquidIngredient l in recipe.LiquidOutputs) outs.Add("lo" + l.LiquidType + ":" + l.Amount);
            outs.Sort(System.StringComparer.Ordinal);
            return string.Join(",", parts) + ">" + string.Join(",", outs);
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