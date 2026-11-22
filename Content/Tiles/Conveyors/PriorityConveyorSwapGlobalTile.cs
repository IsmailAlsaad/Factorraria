using Factorraria.Common.Net;
using Factorraria.Content.VirtualItems;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Conveyors
{
    // Block swap (replacing a tile in place with another block, WorldGen.ReplaceTile) does not go through placement, so the
    // shared PlaceInWorld hook (TilePlacedGlobalTile) never runs for it. The swap also runs the old tile's CanDrop first, which
    // calls VirtualItemSystem.OnPriorityConveyorKilled and takes the conveyor out of its filtered network. Without this hook a
    // swapped priority conveyor was left outside every network. ReplaceTile runs AFTER the tile has changed, on every process
    // that ran the swap.
    public class PriorityConveyorSwapGlobalTile : GlobalTile
    {
        public override void ReplaceTile(int i, int j, int type, int targetType, int targetStyle)
        {
            bool wasPriority = IsPriorityConveyor(type);
            bool isPriority = IsPriorityConveyor(targetType);

            if (!wasPriority && !isPriority)
                return;

            if (isPriority)
            {
                // The new tile is a priority conveyor (from a block, a vanilla belt or the other priority conveyor): treat it as a
                // placement. The consumers rebuild its network and merge the filter of its neighbours, and do nothing on a
                // multiplayer client (the server's MarkDirty sends the filtered networks to the clients).
                TilePlaced.RunConsumers(i, j, targetType);
                return;
            }

            // A priority conveyor was swapped for something else. CanDrop already removed it, this makes sure (idempotent).
            VirtualItemSystem.OnPriorityConveyorKilled(i, j);
        }

        static bool IsPriorityConveyor(int tileType)
        {
            return tileType == ModContent.TileType<ClockwisePriorityConveyorTile>()
                || tileType == ModContent.TileType<CounterClockwisePriorityConveyorTile>();
        }
    }
}