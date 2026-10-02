using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;

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

            //list.Add(new MachineUIElementEntry(
            //new ElectricityUIIcon(Entity.GetUseFraction),
            //new Vector2(8, -50), new Vector2(40, 40)));

            return list;
        }
    }
}
