using Factorraria.Common.UI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    // Just the shimmer tank (liquid-fuel tank 0). Right click on the machine opens it, Shift + right click toggles the background.
    public class MagicMaterializerUIState : MachineUIStateBase
    {
        protected override List<MachineUIElementEntry> BuildElements()
        {
            return new List<MachineUIElementEntry>
            {
                FuelTankEntry(0, new Vector2(0f, 0f))
            };
        }
    }
}