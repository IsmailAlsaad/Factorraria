using Factorraria.Common.Carts;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
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

        public override void SetDefaults(Item entity)
        {
            entity.maxStack = 99; // carts are placed, not ridden, so they stack (CartEquipPlayer keeps the equip slot at 1)
        }

        public override bool CanUseItem(Item item, Player player)
        {
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "FactorrariaCartPlace", "Left click on a minecart track to place a cart"));
            tooltips.Add(new TooltipLine(Mod, "FactorrariaCartPickup", "Shift + Right click a cart to pick it up"));
            if (item.type == ItemID.SteampunkMinecart)
            {
                tooltips.Add(new TooltipLine(Mod, "FactorrariaCartMotor", "Burns fuel to drive itself"));
            }
            else if (item.type == ItemID.HellMinecart)
            {
                tooltips.Add(new TooltipLine(Mod, "FactorrariaCartMotor", "Drenches itself in lava to drive"));
            }
            else if (item.type == ItemID.BeeMinecart)
            {
                tooltips.Add(new TooltipLine(Mod, "FactorrariaCartMotor", "Drenches itself in honey to drive"));
            }
            else if (item.type == ItemID.FishMinecart)
            {
                tooltips.Add(new TooltipLine(Mod, "FactorrariaCartMotor", "Drenches itself in water to drive"));
            }
            else if (item.type == ItemID.MinecartMech)
            {
                tooltips.Add(new TooltipLine(Mod, "FactorrariaCartMotor", "POWAHHHHHH"));
            }
            else
            {
                tooltips.Add(new TooltipLine(Mod, "FactorrariaCartRide", "Right click an empty cart to ride it"));
            }
        }
    }
}