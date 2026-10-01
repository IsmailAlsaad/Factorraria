using Factorraria.Common.UI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.Autohammer
{
    public class AutohammerUIState : MachineUIStateBase
    {
        protected override Vector2 BasePanelSize => new Vector2(100, 100);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            list.Add(InputSlotEntry(0, new Vector2(-60, 0)));
            list.Add(OutputSlotEntry(0, new Vector2(54, 0)));

            return list;
        }
    }
}
