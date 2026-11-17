using Factorraria.Content.Tiles.Machines.ElectricalConsumers.MechanicalAssembler;
using System;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// The file you edit to say which recipe book tabs merge. A machine that sits on a vanilla crafting tile has a vanilla station
    /// tab for that same tile (Hellforge machine / Hellforge tile). The book shows them as ONE tab under the machine's name:
    /// the machine's recipes first, then the station's recipes for products the machine does not already list.
    /// Display only (RecipeCatalog.TabOf): catalog keys and saves are untouched.
    ///
    /// Key = catalog machine name (as registered in MachineRegisterationSystem), value = the vanilla station tab
    /// (VanillaStationBooks.NameFor(tile)). A pair whose machine or station tab does not exist simply does nothing.
    /// A machine that imports recipes from a DIFFERENT tile (Autohammer imports Anvils, Mechanical Assembler imports Work Benches)
    /// is not a twin of that tile and stays separate.
    /// </summary>
    public static class RecipeTabDefinitions
    {
        public static readonly KeyValuePair<string, string>[] Twins =
        {
            Twin("Furnace", TileID.Furnaces),
            Twin("Hellforge", TileID.Hellforge),
            Twin("Solidifier", TileID.Solidifier),
            Twin("IceMachine", TileID.IceMachine),
            Twin("LiquidDistillator", TileID.ImbuingStation),
            Twin("DecayChamber", TileID.LesionStation),
            Twin("Autohammer", TileID.Autohammer),
        };



        /// <summary>
        /// Machine tabs with no vanilla twin: the machine sits on its own tile, and holding the item that places it (or standing near
        /// it) discovers the tab (RecipeTabDiscovery). Key = catalog machine name. APPEND ONLY: a tab's saved discovery id is
        /// RecipeTabDiscovery.ExtraIdBase plus its position in this list.
        /// A machine tab that is a twin (above) is discovered through its twin's tile and needs no line here.
        /// </summary>
        public static readonly KeyValuePair<string, Func<int>>[] ExtraMachineTiles =
        {
            new KeyValuePair<string, Func<int>>("MechanicalAssembler", ModContent.TileType<MechanicalAssemblerTile>),
        };

        /// <summary>
        /// Machines that INHERIT recipes from vanilla stations of a different tile (their RecipeBook calls ImportVanillaRecipes for it).
        /// Each listed station's tab is absorbed into the machine's tab (RecipeCatalog.TabOf), so the book has one tab where the old
        /// station tab and the machine tab were. Until the machine itself is discovered the tab is titled after the discovered
        /// station(s) ("Anvils"); once the machine is discovered it is titled after the machine. The order here is the order shown
        /// in the hover tip and in "Autohammer or Anvils" on the detail page. A station that is already a twin of another machine
        /// (Furnaces / Furnace) is never absorbed a second time.
        /// Key = catalog machine name, value = vanilla station tiles.
        /// </summary>
        public static readonly KeyValuePair<string, int[]>[] Inherits =
        {
            Inherit("Autohammer", TileID.Anvils),
            Inherit("MechanicalAssembler", TileID.WorkBenches, TileID.HeavyWorkBench),
        };

        /// <summary>
        /// R1.6: owners (catalog machine names) whose inherited stations stay SEPARATE tabs until the owner itself is discovered
        /// (held or stood near, or unlocked by a parchment). Before that each station is its own tab, discovered by its own item or tile
        /// ("Work Benches", "Heavy Work Bench"); after that they merge into the owner's tab, as every Inherits entry does from the start.
        /// Only applies to the stations listed in Inherits (not to InheritedTabs). An owner that is not listed here (Autohammer) merges
        /// its stations at once, as before.
        /// </summary>
        public static readonly string[] SplitUntilOwnerDiscovered =
        {
            "MechanicalAssembler",
        };

        /// <summary>
        /// Machines that inherit a WHOLE tab: key = the machine that takes over, value = the machine whose tab (with its vanilla twin
        /// station) is absorbed. The absorbed machine has no tab of its own any more; until the owner is discovered the tab is titled
        /// after the absorbed one ("Furnace"), afterwards after the owner ("Hellforge"). Its recipes stay listed once per product:
        /// a product both machines make is one entry whose right page cycles the recipes, each with the fuels of the machines that do it.
        /// </summary>
        public static readonly KeyValuePair<string, string>[] InheritedTabs =
        {
            new KeyValuePair<string, string>("Hellforge", "Furnace"),
        };

        static KeyValuePair<string, int[]> Inherit(string machine, params int[] tiles) =>
            new KeyValuePair<string, int[]>(machine, tiles);

        static KeyValuePair<string, string> Twin(string machine, int tile) =>
            new KeyValuePair<string, string>(machine, VanillaStationBooks.NameFor(tile));
    }
}
