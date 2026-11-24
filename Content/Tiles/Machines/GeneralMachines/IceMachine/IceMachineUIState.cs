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

            // No item-ingredient recipes yet, so this adds nothing; it fills in by itself once a recipe has item inputs.
            AddIngredientSlots(list, IceMachineRecipeRegistry.Book, 0, new Vector2(-90f, 0f));

            Vector2 productSlotPosition = new Vector2(55f, 0f);
            list.Add(OutputSlotEntry(0, productSlotPosition));

            // Same placement as the other machines (hammer icon below-right of the output slot, browser panel next to it).
            AddRecipePicker(list, IceMachineRecipeRegistry.Book,
                buttonPos: productSlotPosition + new Vector2(50, 40),
                browserPos: productSlotPosition + new Vector2(80, 40));

            return list;
        }
    }
}
