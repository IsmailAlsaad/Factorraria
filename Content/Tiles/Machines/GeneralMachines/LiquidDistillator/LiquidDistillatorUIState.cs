using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.LiquidDistillator
{
    internal class LiquidDistillatorUIState : MachineUIStateBase
    {
        LiquidDistillatorTileEntity Entity => (LiquidDistillatorTileEntity)CurrentEntity;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            var LiquidTank1 = new LiquidTankUIElement(() => Entity.InputLiquids[0]);
            list.Add(new MachineUIElementEntry(LiquidTank1, new Vector2(-30, -20), new Vector2(16, 84)));

            var LiquidTank2 = new LiquidTankUIElement(() => Entity.OutputLiquids[0]);
            list.Add(new MachineUIElementEntry(LiquidTank2, new Vector2(60, -20), new Vector2(16, 84)));

            return list;
        }
    }
}
