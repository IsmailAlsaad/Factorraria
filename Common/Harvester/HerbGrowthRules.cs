using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Harvester
{
    // What a herb needs to grow and to bloom (Ismail's herb rules, 2026-11-21). Replaces vanilla's WorldGen.GrowAlch for every herb in the world (wild herbs, vanilla
    // planter boxes, clay pots and Harvester Boxes alike); HerbGrowthHooks calls Step from the hook.
    //
    // Stages (vanilla tiles): 82 sprout -> 83 mature -> 84 blooming. Sprout -> mature needs CanGrow. Mature -> blooming needs CanBloom. A blooming herb whose
    // CanBloom is lost (Moonglow at sunrise) slips back to mature. Blooming is the stage the Harvester Box waits for (HarvesterBoxRules.IsReadyToHarvest).
    //
    // Multiplayer: vanilla only runs GrowAlch on the server and in single-player (WorldGen.UpdateWorld skips it on clients), so these rules run there only and the
    // tile change is sent the way vanilla does it (NetMessage.SendTileSquare). Nothing here may use client-only data: no Lighting (the server has none, see
    // EstimateLight), no player biome flags (a server has no player nearby), only Main.tile, Main.time, Main.dayTime and Main.raining, which the server owns.
    public struct HerbConditions
    {
        public bool CanGrow;     // sprout -> mature allowed
        public bool CanBloom;    // mature -> blooming allowed (and a blooming herb stays blooming)
        public float Speed;      // multiplies the grow and bloom chance
    }

    public static class HerbGrowthRules
    {
        // ---- Tunables (expression-bodied so hot reload can change them) ----
        // Vanilla only picks a given tile for a random update every few minutes, so these are chances PER random update, not per tick.
        public static float GrowChance => 1f;      // sprout -> mature, per random update, before Speed
        public static float BloomChance => 0.5f;      // mature -> blooming, per random update, before Speed
        public static float WiltChance => 10f;      // blooming -> mature once the bloom conditions are lost

        public static int BiomeRadiusX => 50;         // the window of tiles around the herb that is counted for biomes (101 x 101)
        public static int BiomeRadiusY => 50;
        public static int BiomeTileThreshold => 100;  // tiles of a biome inside the window for the herb to count as being in it

        public static float DaybloomMinSpeed => 0.15f;   // speed at zero light
        public static float DaybloomMaxSpeed => 2f;      // speed at full light
        public static float BlinkrootMaxLight => 1f;   // Blinkroot only grows at or below this light (0..1)
        public static float WaterleafDesertSpeed => 1f;
        public static int RainCloudRange => 26;          // tiles above a Waterleaf that a rain cloud may be
        public static float RainStrengthMin => 0.2f;     // natural rain only counts at or above this strength (Main.maxRaining, 0..1)
        public static int SkyScanTiles => 60;            // how far up the sun check looks for a roof
        public static float IndoorSunFraction => 0.25f;  // sunlight that reaches a herb under a roof or wall
        public static int LampRadius => 7;               // light-emitting tiles (torches...) within this many tiles light the herb

        // ---- The rules ----
        public static HerbConditions Evaluate(HerbStyle herb, int x, int y)
        {
            HerbConditions c = new HerbConditions { Speed = 1f };

            switch (herb)
            {
                case HerbStyle.Daybloom:
                    // Surface layer only; the more light, the faster it grows. Blooms by day only.
                    c.CanGrow = IsSurface(y);
                    c.CanBloom = c.CanGrow && !IsNight();
                    if (c.CanGrow)
                        c.Speed = MathHelper.Lerp(DaybloomMinSpeed, DaybloomMaxSpeed, EstimateLight(x, y));
                    break;

                case HerbStyle.Moonglow:
                    // Grows in the jungle (surface or underground), blooms only at night.
                    c.CanGrow = ScanBiomes(x, y).Jungle >= BiomeTileThreshold;
                    c.CanBloom = c.CanGrow && IsNight();
                    break;

                case HerbStyle.Blinkroot:
                    // Underground or cavern, in low light.
                    c.CanGrow = IsUnderground(y) && EstimateLight(x, y) <= BlinkrootMaxLight;
                    c.CanBloom = c.CanGrow;
                    break;

                case HerbStyle.Waterleaf:
                    // Desert only, and only while it gets water: natural rain with open sky above it, or a rain cloud above it with nothing solid in between.
                    // Without water it neither grows nor blooms (a blooming one wilts). The desert speeds it up.
                    bool desert = ScanBiomes(x, y).Desert >= BiomeTileThreshold;
                    c.CanGrow = desert && HasWater(x, y);
                    c.CanBloom = c.CanGrow;
                    if (desert)
                        c.Speed = WaterleafDesertSpeed;
                    break;

                case HerbStyle.Deathweed:
                    // Corruption or crimson; blooms only at night.
                    c.CanGrow = ScanBiomes(x, y).Evil >= BiomeTileThreshold;
                    c.CanBloom = c.CanGrow && IsNight();
                    break;

                case HerbStyle.Fireblossom:
                    c.CanGrow = IsUnderworld(y);
                    c.CanBloom = c.CanGrow;
                    break;

                case HerbStyle.Shiverthorn:
                    // Snow biome, surface or underground.
                    c.CanGrow = ScanBiomes(x, y).Snow >= BiomeTileThreshold;
                    c.CanBloom = c.CanGrow;
                    break;
            }

            return c;
        }

        // One random update of the herb at (x, y). Runs on the server and in single-player only.
        public static void Step(int x, int y)
        {
            Tile plant = Main.tile[x, y];
            int style = plant.TileFrameX / 18;
            if (style < 0 || style > (int)HerbStyle.Shiverthorn)
                return;

            // Vanilla's liquid rule: only Fireblossom lives in lava, only Moonglow and Waterleaf live in water, nothing lives in honey or shimmer.
            if (!LiquidAllows((HerbStyle)style, plant))
            {
                WorldGen.KillTile(x, y);
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendTileSquare(-1, x, y, 1);
                return;
            }

            HerbConditions c = Evaluate((HerbStyle)style, x, y);

            switch (plant.TileType)
            {
                case TileID.ImmatureHerbs:
                    if (c.CanGrow && Roll(GrowChance * c.Speed))
                        SetStage(x, y, TileID.MatureHerbs);
                    break;

                case TileID.MatureHerbs:
                    if (c.CanBloom && Roll(BloomChance * c.Speed))
                        SetStage(x, y, TileID.BloomingHerbs);
                    break;

                case TileID.BloomingHerbs:
                    if (!c.CanBloom && Roll(WiltChance))
                        SetStage(x, y, TileID.MatureHerbs);
                    break;
            }
        }

        static bool Roll(float chance) => WorldGen.genRand.NextDouble() < chance;

        static void SetStage(int x, int y, ushort tileType)
        {
            Tile plant = Main.tile[x, y];
            plant.TileType = tileType;   // the frame (the herb's style) stays, exactly like vanilla's own stage change
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x, y, 1);
        }

        static bool LiquidAllows(HerbStyle herb, Tile plant)
        {
            if (plant.LiquidAmount == 0)
                return true;

            return plant.LiquidType switch
            {
                LiquidID.Water => herb == HerbStyle.Moonglow || herb == HerbStyle.Waterleaf,
                LiquidID.Lava => herb == HerbStyle.Fireblossom,
                _ => false
            };
        }

        // ---- Layers and time ----
        public static bool IsSurface(int y) => y < Main.worldSurface;
        public static bool IsUnderworld(int y) => y > Main.maxTilesY - 200;
        public static bool IsUnderground(int y) => y >= Main.worldSurface && !IsUnderworld(y);
        public static bool IsNight() => !Main.dayTime;

        // ---- Water: natural rain (surface layer, no wall behind the herb, strong enough), or a rain cloud straight above with nothing solid in between ----
        public static bool HasWater(int x, int y)
        {
            if (Main.raining && Main.maxRaining >= RainStrengthMin && IsSurface(y) && Main.tile[x, y].WallType == 0)
                return true;

            int top = Math.Max(0, y - RainCloudRange);
            for (int yy = y - 1; yy >= top; yy--)
            {
                Tile t = Main.tile[x, yy];
                if (!t.HasUnactuatedTile)
                    continue;

                if (t.TileType == TileID.RainCloud)
                    return true;

                if (Main.tileSolid[t.TileType])
                    return false;   // a block between the herb and any cloud above it
            }

            return false;
        }

        // ---- Light ----
        // The server has no lighting engine, so light is estimated from data the server owns: the sun (time of day, open sky above the herb, surface only) and
        // light-emitting tiles close by (torches, lamps and so on: Main.tileLighted). 0 = dark, 1 = full light. Same answer on the server and in single-player.
        public static float EstimateLight(int x, int y)
        {
            float sun = 0f;
            if (Main.dayTime && y < Main.worldSurface)
            {
                float day = Math.Max(0f, (float)Math.Sin(Main.time / 54000.0 * Math.PI));   // 0 at dawn and dusk, 1 at noon
                sun = day * (SkyIsOpen(x, y) ? 1f : IndoorSunFraction);
            }

            return Math.Max(sun, LampLight(x, y));
        }

        static bool SkyIsOpen(int x, int y)
        {
            int top = Math.Max(0, y - SkyScanTiles);
            for (int yy = y - 1; yy >= top; yy--)
            {
                Tile t = Main.tile[x, yy];
                if (t.WallType != 0)
                    return false;
                if (t.HasUnactuatedTile && Main.tileSolid[t.TileType])
                    return false;
            }

            return true;
        }

        static float LampLight(int x, int y)
        {
            int r = LampRadius;
            float best = 0f;
            for (int i = Math.Max(0, x - r); i <= Math.Min(Main.maxTilesX - 1, x + r); i++)
            {
                for (int j = Math.Max(0, y - r); j <= Math.Min(Main.maxTilesY - 1, y + r); j++)
                {
                    Tile t = Main.tile[i, j];
                    if (!t.HasUnactuatedTile || !Main.tileLighted[t.TileType])
                        continue;

                    float dx = i - x, dy = j - y;
                    float light = 1f - (float)Math.Sqrt(dx * dx + dy * dy) / (r + 1);
                    if (light > best)
                        best = light;
                }
            }

            return best;
        }

        // ---- Biomes ----
        // A server has no player zone flags, so a biome is a count of its tiles in a window around the herb (BiomeRadiusX / Y) against BiomeTileThreshold.
        struct BiomeCounts
        {
            public int Jungle, Desert, Evil, Snow;
        }

        static BiomeCounts ScanBiomes(int x, int y)
        {
            BiomeCounts c = default;
            int x0 = Math.Max(0, x - BiomeRadiusX), x1 = Math.Min(Main.maxTilesX - 1, x + BiomeRadiusX);
            int y0 = Math.Max(0, y - BiomeRadiusY), y1 = Math.Min(Main.maxTilesY - 1, y + BiomeRadiusY);

            for (int i = x0; i <= x1; i++)
            {
                for (int j = y0; j <= y1; j++)
                {
                    Tile t = Main.tile[i, j];
                    if (!t.HasTile)
                        continue;

                    ushort type = t.TileType;

                    if (type == TileID.JungleGrass || type == TileID.JunglePlants || type == TileID.JungleVines || type == TileID.JunglePlants2 || type == TileID.Mud)
                        c.Jungle++;
                    if (TileID.Sets.Conversion.Sand[type] || TileID.Sets.Conversion.HardenedSand[type] || TileID.Sets.Conversion.Sandstone[type])
                        c.Desert++;
                    if (TileID.Sets.Corrupt[type] || TileID.Sets.Crimson[type])
                        c.Evil++;
                    if (TileID.Sets.IcesSnow[type])
                        c.Snow++;
                }
            }

            return c;
        }
    }
}