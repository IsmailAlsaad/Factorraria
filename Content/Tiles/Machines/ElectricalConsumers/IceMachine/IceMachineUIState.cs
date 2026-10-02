using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine
{
    internal class IceMachineUIState : MachineUIStateBase
    {
        IceMachineTileEntity Entity => (IceMachineTileEntity)CurrentEntity;

        protected override Vector2 BasePanelSize => new Vector2(100, 100);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            var LiquidTank = new LiquidTankUIElement(() => Entity.InputLiquids[0]);
            list.Add(new MachineUIElementEntry(LiquidTank, new Vector2(-30, -20), new Vector2(16, 84)));

            list.Add(OutputSlotEntry(0, new Vector2(55f, 0f)));

            return list;
        }
    }
}
