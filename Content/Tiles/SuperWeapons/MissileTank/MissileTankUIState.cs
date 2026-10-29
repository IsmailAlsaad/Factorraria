using Factorraria.Common.UI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileTank
{
    // Just the Fuel gauge for now (hover shows the amount). The status text and the primed state come with patch S3.2.
    internal class MissileTankUIState : MachineUIStateBase
    {
        protected override Vector2 BasePanelSize => new Vector2(100, 100);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();
            list.Add(FuelTankEntry(0, new Vector2(-8, -20)));
            return list;
        }
    }
}