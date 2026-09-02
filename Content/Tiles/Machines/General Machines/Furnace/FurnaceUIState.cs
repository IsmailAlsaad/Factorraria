using Factorraria.Common.UI;
using Factorraria.Content.Configs;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using tModPorter;

namespace Factorraria.Content.Tiles.Machines.Furnace
{
    public class FurnaceUIState : MachineUIStateBase
    {
        FurnaceTileEntity Furnace => (FurnaceTileEntity)CurrentEntity;

        protected override Vector2 BasePanelSize => new Vector2(100, 200);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            var oreSlot = new UIItemSlotWrapper(
                ItemSlot.Context.ChestItem,
                () => Furnace.InputSlots[0],
                v => Furnace.InputSlots[0] = v,
                item => FurnaceRecipeRegistry.SmeltingRecipes.ContainsKey(item.type)
            );
            list.Add(new MachineUIElementEntry(oreSlot, new Vector2(0, 0), new Vector2(54, 54)));

            var fireUI = new FireUIElement(() => Furnace.GetSmeltPercent());
            list.Add(new MachineUIElementEntry(fireUI, new Vector2(0, 50), new Vector2(54, 54)));

            var fuelSlot = new UIItemSlotWrapper(
                ItemSlot.Context.ChestItem,
                () => Furnace.InputSlots[1],
                v => Furnace.InputSlots[1] = v,
                item => FurnaceRecipeRegistry.ValidFuels.ContainsKey(item.type)
            );
            list.Add(new MachineUIElementEntry(fuelSlot, new Vector2(0, 100), new Vector2(54, 54)));

            return list;
        }
    }
}
