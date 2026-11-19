using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Harvester
{
    // The one place that decides where a herb may grow on a planter box. Future note (Ismail): custom growth rules such as a light level requirement go here
    // too, so keep every growth check in this class.
    public static class HarvesterBoxRules
    {
        // A herb is ready to harvest at the Mature and Blooming stages (tiles 83 and 84), and only if it is the herb of its own box. The harvest
        // takes the vanilla yield for the stage it finds (mature outside its blooming condition: the herb only; blooming: herb and seeds).
        // Change this one function to change when boxes harvest (for example to wait for the blooming stage).
        public static bool IsReadyToHarvest(int herbX, int herbY, int boxTileType)
        {
            // Phase 5: a grown tree standing on a box that grows trees is ready too (the trunk base sits where a herb would).
            if (HarvesterBoxTrees.IsGrownTree(herbX, herbY, boxTileType))
                return true;

            if (!WorldGen.InWorld(herbX, herbY, 1) || !HarvesterBoxRegistry.BoxHerbs.TryGetValue(boxTileType, out HerbStyle boxHerb))
                return false;

            Tile plant = Main.tile[herbX, herbY];
            if (!plant.HasTile || (plant.TileType != TileID.MatureHerbs && plant.TileType != TileID.BloomingHerbs))
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

            if (HarvesterBoxRegistry.BoxHerbs.TryGetValue(soil.TileType, out HerbStyle boxHerb))
                return (int)boxHerb == herbStyle;

            if (soil.TileType == TileID.PlanterBox && HarvesterBoxRegistry.PlanterHerbs.TryGetValue(soil.TileFrameY / 18, out HerbStyle planterHerb))
                return (int)planterHerb == herbStyle;

            return null;
        }
    }
}