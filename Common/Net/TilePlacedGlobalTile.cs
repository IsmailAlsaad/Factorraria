using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // The single shared hook for "a tile was placed" (roadmap patch 1.3). PlaceInWorld runs on the process that ran the placement
    // (the placing client, single-player, or server code that calls TileLoader.PlaceInWorld such as the Robotic Arm), and
    // TilePlaced decides what to do with it. Systems do not add their own PlaceInWorld hooks for this: they register an
    // ITilePlacedConsumer instead.
    public class TilePlacedGlobalTile : GlobalTile
    {
        public override void PlaceInWorld(int i, int j, int type, Item item)
        {
            TilePlaced.NotifyPlaced(i, j, type);
        }
    }
}