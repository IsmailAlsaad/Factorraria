using Factorraria.Common.UI;
using Factorraria.Content.Tiles.Machines.Furnace;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Factorraria.Content.Tiles.Machines.GelBurner
{
    public class GelBurnerUIState : MachineUIStateBase
    {
        GelBurnerTileEntity Entity => (GelBurnerTileEntity)CurrentEntity;

        protected override Vector2 BasePanelSize => new Vector2(100, 100);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            // LATER MAKE THE FUEL SLOT ITSELF HAVE A
            // MASK THAT GOES FROM TOP OF THE SLOT TO THE BOTTOM

            // Flame gauge: 0..1 of the current gel left, -1 when nothing is burning
            list.Add(new MachineUIElementEntry(
                new FireUIElement(() => Entity.Fuel.GetBurnFraction(0f)),
                new Vector2(-70, 30), new Vector2(54, 54)));

            list.Add(FuelSlotEntry(new Vector2(-70, -30)));

            return list;
        }
    }
}
