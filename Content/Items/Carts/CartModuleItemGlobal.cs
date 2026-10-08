using Factorraria.Common.Carts;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Carts
{
    /// <summary>Tooltip on every chest and on the Terrarium: they are the modules you install into a cart.</summary>
    public class CartModuleItemGlobal : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return lateInstantiation && (Cart.IsChestItem(entity) || Cart.IsTerrariumItem(entity));
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "FactorrariaCartModuleInstall", "Left click on a cart to place down"));
            tooltips.Add(new TooltipLine(Mod, "FactorrariaCartModulePickup", "Shift + Right click to pick it up"));
        }
    }
}
