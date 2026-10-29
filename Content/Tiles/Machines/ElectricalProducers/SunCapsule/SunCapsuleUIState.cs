using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.SunCapsule
{
    // Only the electricity icon, like the power poles, but it shows the capsule's own charge: the bolt fills from the bottom up with
    // stored / capacity and the hover text reads "stored/capacity". The background pulses only while it charges or discharges.
    public class SunCapsuleUIState : MachineUIStateBase
    {
        SunCapsuleTileEntity Entity => (SunCapsuleTileEntity)CurrentEntity;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            list.Add(new MachineUIElementEntry(
                new ElectricityUIIcon(Entity.GetChargeFraction, null, () => Entity.WireToggle, Entity)
                {
                    HoverOverride = Entity.GetChargeReadout,
                    PulseWhen = () => Entity.FlowState != 0,
                    EmptyAtZero = true
                },
                new Vector2(-4, -60), new Vector2(40, 40)));

            return list;
        }
    }
}