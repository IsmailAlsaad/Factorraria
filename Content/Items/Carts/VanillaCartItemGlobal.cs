using Factorraria.Common.Carts;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Carts
{
    /// <summary>
    /// Every minecart item (vanilla or modded) now places a simulated cart. Using the item the vanilla way would mount
    /// the player, so use is blocked here; the actual placement happens in CartSystem's click handling.
    /// </summary>
    public class VanillaCartItemGlobal : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return lateInstantiation && CartSkinTable.IsSkinItem(entity);
        }

        public override bool CanUseItem(Item item, Player player)
        {
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "FactorrariaCartPlace", "Left click on a minecart track to place a cart"));
            if (item.type == Cart.SteampunkItemType)
            {
                tooltips.Add(new TooltipLine(Mod, "FactorrariaCartMotorNoModules", "Shift + Right click to pick it up"));
                tooltips.Add(new TooltipLine(Mod, "FactorrariaCartMotor", "Burns fuel to drive itself"));
            }
            else
            {
                tooltips.Add(new TooltipLine(Mod, "FactorrariaCartPickup", "Right click to pick it up"));
            }
        }
    }
}