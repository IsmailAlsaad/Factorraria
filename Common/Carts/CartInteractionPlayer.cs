using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// A left click on a cart is ours (shove / install a module). While the cursor is over a cart in range, other held
    /// items are not used, so a click does not swing a sword and shove at the same time. Cart items and legacy
    /// Track Cart items are exempt because they are how you place one.
    /// </summary>
    public class CartInteractionPlayer : ModPlayer
    {
        public override bool CanUseItem(Item item)
        {
            if (Player.whoAmI != Main.myPlayer || Main.netMode != NetmodeID.SinglePlayer || !Main.mouseLeft)
            {
                return true;
            }

            if (CartSkinTable.IsPlaceable(item))
            {
                return true;
            }

            return CartSystem.FindCartAtCursor(Player) == null;
        }

        /// <summary>While a cart inventory is open, shift-clicking an inventory item sends it into the cart.</summary>
        public override bool ShiftClickSlot(Item[] inventory, int context, int slot)
        {
            if (Player.whoAmI != Main.myPlayer || !CartChestUISystem.IsOpen || context != ItemSlot.Context.InventoryItem)
            {
                return false;
            }

            if (inventory == null || slot < 0 || slot >= inventory.Length)
            {
                return false;
            }

            Item item = inventory[slot];
            if (item == null || item.IsAir || item.favorited)
            {
                return false;
            }

            return CartChestUISystem.TryShiftInsert(item);
        }
    }
}