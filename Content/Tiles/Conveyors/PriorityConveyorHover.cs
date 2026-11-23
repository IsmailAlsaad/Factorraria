using Factorraria.Common.UI.CustomUIElements;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace Factorraria.Content.Tiles.Conveyors
{
    // Hover text for the two priority conveyor tiles (drawn at the cursor by ElectricityHoverTip, which supports colours).
    // Client-side only: the filter networks are replicated to clients (ConveyorFilterNet), so TryGetConveyorFilter works in multiplayer too.
    //   filter set:  "{item name}: High" / "Others: Low"
    //   no filter:   "All Items: Medium"
    public static class PriorityConveyorHover
    {
        public static void Show(int i, int j)
        {
            if (Main.dedServ) return;

            Color white = Color.White;
            Color high = new Color(255, 91, 41);
            Color medium = new Color(240, 220, 70);
            Color low = new Color(90, 220, 90);

            if (VirtualItemSystem.TryGetConveyorFilter(i, j, out int filterItemId))
            {
                ElectricityHoverTip.ShowLines(
                    new[] { (Lang.GetItemNameValue(filterItemId) + ": ", white), ("High", high) },
                    new[] { ("Others: ", white), ("Low", low) });
            }
            else
            {
                ElectricityHoverTip.ShowLines(
                    new[] { ("All Items: ", white), ("Medium", medium) });
            }
        }
    }
}