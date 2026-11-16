using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// The vanilla half of the recipe book: one tab per vanilla crafting station (Work Bench, Furnace, Anvil, Demon Altar, ...) plus one
    /// for the recipes that need no station. Each tab is a RecipeBook built from Main.recipe by RecipeBook.ImportStationRecipes and
    /// registered in RecipeCatalog as a station (name = "Vanilla" + the tile's TileID name, "VanillaHand" for hand crafts).
    ///
    /// A tab lists the real vanilla recipe even when a machine book copied it and switched the original off, and also when no machine
    /// has it at all. A recipe that needs two stations is listed in both tabs. Crafting a product anywhere counts for every tab and
    /// machine that makes it (RecipeKnowledgeSystem.IsCrafted, RecipeCatalog.SiblingKeys).
    ///
    /// Built in MachineRegisterationSystem.PostAddRecipes after every machine book. Nothing here is random or per player, so the server
    /// and every client end up with the same tabs and the same catalog keys.
    /// </summary>
    public static class VanillaStationBooks
    {
        public const string Prefix = "Vanilla";
        public const string HandName = "VanillaHand";

        /// <summary>Catalog name of a station tab (station = a TileID, or RecipeBook.HandStation).</summary>
        public static string NameFor(int station)
        {
            if (station == RecipeBook.HandStation) return HandName;
            string name = TileID.Search.GetName(station);
            return Prefix + (string.IsNullOrEmpty(name) ? "Tile" + station : name);
        }

        /// <summary>The tile a station tab's catalog name stands for ("VanillaFurnaces" is TileID.Furnaces), or -1 (hand crafts, not a station tab, unknown tile).</summary>
        public static int TileOf(string catalogName)
        {
            if (catalogName == null || catalogName == HandName) return -1;
            if (catalogName.Length <= Prefix.Length || !catalogName.StartsWith(Prefix, System.StringComparison.Ordinal)) return -1;
            return TileID.Search.TryGetId(catalogName.Substring(Prefix.Length), out int tile) ? tile : -1;
        }

        /// <summary>Builds and registers every tab that has at least one recipe. Returns the number of tabs; groups = recipe groups over all tabs.</summary>
        public static int RegisterAll(out int groups)
        {
            groups = 0;
            bool hand = false;
            var stations = new SortedSet<int>();

            for (int i = 0; i < Main.recipe.Length; i++)
            {
                Recipe recipe = Main.recipe[i];
                if (!RecipeBook.IsStationRecipe(recipe)) continue;

                if (recipe.requiredTile.Count == 0) { hand = true; continue; }
                foreach (int tile in recipe.requiredTile)
                    if (tile >= 0 && tile < TileID.Count) stations.Add(tile);   // a modded station has no vanilla tab
            }

            int tabs = 0;
            if (hand && RegisterOne(RecipeBook.HandStation, ref groups)) tabs++;
            foreach (int tile in stations)
                if (RegisterOne(tile, ref groups)) tabs++;
            return tabs;
        }

        static bool RegisterOne(int station, ref int groups)
        {
            var book = new RecipeBook();
            book.ImportStationRecipes(station);
            book.Rebuild();
            if (book.Groups.Count == 0) return false;

            RecipeCatalog.RegisterStation(NameFor(station), book);
            groups += book.Groups.Count;
            return true;
        }
    }
}