using Factorraria.Common.UI;
using Factorraria.Common.UI.CustomUIElements;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.Autohammer;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Hellforge;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;

namespace Factorraria.Content.Tiles.Machines.Autohammer
{
    public class AutohammerUIState : MachineUIStateBase
    {
        protected override Vector2 BasePanelSize => new Vector2(100, 100);

        AutohammerTileEntity Entity => (AutohammerTileEntity)CurrentEntity;

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            list.Add(InputSlotEntry(0, new Vector2(-60, 0)));
            Vector2 productSlotPosition = new Vector2(54, 0);
            list.Add(OutputSlotEntry(0, productSlotPosition));

            AddRecipePicker(list, AutohammerRecipeRegistry.Book,
                buttonPos: productSlotPosition + new Vector2(50, 40),
                browserPos: productSlotPosition + new Vector2(80, 40));

            list.Add(new MachineUIElementEntry(
            new ElectricityUIIcon(Entity.GetUseFraction),
            new Vector2(8, -50), new Vector2(40, 40)));

            return list;
        }
    }
}
