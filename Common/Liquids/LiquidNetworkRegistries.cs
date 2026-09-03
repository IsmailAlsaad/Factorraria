using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Factorraria.Common.Liquids
{
    // Describes one liquid — water, lava, crude oil, future custom liquids.
    // Pure data, no behavior — the liquid equivalent of an ItemID entry.
    public class LiquidTypeDefinition
    {
        public string Name;
        public Color RenderColor; // used to draw liquid in pipes/tanks until real textures exist
    }

    public static class LiquidTypeRegistry
    {
        static List<LiquidTypeDefinition> definitions = new();

        // Hands back an ID = wherever it landed in the list, same idea as tModLoader
        // auto-assigning IDs to ModItem/ModTile. This is what keeps the door open for
        // custom liquids later — nothing here is a fixed enum.
        public static int Register(LiquidTypeDefinition definition)
        {
            definitions.Add(definition);
            return definitions.Count - 1;
        }

        public static LiquidTypeDefinition Get(int liquidTypeId) => definitions[liquidTypeId];

        // Built-ins, filled in once at load by whatever ModSystem owns startup registration.
        public static int Water;
        public static int Lava;
    }

    // Maps a pipe TILE TYPE to its max flow-rate capacity. MK1 registers one rate,
    // a future MK2 registers a higher rate — no other code changes when a tier is added.
    public static class PipeTierRegistry
    {
        public static Dictionary<int, float> MaxFlowRateByTileType = new();

        public static void Register(int pipeTileType, float maxFlowRate)
        {
            MaxFlowRateByTileType[pipeTileType] = maxFlowRate;
        }

        // Lets the network scanner ask "is this tile even a pipe" without knowing
        // how many tiers exist.
        public static bool IsPipeTile(int tileType) => MaxFlowRateByTileType.ContainsKey(tileType);
    }
}