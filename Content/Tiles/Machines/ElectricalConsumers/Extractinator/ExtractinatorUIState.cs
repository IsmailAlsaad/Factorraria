using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.Extractinator
{
    public class ExtractinatorUIState : MachineUIStateBase
    {
        ExtractinatorTileEntity Entity => (ExtractinatorTileEntity)CurrentEntity;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            list.Add(InputSlotEntry(0, new Vector2(-60, 0)));

            list.Add(new MachineUIElementEntry(
                new ElectricityUIIcon(Entity.GetUseFraction),
                new Vector2(0, -40), new Vector2(40, 40)));

            return list;
        }
    }
}