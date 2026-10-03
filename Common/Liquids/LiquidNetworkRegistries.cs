using Factorraria.Common.Machines;
using Factorraria.Content.Liquids.Fuel;
using Factorraria.Content.Liquids.Oil;
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
        public bool IsWaterVariant;   // biome waters (Jungle Water, ...): plain water in the world, but their own liquid type in pipes, tanks and recipes
        public string IconPath;   // optional browser icon, e.g. "Factorraria/Content/Liquids/Oil/OilIcon"; null = flat RenderColor swatch
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

        // Custom Liquids
        public static int Oil;
        public static int Fuel;

        // Recipe-only sentinel meaning "any water, whatever the biome". It is NOT a registered liquid:
        // it never sits in a tank or a network, it is only ever used as a recipe INPUT.
        public const int AnyWater = -2;

        // Biome waters (ids are filled in by LiquidTypeRegistrationSystem)
        public static int SnowWater;
        public static int DesertWater;
        public static int JungleWater;
        public static int OceanWater;
        public static int UndergroundWater;
        public static int CavernWater;
        public static int CorruptionWater;
        public static int CrimsonWater;
        public static int HallowWater;
        public static int MushroomWater;

        public static readonly Dictionary<MachineBiome, int> WaterByBiome = new();

        // The water type for a biome. Biomes without a registered variant just get plain Water.
        public static int WaterFor(MachineBiome biome) => WaterByBiome.TryGetValue(biome, out int id) ? id : Water;

        // True for plain Water and for every biome variant.
        public static bool IsWater(int liquidType) =>
            liquidType == Water || (liquidType >= 0 && liquidType < definitions.Count && definitions[liquidType].IsWaterVariant);

        // Registers a biome variant of water and remembers which biome it belongs to.
        // It is still ordinary water in the world (VanillaTileLiquidId = Water).
        public static int RegisterBiomeWater(MachineBiome biome, string name, Color color, string iconPath = null)
        {
            int id = Register(new LiquidTypeDefinition
            {
                Name = name,
                RenderColor = color,
                VanillaTileLiquidId = (byte?)LiquidID.Water,
                IsWaterVariant = true,
                IconPath = iconPath
            });
            WaterByBiome[biome] = id;
            return id;
        }
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
        public override void PostSetupContent()
        {
            LiquidTypeRegistry.Water = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Water", RenderColor = new Color(0, 81, 229), VanillaTileLiquidId = (byte?)LiquidID.Water, IconPath = "Factorraria/Common/Liquids/LiquidIcons/WaterIcon" });
            LiquidTypeRegistry.Lava = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Lava", RenderColor = new Color(200, 70, 20), VanillaTileLiquidId = (byte?)LiquidID.Lava, IconPath = "Factorraria/Common/Liquids/LiquidIcons/LavaIcon" });
            LiquidTypeRegistry.Honey = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Honey", RenderColor = new Color(247, 167, 8), VanillaTileLiquidId = (byte?)LiquidID.Honey, IconPath = "Factorraria/Common/Liquids/LiquidIcons/HoneyIcon" });
            LiquidTypeRegistry.Shimmer = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Shimmer", RenderColor = new Color(155, 114, 234), VanillaTileLiquidId = (byte?)LiquidID.Shimmer, IconPath = "Factorraria/Common/Liquids/LiquidIcons/ShimmerIcon" });
            LiquidTypeRegistry.Oil = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Oil", RenderColor = new Color(40, 40, 40), VanillaTileLiquidId = (byte?)ModContent.GetInstance<OilLiquid>().Type, IconPath = "Factorraria/Common/Liquids/LiquidIcons/OilIcon" });
            LiquidTypeRegistry.Fuel = LiquidTypeRegistry.Register(new LiquidTypeDefinition { Name = "Fuel", RenderColor = new Color(134, 104, 78), VanillaTileLiquidId = (byte?)ModContent.GetInstance<FuelLiquid>().Type, IconPath = "Factorraria/Common/Liquids/LiquidIcons/FuelIcon" });

            // Biome waters. Keep these AFTER the built-ins and only ever APPEND new ones: machine tanks save the
            // liquid as its registry index, so inserting in the middle would change what old saves contain.
            // Add an icon path as a 4th argument if you draw one; without it the recipe browser shows a colour swatch.
            LiquidTypeRegistry.SnowWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.Snow, "Snow Water", new Color(150, 215, 235));
            LiquidTypeRegistry.DesertWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.Desert, "Desert Water", new Color(200, 185, 110));
            LiquidTypeRegistry.JungleWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.Jungle, "Jungle Water", new Color(50, 160, 70));
            LiquidTypeRegistry.OceanWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.Ocean, "Ocean Water", new Color(0, 70, 170));
            LiquidTypeRegistry.UndergroundWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.Underground, "Underground Water", new Color(110, 115, 125));
            LiquidTypeRegistry.CavernWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.Cavern, "Cavern Water", new Color(80, 88, 105));
            LiquidTypeRegistry.CorruptionWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.Corruption, "Corrupted Water", new Color(115, 65, 155));
            LiquidTypeRegistry.CrimsonWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.Crimson, "Crimson Water", new Color(175, 45, 55));
            LiquidTypeRegistry.HallowWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.Hallow, "Hallowed Water", new Color(205, 150, 230));
            LiquidTypeRegistry.MushroomWater = LiquidTypeRegistry.RegisterBiomeWater(MachineBiome.GlowingMushroom, "Mushroom Water", new Color(90, 105, 215));
        }

        
            //if (player.ZoneShimmer)          return MachineBiome.Aether;
            //if (player.ZoneMeteor)           return MachineBiome.Meteor;
            //if (player.ZoneDungeon)          return MachineBiome.Dungeon;
            //if (player.ZoneLihzhardTemple)   return MachineBiome.Temple;
            //if (player.ZoneGlowshroom)       return MachineBiome.GlowingMushroom;
            //if (player.ZoneCorrupt)          return MachineBiome.Corruption;
            //if (player.ZoneCrimson)          return MachineBiome.Crimson;
            //if (player.ZoneHallow)           return MachineBiome.Hallow;
            //if (player.ZoneJungle)           return MachineBiome.Jungle;
            //if (player.ZoneSnow)             return MachineBiome.Snow;
            //if (player.ZoneDesert)           return MachineBiome.Desert;
            //if (player.ZoneGraveyard)        return MachineBiome.Graveyard;
            //if (player.ZoneBeach)            return MachineBiome.Ocean;
            //if (player.ZoneUnderworldHeight) return MachineBiome.Underworld;
            //if (player.ZoneRockLayerHeight)  return MachineBiome.Cavern;
            //if (player.ZoneDirtLayerHeight)  return MachineBiome.Underground;
            //if (player.ZoneSkyHeight)        return MachineBiome.Sky;
            //if (player.ZoneOverworldHeight)  return MachineBiome.Forest;

        public override void Unload()
        {
            LiquidTypeRegistry.definitions.Clear();
            LiquidTypeRegistry.WaterByBiome.Clear();
        }
    }
}