using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Harvester
{
    // The one place that decides where a herb may grow on a planter box. Future note (Ismail): custom growth rules such as a light level requirement go here
    // too, so keep every growth check in this class.
    public static class HarvesterBoxRules
    {
        // A herb is ready to harvest only when it is BLOOMING (tile 84), not just mature (tile 83), and only if it is the herb of its own box. A herb
        // goes from mature to blooming only when its growth rules are satisfied (HerbGrowthRules), so a box waits until the conditions are met.
        // Blooming always yields the herb and seeds. Change this one function to change when boxes harvest.
        public static bool IsReadyToHarvest(int herbX, int herbY, int boxTileType)
        {
            if (!WorldGen.InWorld(herbX, herbY, 1))
                return false;

            // Phase 5: a fully grown tree on a box that grows trees is ripe too (the harvest fells it and plants a new sapling).
            if (HarvesterBoxRegistry.BoxTreeSoil.ContainsKey(boxTileType) && HarvesterBoxTrees.IsTreeReady(herbX, herbY))
                return true;

            if (!HarvesterBoxRegistry.BoxHerbs.TryGetValue(boxTileType, out HerbStyle boxHerb))
                return false;

            Tile plant = Main.tile[herbX, herbY];
            if (!plant.HasTile || plant.TileType != TileID.BloomingHerbs)
                return false;

            return plant.TileFrameX / 18 == (int)boxHerb;
        }

        // Herbs only grow on their own box (Harvester Boxes and vanilla planter boxes alike).
        // true: soil is a planter box of that herb. false: soil is a planter box of another herb. null: soil is not a planter box (or a box we know nothing about),
        // so vanilla's own soil lists decide (clay pot and so on stay as they are).
        public static bool? HerbAllowedOnSoil(int herbStyle, Tile soil)
        {
            if (!soil.HasTile)
                return null;

            if (HarvesterBoxRegistry.NoHerbBoxes.Contains(soil.TileType))
                return false;

            if (HarvesterBoxRegistry.BoxHerbs.TryGetValue(soil.TileType, out HerbStyle boxHerb))
                return (int)boxHerb == herbStyle;

            if (soil.TileType == TileID.PlanterBox && HarvesterBoxRegistry.PlanterHerbs.TryGetValue(soil.TileFrameY / 18, out HerbStyle planterHerb))
                return (int)planterHerb == herbStyle;

            return null;
        }
    }
}