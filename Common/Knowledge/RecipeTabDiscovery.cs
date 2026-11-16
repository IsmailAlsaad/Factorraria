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

        static readonly HashSet<string> recipeTabs = new HashSet<string>();   // tabs that own a crafted or unlocked recipe (rule 3)
        static int recipeTabsVersion = int.MinValue;

        public static IReadOnlyCollection<Tab> All => byName.Values;

        public static void Clear()
        {
            byName.Clear();
            byId.Clear();
            byTile.Clear();
            byItem.Clear();
            recipeTabs.Clear();
            recipeTabsVersion = int.MinValue;
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
                string name = RecipeCatalog.TabOf(e.Machine);
                if (!seen.Add(name)) continue;
                if (!TryFindTile(name, out int tile, out int id)) continue;   // By Hand and tabs with no known tile are always shown
                if (byId.ContainsKey(id) || byTile.ContainsKey(tile)) continue;

                var tab = new Tab { Name = name, Id = id, Tile = tile };
                byName[name] = tab;
                byId[id] = tab;
                byTile[tile] = tab;
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

            return byName.Count;
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

        /// <summary>True if the book may list this tab. Tabs that are not discoverable (By Hand, Notes) are always shown.</summary>
        public static bool IsDiscovered(string tabName)
        {
            if (tabName == null || !byName.TryGetValue(tabName, out Tab tab)) return true;
            if (RecipeKnowledgeSystem.IsSeen(tab.Marker)) return true;
            return RecipeTabs().Contains(tabName);
        }

        /// <summary>The same question for a catalog key ("Furnace|i|Terraria/IronBar").</summary>
        public static bool IsDiscoveredKey(string catalogKey) => IsDiscovered(RecipeCatalog.TabOf(RecipeCatalog.MachineOf(catalogKey)));

        // Rule 3, rebuilt only when the crafted/unlocked sets change. Uses the saved sets themselves, not RecipeKnowledgeSystem.IsCrafted,
        // so crafting a product in one tab does not reveal the other tabs that make it.
        static HashSet<string> RecipeTabs()
        {
            if (recipeTabsVersion == RecipeKnowledgeSystem.Version) return recipeTabs;

            recipeTabsVersion = RecipeKnowledgeSystem.Version;
            recipeTabs.Clear();
            foreach (string key in RecipeKnowledgeSystem.CraftedKeys) AddKeyTab(key);
            foreach (string key in RecipeKnowledgeSystem.UnlockedKeys) AddKeyTab(key);
            return recipeTabs;
        }

        static void AddKeyTab(string key)
        {
            string machine = RecipeCatalog.MachineOf(key);
            if (machine != null) recipeTabs.Add(RecipeCatalog.TabOf(machine));
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
            if (byItem.TryGetValue(item.type, out Tab tab) && !RecipeKnowledgeSystem.IsSeen(tab.Marker))
                Report(tab);
        }

        // Marks the tab here at once (the book reacts without waiting); a multiplayer client also queues it for the server.
        static void Report(Tab tab) => RecipeKnowledgeNet.ReportSeen(tab.Marker);

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

        public static string NameOfMarker(int marker) =>
            byId.TryGetValue(RecipeKnowledgeSystem.TabIdOf(marker), out Tab tab) ? tab.Name : "tab " + RecipeKnowledgeSystem.TabIdOf(marker);
    }

    /// <summary>Runs the local tab scan a few times a second (never on a dedicated server: it has no local player).</summary>
    public class RecipeTabDiscoverySystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu) return;
            if (Main.GameUpdateCount % (uint)Math.Max(1, RecipeTabDiscovery.ScanTicks) != 0) return;

            RecipeTabDiscovery.ScanLocalPlayer(Main.LocalPlayer);
        }
    }
}