using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    // One UI shared by both pole variants: only the electric icon (same entry as the Robotic Arm's).
    public class PowerPoleUIState : MachineUIStateBase
    {
        PowerPoleTileEntityBase Entity => (PowerPoleTileEntityBase)CurrentEntity;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            list.Add(new MachineUIElementEntry(
                new ElectricityUIIcon(Entity.GetUseFraction, null, () => Entity.WireToggle, Entity),
                new Vector2(-8, -70), new Vector2(40, 40)));

            return list;
        }
    }
}