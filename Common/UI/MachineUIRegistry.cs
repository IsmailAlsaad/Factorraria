using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Factorraria.Common.UI
{
    public static class MachineUIRegistry
    {
        // Maps a tile type (e.g. TileID.Furnaces) to ONE reusable UI object for that machine type.
        // "Reusable" because we don't need a separate UI object per furnace in the world —
        // one FurnaceUIState gets reused and just points at whichever furnace was last clicked.
        public static Dictionary<int, MachineUIStateBase> Definitions = new();

        public static void Register(int tileType, MachineUIStateBase state)
        {
            Definitions[tileType] = state;
        }
    }
}
