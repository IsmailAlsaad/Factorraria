using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Carts are simulated by CartSystem now, so the vanilla "ride the minecart" mount is switched off.
    /// Vanilla puts the player on a cart when they interact with a minecart track while a cart is equipped in the
    /// minecart slot (or when a cart item is used). We do not touch that code; instead any cart mount
    /// (MountID.Sets.Cart) that becomes active is dismounted again at the end of the same player update.
    /// </summary>
    public class CartMountBlocker : ModPlayer
    {
        public override void PostUpdate()
        {
            if (Player.mount.Active && MountID.Sets.Cart[Player.mount.Type])
            {
                Player.mount.Dismount(Player);
            }
        }
    }
}