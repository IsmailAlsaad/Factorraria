using Factorraria.Common.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Hellforge
{
    public class HellforgeUIState : MachineUIStateBase
    {
        // The forge has no item-fuel slot, so its ingredient slots start at slot 0 (BaseMachine.IngredientStart).
        const int IngredientSlotStart = 0;

        HellforgeTileEntity Entity => (HellforgeTileEntity)CurrentEntity;

        public override Vector2 BasePanelOffset => new Vector2(-55, -60);

        MachineUIElementEntry[] ingredientEntries;
        MachineUIElementEntry fuelTankEntry;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            int max = HellforgeRecipeRegistry.Book.MaxIngredientCount;
            ingredientEntries = new MachineUIElementEntry[max];
            for (int i = 0; i < max; i++)
            {
                int ingredientIndex = i;
                ingredientEntries[i] = InputSlotEntry(
                    IngredientSlotStart + i,
                    new Vector2(i * -60, 0),
                    () => ingredientIndex < Entity.ActiveIngredientCount);
                list.Add(ingredientEntries[i]);
            }

            // Lava tank under the ingredient row (exact position is set in UpdateLayout).
            fuelTankEntry = FuelTankEntry(0, new Vector2(19, 60));
            list.Add(fuelTankEntry);

            Vector2 productSlotPosition = new Vector2(115, 50);
            list.Add(OutputSlotEntry(0, productSlotPosition));

            AddRecipePicker(list, HellforgeRecipeRegistry.Book,
                buttonPos: productSlotPosition + new Vector2(50, 40),
                browserPos: productSlotPosition + new Vector2(80, 40));

            return list;
        }

        protected override void UpdateLayout()
        {
            if (Entity == null || ingredientEntries == null || fuelTankEntry == null) return;

            const float step = SlotSize + 6f;
            int n = Math.Max(1, Entity.ActiveIngredientCount);

            // Slot 0 stays put; each extra slot goes one step further left.
            for (int i = 0; i < ingredientEntries.Length; i++)
                ingredientEntries[i].BasePosition = new Vector2(-i * step, 0);

            // Center the tank under the ingredient row.
            float centerX = -(n - 1) * step / 2f;
            fuelTankEntry.BasePosition = new Vector2(centerX + (SlotSize - 16f) / 2f, 60);
        }
    }
}
