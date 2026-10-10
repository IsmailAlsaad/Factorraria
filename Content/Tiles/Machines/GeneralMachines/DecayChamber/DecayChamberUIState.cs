using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.DecayChamber
{
    internal class DecayChamberUIState : MachineUIStateBase
    {
        DecayChamberTileEntity Entity => (DecayChamberTileEntity)CurrentEntity;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            // Liquid tank (crimson / corruption water)
            var liquidTank = new LiquidTankUIElement(() => Entity.InputLiquids[0]);
            list.Add(new MachineUIElementEntry(liquidTank, new Vector2(16, -90), new Vector2(16, 84)));

            // Bar in -> bar out
            list.Add(InputSlotEntry(0, new Vector2(-60f, 0f)));
            Vector2 productSlotPosition = new Vector2(54f, 0f);
            list.Add(OutputSlotEntry(0, productSlotPosition));

            AddRecipePicker(list, DecayChamberRecipeRegistry.Book,
                buttonPos: productSlotPosition + new Vector2(50, 40),
                browserPos: productSlotPosition + new Vector2(80, 40));


            return list;
        }
    }
}