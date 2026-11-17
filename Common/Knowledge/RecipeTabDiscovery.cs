using Factorraria.Common.Net;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// Recipe book tab discovery: a tab (a machine, or a vanilla crafting station; a machine and its vanilla twin are ONE tab, see
    /// RecipeCatalog.TabOf) stays out of the book until the world has discovered it. "By Hand" and Notes are always shown.
    ///
    /// A tab is discovered when ANY of these is true:
    ///  1. a player held an item that places the tab's tile (Work Bench item, Furnace item, Mechanical Assembler item ...),
    ///  2. a player stood within crafting reach of the tab's tile (the only way for a Demon Altar, or a Hellforge found in the world),
    ///  3. one of the tab's recipes is crafted or unlocked by a parchment (computed, never saved: old worlds lose nothing).
    /// 1 and 2 are a marker in the world's seen set (RecipeKnowledgeSystem.TabMarker), so they are saved, joined and broadcast by the
    /// existing SeenReport / SeenDelta / recipe-knowledge join provider, with no packet of their own. The client scans its own
    /// inventory and surroundings and reports; the server accepts a marker only if the sender really holds such an item or is near
    /// such a tile (it checks its own copy of the player and its own tile data). Single-player marks directly.
    /// </summary>
    public static class RecipeTabDiscovery
    {
        public class Tab
        {
            public string Name;                         // catalog tab name (RecipeCatalog.TabOf)
            public int Id;                              // stable discovery id (see RecipeKnowledgeSystem.TabMarkerBase)
            public int Tile;                            // the tile whose item / nearness discovers the tab
            public readonly HashSet<int> Items = new HashSet<int>();   // every item that places that tile
            public int[] ItemList = new int[0];         // the same items, sorted (the book header cycles through them)
            public bool Inherited;                      // a vanilla station absorbed into a machine's tab (RecipeTabDefinitions.Inherits)
            public bool Split;                          // Inherited only: its own tab until the owner is discovered (RecipeTabDefinitions.SplitUntilOwnerDiscovered)
            public string Owner;                        // Inherited only: the tab it was absorbed into
            public readonly List<Tab> Inherits = new List<Tab>();   // a machine tab only: the stations it inherits recipes from

            public int Marker => RecipeKnowledgeSystem.TabMarker(Id);
        }

        public const int ExtraIdBase = 100000;

        // Tunables (not const, so hot reload can change them).
        public static int ScanTicks = 30;          // client: how often the inventory and the surroundings are scanned
        public static int NearTilesX = 4;          // client: tiles left/right of the player counted as "near" (about vanilla's crafting reach; check Player.AdjTiles)
        public static int NearTilesY = 3;          // client: tiles above/below
        public static int ServerNearExtra = 3;     // server: extra tiles of tolerance on both axes (position lag), so an honest report is not refused

        static readonly Dictionary<string, Tab> byName = new Dictionary<string, Tab>();
        static readonly Dictionary<int, Tab> byId = new Dictionary<int, Tab>();
        static readonly Dictionary<int, Tab> byTile = new Dictionary<int, Tab>();
        static readonly Dictionary<int, Tab> byItem = new Dictionary<int, Tab>();

        static readonly int[] NoItems = new int[0];

        static readonly HashSet<string> unlockedMachines = new HashSet<string>();   // catalog names that own a parchment-unlocked recipe
        static int unlockedVersion = int.MinValue;

        public static IReadOnlyCollection<Tab> All => byName.Values;

        public static void Clear()
        {
            byName.Clear();
            byId.Clear();
            byTile.Clear();
            byItem.Clear();
            unlockedMachines.Clear();
            unlockedVersion = int.MinValue;
        }

        /// <summary>
        /// Builds the table after every machine book and station tab is registered in RecipeCatalog. Deterministic (the ids do not
        /// depend on load order), so the server and every client agree on the markers. Returns the number of discoverable tabs.
        /// </summary>
        public static int Build()
        {
            Clear();

            var seen = new HashSet<string>();
            foreach (RecipeCatalog.Entry e in RecipeCatalog.Entries())
            {
                string name = RecipeCatalog.FullTabOf(e.Machine);   // the merged name: the table is the same whether or not a station is split right now
                if (!seen.Add(name)) continue;
                if (!TryFindTile(name, out int tile, out int id)) continue;   // By Hand and tabs with no known tile are always shown
                if (byId.ContainsKey(id) || byTile.ContainsKey(tile)) continue;

                var tab = new Tab { Name = name, Id = id, Tile = tile };
                byName[name] = tab;
                byId[id] = tab;
                byTile[tile] = tab;
            }

            // Stations a machine inherits from (the Anvils of the Autohammer): each one is its own discoverable tile with its own
            // marker, filed under the machine's tab. Holding or standing near it makes the machine's tab appear.
            // A unit is a vanilla station (the Anvils) or a whole machine tab (the Furnace, discovered through its twin tile, Furnaces).
            foreach (string ownerName in RecipeCatalog.InheritingTabs())
            {
                if (!byName.TryGetValue(ownerName, out Tab owner)) continue;
                foreach (string station in RecipeCatalog.InheritedStations(ownerName))
                {
                    int tile = TileOfUnit(station);
                    if (tile < 0 || byId.ContainsKey(tile) || byTile.ContainsKey(tile) || byName.ContainsKey(station)) continue;

                    var sub = new Tab { Name = station, Id = tile, Tile = tile, Inherited = true, Owner = ownerName, Split = RecipeCatalog.IsSplitStation(station) };
                    byName[station] = sub;
                    byId[tile] = sub;
                    byTile[tile] = sub;
                    owner.Inherits.Add(sub);
                }
            }

            for (int type = 1; type < ItemLoader.ItemCount; type++)
            {
                if (!ContentSamples.ItemsByType.TryGetValue(type, out Item sample)) continue;
                if (sample.createTile >= 0 && byTile.TryGetValue(sample.createTile, out Tab tab))
                {
                    tab.Items.Add(type);
                    byItem[type] = tab;
                }
            }



            foreach (Tab tab in byName.Values)
            {
                var sorted = new List<int>(tab.Items);
                sorted.Sort();
                tab.ItemList = sorted.ToArray();
            }

            return byName.Count;
        }

        // The tile that discovers an inherited unit: a vanilla station's own tile, or a machine's twin tile (Furnace -> Furnaces).
        static int TileOfUnit(string name)
        {
            int tile = VanillaStationBooks.TileOf(name);
            if (tile >= 0) return tile;
            foreach (KeyValuePair<string, string> twin in RecipeTabDefinitions.Twins)
                if (twin.Key == name) return VanillaStationBooks.TileOf(twin.Value);
            return -1;
        }

        static bool TryFindTile(string name, out int tile, out int id)
        {
            tile = -1;
            id = -1;

            if (RecipeCatalog.IsStation(name))
            {
                tile = VanillaStationBooks.TileOf(name);
                id = tile;
                return tile >= 0;
            }

            foreach (KeyValuePair<string, string> twin in RecipeTabDefinitions.Twins)
            {
                if (twin.Key != name) continue;
                tile = VanillaStationBooks.TileOf(twin.Value);
                id = tile;
                return tile >= 0;
            }



            for (int i = 0; i < RecipeTabDefinitions.ExtraMachineTiles.Length; i++)
            {
                if (RecipeTabDefinitions.ExtraMachineTiles[i].Key != name) continue;
                tile = RecipeTabDefinitions.ExtraMachineTiles[i].Value();
                id = ExtraIdBase + i;
                return tile >= 0;
            }

            return false;
        }

        // ---------------------------------------------------------------- the question the book asks

        /// <summary>
        /// True if the book may list this tab: the tab's own machine/station was discovered (held or stood near), or so was a station
        /// it inherits recipes from. A crafted or parchment-unlocked recipe does NOT discover a tab. Tabs that are not discoverable
        /// (By Hand, Notes) are always shown.
        /// </summary>
        public static bool IsDiscovered(string tabName)
        {
            if (tabName == null || !byName.TryGetValue(tabName, out Tab tab)) return true;
            if (Hit(tab)) return true;
            foreach (Tab sub in tab.Inherits)
                if (!sub.Split && Hit(sub)) return true;   // a split station is its own tab; it does not show the owner's tab
            return false;
        }

        /// <summary>The same question for a catalog key ("Furnace|i|Terraria/IronBar").</summary>
        public static bool IsDiscoveredKey(string catalogKey) => IsDiscovered(RecipeCatalog.TabOf(RecipeCatalog.MachineOf(catalogKey)));

        /// <summary>True if the tab's own machine/station was discovered (not just a station it inherits from). Tabs without a tile count as discovered.</summary>
        public static bool IsOwnDiscovered(string tabName) =>
            tabName == null || !byName.TryGetValue(tabName, out Tab tab) || Hit(tab);

        /// <summary>The tiles that stand for this tab: its own tile plus the tiles of the stations it inherits from. The detail page names any OTHER tile a recipe needs.</summary>
        public static HashSet<int> TilesOfTab(string tabName)
        {
            var tiles = new HashSet<int>();
            if (tabName == null || !byName.TryGetValue(tabName, out Tab tab)) return tiles;
            if (tab.Tile >= 0) tiles.Add(tab.Tile);
            foreach (Tab sub in tab.Inherits)
                if (sub.Tile >= 0) tiles.Add(sub.Tile);
            return tiles;
        }

        /// <summary>True if the tab has to be discovered at all (By Hand and Notes do not, so they are never "new").</summary>
        public static bool IsDiscoverable(string tabName) => tabName != null && byName.ContainsKey(tabName);

        /// <summary>
        /// Which catalog names the book header is titled after right now. Normally just the tab itself. A tab that inherits from stations
        /// and whose own machine is not discovered yet is titled after the inherited stations that are ("Anvils").
        /// </summary>
        public static string[] HeaderStations(string tabName)
        {
            if (tabName == null || !byName.TryGetValue(tabName, out Tab tab) || tab.Inherits.Count == 0 || Hit(tab))
                return new[] { tabName };

            var shown = new List<string>();
            foreach (Tab sub in tab.Inherits)
                if (Hit(sub)) shown.Add(sub.Name);
            if (shown.Count == 0) shown.Add(tabName);
            return shown.ToArray();
        }

        /// <summary>
        /// R1.6 migration, run once per world by RecipeKnowledgeSystem.LoadWorldData (flag "TabSplitMigrated"). Before R1.6 the Work Benches and the
        /// Heavy Work Bench were one tab with the Mechanical Assembler: any of them discovered showed all recipes of all of them. Now they are
        /// separate tabs, so a station the world already CRAFTED in, whose merged tab used to be visible, gets its own marker, and its tab does not
        /// vanish. Returns the markers to add. Uses the raw crafted keys on purpose (no sibling rule) and only ever runs on old data.
        /// </summary>
        public static List<int> LegacySplitMarkers(IEnumerable<string> craftedKeys)
        {
            var result = new List<int>();
            HashSet<string> craftedTabs = null;
            foreach (Tab owner in byName.Values)
            {
                if (owner.Inherited || owner.Inherits.Count == 0) continue;
                bool anySplit = false, anyHit = false;
                foreach (Tab sub in owner.Inherits)
                {
                    if (sub.Split) anySplit = true;
                    if (Hit(sub)) anyHit = true;
                }
                if (!anySplit || !anyHit) continue;   // the merged tab was not visible before, nothing to keep

                foreach (Tab sub in owner.Inherits)
                {
                    if (!sub.Split || Hit(sub)) continue;
                    if (craftedTabs == null)
                    {
                        craftedTabs = new HashSet<string>();
                        foreach (string key in craftedKeys)
                        {
                            string machine = RecipeCatalog.MachineOf(key);
                            if (machine != null) craftedTabs.Add(machine);
                        }
                    }
                    if (craftedTabs.Contains(sub.Name)) result.Add(sub.Marker);
                }
            }
            return result;
        }

        // One tile (a machine, or one vanilla station) counts as discovered when its marker is in the world's seen set, OR a parchment has
        // unlocked a recipe that names it (the key's machine part). A crafted recipe does not count (obtaining a product would
        // otherwise reveal every station that makes it). Computed, never saved.
        static bool Hit(Tab tab)
        {
            if (RecipeKnowledgeSystem.IsSeen(tab.Marker)) return true;

            // A machine that is discovered (held, near, or unlocked by a parchment) also discovers every station it inherits recipes
            // from (Autohammer -> Anvils). The owner is never itself inherited, so this cannot loop. Computed, never saved.
            if (tab.Inherited && byName.TryGetValue(tab.Owner, out Tab owner) && Hit(owner)) return true;
            HashSet<string> machines = UnlockedMachines();
            if (machines.Contains(tab.Name)) return true;
            return tab.Tile >= 0 && machines.Contains(VanillaStationBooks.NameFor(tab.Tile));   // the vanilla twin of a machine
        }

        static HashSet<string> UnlockedMachines()
        {
            if (unlockedVersion == RecipeKnowledgeSystem.Version) return unlockedMachines;

            unlockedVersion = RecipeKnowledgeSystem.Version;
            unlockedMachines.Clear();
            foreach (string key in RecipeKnowledgeSystem.UnlockedKeys)
            {
                string machine = RecipeCatalog.MachineOf(key);
                if (machine != null) unlockedMachines.Add(machine);
            }
            return unlockedMachines;
        }

        /// <summary>The inherited stations of the tab that are discovered, for the "Inherits from ..." hover tip. Empty (no tip) when there are none, and for most tabs.</summary>
        public static string[] InheritedNames(string tabName)
        {
            if (tabName == null || !byName.TryGetValue(tabName, out Tab tab) || tab.Inherits.Count == 0) return new string[0];
            var names = new List<string>();
            foreach (Tab sub in tab.Inherits)
                if (Hit(sub)) names.Add(sub.Name);
            return names.ToArray();
        }

        /// <summary>Item types whose sprites stand for this tab (every item that places its tile, sorted). Empty for By Hand and Notes.</summary>
        public static int[] ItemsOf(string catalogName)
        {
            if (catalogName == null || !byName.TryGetValue(catalogName, out Tab tab) || tab.ItemList.Length == 0) return NoItems;

            // Only the items the world has held (the Wooden Work Bench, then the Cactus Work Bench once someone has it). A tab that was
            // discovered without holding anything (stood near it, parchment) shows its first item so it still has a sprite.
            List<int> known = null;
            foreach (int type in tab.ItemList)
                if (RecipeKnowledgeSystem.IsSeen(RecipeKnowledgeSystem.StationItemMarker(type)))
                    (known ??= new List<int>()).Add(type);
            return known != null ? known.ToArray() : new[] { tab.ItemList[0] };
        }

        /// <summary>The items of several tabs together, in order.</summary>
        public static int[] ItemsOf(string[] catalogNames)
        {
            var all = new List<int>();
            foreach (string name in catalogNames) all.AddRange(ItemsOf(name));
            return all.ToArray();
        }

        // ---------------------------------------------------------------- client scan (rules 1 and 2)

        /// <summary>Local player, a few times a second: reports every tab whose item the player holds or whose tile is within reach.</summary>
        public static void ScanLocalPlayer(Player player)
        {
            if (player == null || !player.active || player.dead || byName.Count == 0) return;

            // Same slots as the ingredient scan: 0-49 inventory + hotbar, 50-53 coins, 54-57 ammo, plus the item on the cursor.
            for (int i = 0; i < 58; i++) ScanItem(player.inventory[i]);
            ScanItem(Main.mouseItem);

            // Vanilla's own "could craft here" flags first, then a tile scan (the same test the server runs, a little stricter).
            foreach (Tab tab in byName.Values)
                if (!RecipeKnowledgeSystem.IsSeen(tab.Marker) && tab.Tile < player.adjTile.Length && player.adjTile[tab.Tile])
                    Report(tab);

            int cx = (int)(player.Center.X / 16f);
            int cy = (int)(player.Center.Y / 16f);
            for (int x = cx - NearTilesX; x <= cx + NearTilesX; x++)
                for (int y = cy - NearTilesY; y <= cy + NearTilesY; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1)) continue;
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && byTile.TryGetValue(tile.TileType, out Tab tab) && !RecipeKnowledgeSystem.IsSeen(tab.Marker))
                        Report(tab);
                }
        }

        static void ScanItem(Item item)
        {
            if (item == null || item.IsAir) return;
            if (!byItem.TryGetValue(item.type, out Tab tab)) return;
            if (!RecipeKnowledgeSystem.IsSeen(tab.Marker)) Report(tab);
            int itemMarker = RecipeKnowledgeSystem.StationItemMarker(item.type);
            if (!RecipeKnowledgeSystem.IsSeen(itemMarker)) RecipeKnowledgeNet.ReportSeen(itemMarker);
        }

        // Marks the tab here at once (the book reacts without waiting); a multiplayer client also queues it for the server.
        static void Report(Tab tab)
        {
            // R3: the marker is NEW to this process only on the first report, so the pop-up shows once per discovery, for the client that made it.
            // Later discoveries by other players arrive as a SeenDelta and never pass through here. Saved markers (relog) are already seen.
            bool isNew = !RecipeKnowledgeSystem.IsSeen(tab.Marker);
            RecipeKnowledgeNet.ReportSeen(tab.Marker);
            if (isNew && !Main.dedServ) popUps.Enqueue(tab.Name);
        }

        // ---------------------------------------------------------------- R3 pop-up

        // Tunables (not const, so hot reload can change them).
        public static Microsoft.Xna.Framework.Color PopUpColor = new Microsoft.Xna.Framework.Color(255, 220, 90);
        public static bool PopUpDramatic = true;       // CombatText "dramatic": bigger and stays longer

        static readonly Queue<string> popUps = new Queue<string>();   // tabs discovered by this client, waiting for their text (one per scan, so several at once do not overlap)

        /// <summary>Client, once per scan: shows one "New recipes unlocked!" text above the local player (CombatText, local only, no sound). Works with the book closed.</summary>
        public static void ShowNextPopUp()
        {
            if (Main.dedServ || popUps.Count == 0) return;
            popUps.Dequeue();
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) return;
            CombatText.NewText(player.Hitbox, PopUpColor, Terraria.Localization.Language.GetTextValue("Mods.Factorraria.RecipeBook.NewTabDiscovered"), PopUpDramatic);
        }

        public static void ClearPopUps() => popUps.Clear();

        // ---------------------------------------------------------------- server check

        /// <summary>
        /// Server: is this a real tab marker, and does the sender really hold one of the tab's items or stand near its tile? The server
        /// reads its own copy of the player (inventory and position are synced by vanilla) and its own tile data.
        /// </summary>
        public static bool ServerAccepts(Player player, int marker)
        {
            if (player == null || !player.active || player.dead) return false;
            if (!byId.TryGetValue(RecipeKnowledgeSystem.TabIdOf(marker), out Tab tab)) return false;

            foreach (Item item in player.inventory)
                if (item != null && !item.IsAir && tab.Items.Contains(item.type))
                    return true;

            int cx = (int)(player.Center.X / 16f);
            int cy = (int)(player.Center.Y / 16f);
            int rx = NearTilesX + ServerNearExtra;
            int ry = NearTilesY + ServerNearExtra;
            for (int x = cx - rx; x <= cx + rx; x++)
                for (int y = cy - ry; y <= cy + ry; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1)) continue;
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == tab.Tile) return true;
                }

            return false;
        }

        /// <summary>Server: does the sender really hold this station/machine item (and is it one a tab uses)?</summary>
        public static bool ServerAcceptsItem(Player player, int marker)
        {
            if (player == null || !player.active || player.dead) return false;
            int type = RecipeKnowledgeSystem.StationItemOf(marker);
            if (!byItem.ContainsKey(type)) return false;

            foreach (Item item in player.inventory)
                if (item != null && !item.IsAir && item.type == type)
                    return true;
            return false;
        }

        public static string NameOfMarker(int marker) =>
            byId.TryGetValue(RecipeKnowledgeSystem.TabIdOf(marker), out Tab tab) ? tab.Name : "tab " + RecipeKnowledgeSystem.TabIdOf(marker);
    }

    /// <summary>Runs the local tab scan a few times a second (never on a dedicated server: it has no local player).</summary>
    public class RecipeTabDiscoverySystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu) return;
            if (!JoinSync.Completed) return;   // R3: until the world's knowledge has arrived, everything already discovered would look new to this client
            if (Main.GameUpdateCount % (uint)Math.Max(1, RecipeTabDiscovery.ScanTicks) != 0) return;

            RecipeTabDiscovery.ScanLocalPlayer(Main.LocalPlayer);
            RecipeTabDiscovery.ShowNextPopUp();
        }

        public override void OnWorldUnload() => RecipeTabDiscovery.ClearPopUps();
    }
}