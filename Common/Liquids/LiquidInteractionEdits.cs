using ModLiquidLib.ModLoader;
using Terraria.ID;

namespace Factorraria.Common.Globals
{
    public class LiquidInteractionEdits : GlobalLiquid
    {
        public override int? LiquidMerge(int i, int j, int type, int otherLiquid)
        {
            bool waterLava =
                (type == LiquidID.Water && otherLiquid == LiquidID.Lava) ||
                (type == LiquidID.Lava && otherLiquid == LiquidID.Water);

            return waterLava ? TileID.Stone : null;
        }
    }
}