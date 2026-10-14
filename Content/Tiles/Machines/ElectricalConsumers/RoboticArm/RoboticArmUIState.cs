using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    public class RoboticArmUIState : MachineUIStateBase
    {
        RoboticArmTileEntity Entity => (RoboticArmTileEntity)CurrentEntity;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            // Tool / block slot on the left. The cursor can only drop a tool or a block into it.
            list.Add(new MachineUIElementEntry(
                new UIItemSlotWrapper(ItemSlot.Context.ChestItem,
                    () => Entity.InputSlots[0],
                    v => Entity.InputSlots[0] = v,
                    null,
                    item => Entity.IsAcceptedItem(item)),
                new Vector2(-70, -20), new Vector2(SlotSize, SlotSize)));

            // Everything the arm mines or collects ends up here.
            list.Add(OutputSlotEntry(0, new Vector2(35, -20)));

            list.Add(new MachineUIElementEntry(
                new ElectricityUIIcon(Entity.GetUseFraction),
                new Vector2(-8, -70), new Vector2(40, 40)));

            return list;
        }
    }
}