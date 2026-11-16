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

        static KeyValuePair<string, string> Twin(string machine, int tile) =>
            new KeyValuePair<string, string>(machine, VanillaStationBooks.NameFor(tile));
    }
}
