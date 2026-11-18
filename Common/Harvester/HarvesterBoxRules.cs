using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Harvester
{
    // The one place that decides where a herb may grow on a planter box. Future note (Ismail): custom growth rules such as a light level requirement go here
    // too, so keep every growth check in this class.
    public static class HarvesterBoxRules
    {
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