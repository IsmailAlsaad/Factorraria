using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Liquids
{
    // Describes one liquid — water, lava, crude oil, future custom liquids.
    // Pure data, no behavior — the liquid equivalent of an ItemID entry.
    public class LiquidTypeDefinition
    {
        public string Name;
        public Color RenderColor; // why do i even need this
        public byte? VanillaTileLiquidId; // null = no world-tile representation (pure custom liquid)
    }

    public static class LiquidTypeRegistry
    {
        public static List<LiquidTypeDefinition> definitions = new();

        // Hands back an ID = wherever it landed in the list, same idea as tModLoader
        // auto-assigning IDs to ModItem/ModTile. This is what keeps the door open for
        // custom liquids later — nothing here is a fixed enum.
        public static int Register(LiquidTypeDefinition definition)
        {
            definitions.Add(definition);
            return definitions.Count - 1;
        }

        public static byte? ToTileLiquidId(int liquidType) => definitions[liquidType].VanillaTileLiquidId;

        public static int? FromTileLiquidId(byte tileLiquidId)
        {
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i].VanillaTileLiquidId == tileLiquidId) return i;
            return null;
        }
        public static LiquidTypeDefinition Get(int liquidTypeId) => definitions[liquidTypeId];

        // Built-ins, filled in once at load by whatever ModSystem owns startup registration.
        public static int Water;
        public static int Lava;
        public static int Honey;
        public static int Shimmer;
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

    public class LiquidTypeRegistrationSystem : ModSystem
    {
        public override void Load()
        {
            LiquidTypeRegistry.Water = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Water", RenderColor = new Color(40, 110, 190), VanillaTileLiquidId = (byte?)LiquidID.Water });
            LiquidTypeRegistry.Lava = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Lava", RenderColor = new Color(200, 70, 20), VanillaTileLiquidId = (byte?)LiquidID.Lava });
            LiquidTypeRegistry.Honey = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Honey", RenderColor = new Color(247, 167, 8), VanillaTileLiquidId = (byte?)LiquidID.Honey });
            LiquidTypeRegistry.Shimmer = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Shimmer", RenderColor = new Color(146, 125, 125), VanillaTileLiquidId = (byte?)LiquidID.Shimmer });
        }

        public override void Unload()
        {
            LiquidTypeRegistry.definitions.Clear();
        }
    }
}