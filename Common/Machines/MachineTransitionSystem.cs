using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Common.Machines
{
    // Watches every machine for the running (isOn && isWorking) flag changing and tells it, so it can start its startup / shutdown
    // animation and fire OnStartup / OnShutdown. Purely visual and client-side: runs in single-player and on multiplayer clients
    // (where the flags arrive through the normal machine sync), never on a dedicated server.
    public class MachineTransitionSystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not BaseMachine machine)
                    continue;

                Point16 corner = machine.Position;
                Tile tile = Framing.GetTileSafely(corner.X, corner.Y);

                MachineVisualDefinition def = null;
                if (tile.HasTile)
                    MachineVisualRegistry.Definitions.TryGetValue(tile.TileType, out def);

                machine.UpdateVisualTransition(def);
            }
        }
    }
}