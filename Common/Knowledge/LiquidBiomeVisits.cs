using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using System.Collections.Generic;
using Terraria;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// Which biome reveals which liquid in the recipe book. A liquid cannot be held, so instead it counts as SEEN once the
    /// local player has stood in its biome. The recipe book then shows it as a real ingredient/fuel instead of "???".
    ///
    /// THIS IS THE FILE YOU EDIT: change the table in Register() (one line per liquid, Set(liquid, biome)).
    /// A liquid with no line here keeps the old rule (always shown if no recipe makes it, otherwise shown once crafted).
    /// A liquid WITH a line stays hidden until its biome was visited or a recipe that makes it was crafted.
    ///
    /// Multiplayer: the visit is a "seen marker" in the same set and the same SeenReport / SeenDelta / join sync packets as
    /// seen items (liquid markers are negative numbers, see RecipeKnowledgeSystem.LiquidMarker), so one player visiting a
    /// biome reveals it for everybody in the world, and it is saved with the world. Only the local player is polled.
    /// </summary>
    public static class LiquidBiomeVisits
    {
        public static int PollTicks = 30;   // how often the local player's biomes are checked (30 = twice a second)

        static readonly Dictionary<int, MachineBiome> biomeOf = new Dictionary<int, MachineBiome>();

        /// <summary>Builds the table. Needs the liquid ids, so it runs in PostAddRecipes (they are assigned in PostSetupContent).</summary>
        public static void Register()
        {
            biomeOf.Clear();

            Set(LiquidTypeRegistry.Water, MachineBiome.Forest);               // plain (forest) water
            Set(LiquidTypeRegistry.Lava, MachineBiome.Underworld);
            Set(LiquidTypeRegistry.Honey, MachineBiome.Jungle);
            Set(LiquidTypeRegistry.Shimmer, MachineBiome.Aether);
            Set(LiquidTypeRegistry.Oil, MachineBiome.UndergroundDesert);

            // Every biome water follows the biome it was registered for (Snow Water -> Snow, Corrupted Water -> Corruption, ...).
            foreach (KeyValuePair<MachineBiome, int> water in LiquidTypeRegistry.WaterByBiome)
                Set(water.Value, water.Key);
        }

        public static void Set(int liquidType, MachineBiome biome) => biomeOf[liquidType] = biome;

        public static void Clear() => biomeOf.Clear();

        /// <summary>True if this liquid is revealed by visiting a biome (so it is hidden until then).</summary>
        public static bool HasRule(int liquidType) => biomeOf.ContainsKey(liquidType);

        /// <summary>Server: is this negative seen marker a liquid that can really be revealed by a visit?</summary>
        public static bool IsValidMarker(int marker) => marker < 0 && biomeOf.ContainsKey(-marker - 1);

        /// <summary>Local player: reports every not-yet-seen liquid whose biome the player is in right now.</summary>
        public static void Poll(Player player)
        {
            foreach (KeyValuePair<int, MachineBiome> rule in biomeOf)
                if (!RecipeKnowledgeSystem.IsLiquidSeen(rule.Key) && InBiome(player, rule.Value))
                    RecipeKnowledgeNet.ReportLiquidSeen(rule.Key);
        }

        // Each biome's own zone flag, checked independently (unlike BaseMachine's single priority-ordered biome),
        // so standing in the underground desert inside a corruption still counts as visiting both.
        static bool InBiome(Player p, MachineBiome biome)
        {
            switch (biome)
            {
                case MachineBiome.Forest: return p.ZoneOverworldHeight;
                case MachineBiome.Snow: return p.ZoneSnow;
                case MachineBiome.Desert: return p.ZoneDesert;
                case MachineBiome.UndergroundDesert: return p.ZoneUndergroundDesert;
                case MachineBiome.Jungle: return p.ZoneJungle;
                case MachineBiome.GlowingMushroom: return p.ZoneGlowshroom;
                case MachineBiome.Corruption: return p.ZoneCorrupt;
                case MachineBiome.Crimson: return p.ZoneCrimson;
                case MachineBiome.Hallow: return p.ZoneHallow;
                case MachineBiome.Underworld: return p.ZoneUnderworldHeight;
                case MachineBiome.Cavern: return p.ZoneRockLayerHeight;
                case MachineBiome.Underground: return p.ZoneDirtLayerHeight;
                case MachineBiome.Sky: return p.ZoneSkyHeight;
                case MachineBiome.Ocean: return p.ZoneBeach;
                case MachineBiome.Dungeon: return p.ZoneDungeon;
                case MachineBiome.Temple: return p.ZoneLihzhardTemple;
                case MachineBiome.Aether: return p.ZoneShimmer;
                case MachineBiome.Graveyard: return p.ZoneGraveyard;
                case MachineBiome.Meteor: return p.ZoneMeteor;
                default: return false;
            }
        }
    }
}