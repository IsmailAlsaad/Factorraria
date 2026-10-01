using Factorraria.Common.UI;
using Factorraria.Content.Tiles.Machines.Furnace;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace
{
    public class FurnaceUIState : MachineUIStateBase
    {
        FurnaceTileEntity Entity => (FurnaceTileEntity)CurrentEntity;

        protected override Vector2 BasePanelSize => new Vector2(600, 300);

        public override Vector2 BasePanelOffset => new Vector2(-55, -60);

        MachineUIElementEntry[] ingredientEntries;
        MachineUIElementEntry fireEntry, fuelEntry, productEntry;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            int max = FurnaceRecipeRegistry.Book.MaxIngredientCount;
            ingredientEntries = new MachineUIElementEntry[max];
            for (int i = 0; i < max; i++)
            {
                int ingredientIndex = i;
                ingredientEntries[i] = InputSlotEntry(
                    FurnaceTileEntity.FirstIngredientSlot + i,
                    new Vector2(i * -60, 0),
                    () => ingredientIndex < Entity.ActiveIngredientCount);
                list.Add(ingredientEntries[i]);
            }

            fireEntry = new MachineUIElementEntry(new FireUIElement(() => Entity.GetFuelBurnFraction()), new Vector2(0, 50), new Vector2(54, 54));
            list.Add(fireEntry);

            fuelEntry = FuelSlotEntry(new Vector2(0, 100));
            list.Add(fuelEntry);

            Vector2 productSlotPosition = new Vector2(115, 50);
            productEntry = OutputSlotEntry(0, productSlotPosition);
            list.Add(productEntry);

            AddRecipePicker(list, FurnaceRecipeRegistry.Book,
                buttonPos: productSlotPosition + new Vector2(50, 40),
                browserPos: productSlotPosition + new Vector2(80, 40));

            return list;
        }

        protected override void UpdateLayout()
        {
            if (Entity == null || ingredientEntries == null || fireEntry == null || fuelEntry == null) return;

            const float slotSize = 54f, gap = 6f;
            float step = slotSize + gap;
            int n = Entity.ActiveIngredientCount;

            // Slot 0 stays put; each extra slot goes one step further left.
            for (int i = 0; i < ingredientEntries.Length; i++)
                ingredientEntries[i].BasePosition = new Vector2(-i * step, 0);

            // The row spans from -(n-1)*step to slotSize, so its center is half of the leftward extent.
            float centerX = -(n - 1) * step / 2f;
            fireEntry.BasePosition = new Vector2(centerX, 50);
            fuelEntry.BasePosition = new Vector2(centerX, 100);

            // productEntry, buttonEntry and browserEntry are not touched, so they keep the
            // positions set in BuildElements.
        }
    }
}
