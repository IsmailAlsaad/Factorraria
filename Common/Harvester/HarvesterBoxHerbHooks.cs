using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Vanilla's herb soil checks are hard-coded tile lists (WorldGen.CheckAlch decides whether a growing herb survives, WorldGen.PlaceAlch whether one can be planted).
    // These two hooks add the "herbs only grow on their own box" rule and make a Harvester Box count as a vanilla planter box for the herb it belongs to.
    // They only read tile data that every client and the server already have, so they behave the same everywhere and need no packet.
    public class HarvesterBoxHerbHooks : ModSystem
    {
        public override void Load()
        {
            On_WorldGen.CheckAlch += CheckAlchHook;
            On_WorldGen.PlaceAlch += PlaceAlchHook;
        }

        public override void Unload()
        {
            On_WorldGen.CheckAlch -= CheckAlchHook;
            On_WorldGen.PlaceAlch -= PlaceAlchHook;
        }

        public override void PostSetupContent()
        {
            HarvesterBoxRegistry.ResolvePlanterStyles(Mod);
        }

        static void CheckAlchHook(On_WorldGen.orig_CheckAlch orig, int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 2))
            {
                orig(x, y);
                return;
            }

            Tile herb = Main.tile[x, y];
            Tile soil = Main.tile[x, y + 1];
            bool? allowed = herb.HasTile ? HarvesterBoxRules.HerbAllowedOnSoil(herb.TileFrameX / 18, soil) : null;

            if (allowed == null)
            {
                orig(x, y);
                return;
            }

            if (allowed == false)
            {
                WorldGen.KillTile(x, y);   // the wrong herb on a planter box breaks like a herb that lost its soil
                return;
            }

            RunAsVanillaPlanter(soil, () => orig(x, y));
        }

        static bool PlaceAlchHook(On_WorldGen.orig_PlaceAlch orig, int x, int y, int style)
        {
            if (!WorldGen.InWorld(x, y, 2))
                return orig(x, y, style);

            Tile soil = Main.tile[x, y + 1];
            bool? allowed = HarvesterBoxRules.HerbAllowedOnSoil(style, soil);

            if (allowed == null)
                return orig(x, y, style);

            if (allowed == false)
                return false;

            bool placed = false;
            RunAsVanillaPlanter(soil, () => placed = orig(x, y, style));
            return placed;
        }

        // A Harvester Box is not in vanilla's herb soil lists, so for the length of one vanilla call it looks like a vanilla planter box (which every herb list accepts).
        // That keeps vanilla's own liquid and lava rules for each herb exactly as they are. The real type is always put back.
        static void RunAsVanillaPlanter(Tile soil, System.Action vanilla)
        {
            if (soil.TileType == TileID.PlanterBox)
            {
                vanilla();
                return;
            }

            ushort real = soil.TileType;
            soil.TileType = TileID.PlanterBox;
            try
            {
                vanilla();
            }
            finally
            {
                soil.TileType = real;
            }
        }
    }
}