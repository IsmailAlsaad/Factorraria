using Factorraria.Common.Machines;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    // The monolith is overridden by the Materializer, so wires must not toggle it.
    public class MaterializerWireGuard : GlobalTile
    {
        public override bool PreHitWire(int i, int j, int type)
        {
            if (type == TileID.ShimmerMonolith && MachineVisualRegistry.Definitions.ContainsKey(type))
                return false;

            return true;
        }
    }
}