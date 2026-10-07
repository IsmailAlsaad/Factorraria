using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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
    }
}