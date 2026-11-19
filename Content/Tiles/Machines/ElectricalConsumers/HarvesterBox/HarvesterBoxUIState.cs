using Factorraria.Common.Machines;
using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox
{
    // The right-click panel of a Harvester Box: only the electric icon (the same entry the power poles and the Robotic Arm use), drawn above
    // the tile that was clicked. One state for all 8 box kinds. No new art: the icon is Lightning_On / Off / Background in Common/UI/CustomUIElements.
    public class HarvesterBoxUIState : MachineUIStateBase
    {
        ElectricConsumerMachine Entity => (ElectricConsumerMachine)CurrentEntity;

        // Set by the entity right before the panel opens: half a tile (8) for an even-length row, so the icon sits between the two middle boxes.
        public float PanelShiftX;

        public override Vector2 BasePanelOffset => new Vector2(PanelShiftX, 0f);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            list.Add(new MachineUIElementEntry(
                new ElectricityUIIcon(Entity.GetUseFraction, null, () => Entity.WireToggle, Entity),
                new Vector2(-12, -60), new Vector2(40, 40)));

            return list;
        }
    }
}