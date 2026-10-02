using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace;
using Factorraria.Content.Tiles.Machines.Solidifier;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.Solidifier
{
    internal class SolidifierUIState : MachineUIStateBase
    {
        SolidifierTileEntity Entity => (SolidifierTileEntity)CurrentEntity;

        protected override Vector2 BasePanelSize => new Vector2(100, 100);
        //protected override Vector2 BasePanelOffset => new Vector2(-130, 0);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            var LiquidTank1 = new LiquidTankUIElement(() => Entity.InputLiquids[0]);
            list.Add(new MachineUIElementEntry(LiquidTank1, new Vector2(-25, -20), new Vector2(16, 84)));

            var LiquidTank2 = new LiquidTankUIElement(() => Entity.InputLiquids[1]);
            list.Add(new MachineUIElementEntry(LiquidTank2, new Vector2(-50, -20), new Vector2(16, 84)));

            list.Add(OutputSlotEntry(0, new Vector2(55f, 0f)));

            list.Add(new MachineUIElementEntry(
                new ElectricityUIIcon(Entity.GetUseFraction),
                new Vector2(8, -50), new Vector2(40, 40)));

            return list;
        }
    }
}
