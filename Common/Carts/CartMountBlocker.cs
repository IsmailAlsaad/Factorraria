using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Right clicking a minecart track makes vanilla mount the player on a vanilla cart. Carts are simulated by
    /// CartSystem now, so that one trigger is cancelled: a cart mount is refused only while the right mouse button is
    /// held, no item is being used, and the cursor is on a minecart track. Every other way of mounting is untouched.
    /// </summary>
    public class CartMountBlocker : ModSystem
    {
        public override void Load()
        {
            On_Mount.SetMount += BlockTrackMount;
        }

        public override void Unload()
        {
            On_Mount.SetMount -= BlockTrackMount;
        }

        private static void BlockTrackMount(On_Mount.orig_SetMount orig, Mount self, int m, Player mountedPlayer, bool faceLeft)
        {
            if (m >= 0 && m < MountID.Sets.Cart.Length && MountID.Sets.Cart[m] && IsTrackRightClick(mountedPlayer))
            {
                return;
            }

            orig(self, m, mountedPlayer, faceLeft);
        }

        private static bool IsTrackRightClick(Player player)
        {
            if (player.whoAmI != Main.myPlayer || !Main.mouseRight || player.itemAnimation > 0)
            {
                return false;
            }

            int x = Player.tileTargetX;
            int y = Player.tileTargetY;

            if (!WorldGen.InWorld(x, y, 2))
            {
                return false;
            }

            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == TileID.MinecartTrack;
        }
    }
}