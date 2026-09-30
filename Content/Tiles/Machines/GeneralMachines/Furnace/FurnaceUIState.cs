using Factorraria.Common.UI;
using Factorraria.Content.Configs;
using Factorraria.Content.Tiles.Machines.Furnace;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using tModPorter;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace
{
    public class FurnaceUIState : MachineUIStateBase
    {
        FurnaceTileEntity Entity => (FurnaceTileEntity)CurrentEntity;

        protected override Vector2 BasePanelSize => new Vector2(400, 300);
        public override Vector2 BasePanelOffset => new Vector2(-55, -60);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            var oreSlot = new UIItemSlotWrapper(
                ItemSlot.Context.ChestItem,
                () => Entity.InputSlots[1],
                v => Entity.InputSlots[1] = v
            );
            list.Add(new MachineUIElementEntry(oreSlot, new Vector2(0, 0), new Vector2(54, 54)));

            var fireUI = new FireUIElement(() => Entity.GetSmeltPercent());
            list.Add(new MachineUIElementEntry(fireUI, new Vector2(0, 50), new Vector2(54, 54)));

            var fuelSlot = new UIItemSlotWrapper(
                ItemSlot.Context.ChestItem,
                () => Entity.InputSlots[0],
                v => Entity.InputSlots[0] = v
            );
            list.Add(new MachineUIElementEntry(fuelSlot, new Vector2(0, 100), new Vector2(54, 54)));

            Vector2 productSlotPosition = new Vector2(115, 50);
            var productSlot = new UIItemSlotWrapper(
                    ItemSlot.Context.ChestItem,
                    () => Entity.OutputSlots[0],
                    v => Entity.OutputSlots[0] = v
            );
            list.Add(new MachineUIElementEntry(productSlot, productSlotPosition, new Vector2(54, 54)));

            var recipeBrowserList = new RecipeBrowserPanel(
                FurnaceRecipeRegistry.SmeltingRecipes,
                () => Entity.ManualRecipe,
                r => Entity.SetManualRecipe(r));
            list.Add(new MachineUIElementEntry(recipeBrowserList, productSlotPosition + new Vector2(80, 40), new Vector2(155, 200)));

            var recipeSelectButton = new RecipeSelectHammerIcon(recipeBrowserList);
            list.Add(new MachineUIElementEntry(recipeSelectButton, productSlotPosition + new Vector2(50, 40), new Vector2(24, 24)));

            return list;
        }
    }
}
