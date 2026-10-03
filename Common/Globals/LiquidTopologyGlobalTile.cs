using Factorraria.Common.Liquids;
using Factorraria.Common.Systems;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Globals
{
    // Pipe open ends and world-liquid attachments are only discovered on a network rebuild.
    // Placing or mining ANY tile next to a pipe can change which sides are open (or blocked
    // by solid rock), so flag a rebuild when that happens.
    public class LiquidTopologyGlobalTile : GlobalTile
    {
        public override void PlaceInWorld(int i, int j, int type, Item item) => MarkIfNextToPipe(i, j);

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!fail && !effectOnly)
                MarkIfNextToPipe(i, j);
        }

        static void MarkIfNextToPipe(int i, int j)
        {
            if (IsPipe(i - 1, j) || IsPipe(i + 1, j) || IsPipe(i, j - 1) || IsPipe(i, j + 1))
                LiquidNetworkSystem.networkNeedsRebuilding = true;
        }

        static bool IsPipe(int i, int j) =>
            WorldGen.InWorld(i, j) && PipeTierRegistry.IsPipeTile(Main.tile[i, j].TileType);
    }
}
