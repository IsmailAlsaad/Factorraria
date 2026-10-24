using Factorraria.Common.Net;
using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    public class RoboticArmUIState : MachineUIStateBase
    {
        RoboticArmTileEntity Entity => (RoboticArmTileEntity)CurrentEntity;

        // The slots and the power icon disappear (and stop taking clicks) while an area handle is being dragged.
        bool ShowElements() => !ModContent.GetInstance<RoboticArmAreaEditorSystem>().DraggingHandle;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            // Tool / block slot on the left. The cursor can only drop a tool or a block into it.
            // Later make it cycle drawing faded icons of tools and blocks items from the players inventory on the input slot (same style as the recipe book faded item sprites)
            // first pickaxe it sees -> first axe -> first hammer -> first block -> second pickaxe -> second... so on
            list.Add(new MachineUIElementEntry(
                new UIItemSlotWrapper(ItemSlot.Context.ChestItem,
                    () => Entity.InputSlots[0],
                    v => Entity.InputSlots[0] = v,
                    ShowElements,
                    item => Entity.AcceptsSlotItem(MachineSlotKind.Input, 0, item),
                    null, null,
                    new MachineSlotBinding(() => Entity, MachineSlotKind.Input, 0)),
                new Vector2(-20, 30), new Vector2(SlotSize, SlotSize)));

            list.Add(new MachineUIElementEntry(
                new ElectricityUIIcon(Entity.GetUseFraction, ShowElements, () => Entity.WireToggle),
                new Vector2(-8, -70), new Vector2(40, 40)));

            return list;
        }
    }
}