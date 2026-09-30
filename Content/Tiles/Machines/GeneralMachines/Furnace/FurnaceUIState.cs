using Factorraria.Common.UI;
using Factorraria.Content.Configs;
using Factorraria.Content.Tiles.Machines.Furnace;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using tModPorter;
using static Terraria.ModLoader.Core.TmodFile;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace
{
    public class FurnaceUIState : MachineUIStateBase
    {
        FurnaceTileEntity Entity => (FurnaceTileEntity)CurrentEntity;

        protected override Vector2 BasePanelSize => new Vector2(400, 300);
        public override Vector2 BasePanelOffset => new Vector2(-55, -60);

        MachineUIElementEntry[] ingredientEntries;
        MachineUIElementEntry fireEntry, fuelEntry, productEntry, buttonEntry, browserEntry;


        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            int max = FurnaceRecipeRegistry.MaxIngredientCount;
            ingredientEntries = new MachineUIElementEntry[max];
            for (int i = 0; i < max; i++)
            {
                int slotIndex = FurnaceTileEntity.FirstIngredientSlot + i;
                int ingredientIndex = i;
                var slot = new UIItemSlotWrapper(
                    ItemSlot.Context.ChestItem,
                    () => Entity.InputSlots[slotIndex],
                    v => Entity.InputSlots[slotIndex] = v,
                    () => ingredientIndex < Entity.ActiveIngredientCount);

                ingredientEntries[i] = new MachineUIElementEntry(slot, new Vector2(i * -60, 0), new Vector2(54, 54)); // change input slot layout here
                list.Add(ingredientEntries[i]);
            }

            var fireUI = new FireUIElement(() => Entity.GetSmeltPercent());
            fireEntry = new MachineUIElementEntry(fireUI, new Vector2(0, 50), new Vector2(54, 54));
            list.Add(fireEntry);

            var fuelSlot = new UIItemSlotWrapper(
                ItemSlot.Context.ChestItem,
                () => Entity.InputSlots[FurnaceTileEntity.FuelSlot],
                v => Entity.InputSlots[FurnaceTileEntity.FuelSlot] = v
            );
            fuelEntry = new MachineUIElementEntry(fuelSlot, new Vector2(0, 100), new Vector2(54, 54));
            list.Add(fuelEntry);

            Vector2 productSlotPosition = new Vector2(115, 50);
            var productSlot = new UIItemSlotWrapper(
                    ItemSlot.Context.ChestItem,
                    () => Entity.OutputSlots[0],
                    v => Entity.OutputSlots[0] = v
            );
            productEntry = new MachineUIElementEntry(productSlot, productSlotPosition, new Vector2(54, 54));
            list.Add(productEntry);

            var recipeBrowserList = new RecipeBrowserPanel(
                FurnaceRecipeRegistry.SmeltingRecipes,
                () => Entity.ManualRecipe,
                r => Entity.SetManualRecipe(r));
            browserEntry = new MachineUIElementEntry(recipeBrowserList, productSlotPosition + new Vector2(80, 40), new Vector2(155, 200));
            list.Add(browserEntry);

            var recipeSelectButton = new RecipeSelectHammerIcon(recipeBrowserList);
            buttonEntry = new MachineUIElementEntry(recipeSelectButton, productSlotPosition + new Vector2(50, 40), new Vector2(24, 24));
            list.Add(buttonEntry);

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
