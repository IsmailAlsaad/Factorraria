using Factorraria.Common.UI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileTank
{
    // Just the Fuel gauge (hover shows the amount). The planned status text / primed state patch (S3.2) was dropped.
    internal class MissileTankUIState : MachineUIStateBase
    {
        protected override Vector2 BasePanelSize => new Vector2(100, 100);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();
            list.Add(FuelTankEntry(0, new Vector2(24, -90)));
            return list;
        }
    }
}