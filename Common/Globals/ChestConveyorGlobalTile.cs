using Factorraria.Common.Systems;
using Factorraria.Content.VirtualItems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Globals
{
    // Feeds ChestConveyorSystem's registry. PlaceInWorld only runs for tiles a PLAYER places, which is exactly the rule:
    //   - player places a chest            -> look at it (joins the registry if a conveyor is under it)
    //   - player places a conveyor         -> look at the tile above (a tile swap under an existing chest joins it)
    //   - player places any other tile     -> if a registered chest stands on it, re-check that chest (it may have lost its conveyor)
    // Nothing is decided here: the hook only queues a probe, the system handles it next tick when the world is in its final state.
    // A destroyed chest is dropped lazily by the system's per-tick validation, so KillTile needs no hook.
    public class ChestConveyorGlobalTile : GlobalTile
    {
        public override void PlaceInWorld(int i, int j, int type, Item item)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                return;
            }

            if (TileID.Sets.BasicChest[type])
            {
                ChestConveyorSystem.QueueProbe(i, j, true);
                return;
            }

            if (VirtualItemSystem.IsConveyorTile(i, j, out _, out _))
            {
                ChestConveyorSystem.QueueProbe(i, j - 1, true);
                return;
            }

            if (ChestConveyorSystem.HasListedChestAbove(i, j))
            {
                ChestConveyorSystem.QueueProbe(i, j - 1, false);
            }
        }
    }
}