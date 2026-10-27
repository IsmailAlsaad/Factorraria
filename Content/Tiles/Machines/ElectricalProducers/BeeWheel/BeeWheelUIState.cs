using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.BeeWheel
{
    // Only the electricity icon (power on/off wire toggle and use fraction), like the power poles.
    public class BeeWheelUIState : MachineUIStateBase
    {
        BeeWheelTileEntity Entity => (BeeWheelTileEntity)CurrentEntity;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            list.Add(new MachineUIElementEntry(
                new ElectricityUIIcon(Entity.GetUseFraction, null, () => Entity.WireToggle, Entity),
                new Vector2(20, -100), new Vector2(40, 40)));

            return list;
        }
    }
}