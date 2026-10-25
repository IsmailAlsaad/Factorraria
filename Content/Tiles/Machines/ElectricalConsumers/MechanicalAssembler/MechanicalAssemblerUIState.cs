using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.MechanicalAssembler
{
    public class MechanicalAssemblerUIState : MachineUIStateBase
    {
        MechanicalAssemblerTileEntity Entity => (MechanicalAssemblerTileEntity)CurrentEntity;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            // Ingredient slots on the left: slot 0 stays put, every extra slot goes one step further left.
            // No fuel slot, so the ingredient slots start at index 0.
            int max = MechanicalAssemblerRecipeRegistry.Book.MaxIngredientCount;
            for (int i = 0; i < max; i++)
            {
                int ingredientIndex = i;
                list.Add(InputSlotEntry(i, new Vector2(-60 - i * 60, 0), () => ingredientIndex < Entity.ActiveIngredientCount));
            }

            // Product slot on the right, recipe browser next to it.
            Vector2 productSlotPosition = new Vector2(54, 0);
            list.Add(OutputSlotEntry(0, productSlotPosition));

            AddRecipePicker(list, MechanicalAssemblerRecipeRegistry.Book,
                buttonPos: productSlotPosition + new Vector2(50, 40),
                browserPos: productSlotPosition + new Vector2(80, 40));

            list.Add(new MachineUIElementEntry(
                new ElectricityUIIcon(Entity.GetUseFraction, null, () => Entity.WireToggle, Entity),
                new Vector2(8, -50), new Vector2(40, 40)));

            return list;
        }
    }
}