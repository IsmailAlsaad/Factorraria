using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Right clicking a minecart track makes vanilla send the player onto a vanilla cart (it latches a hook to the
    /// track and mounts them). Carts are simulated by CartSystem now, so that click is cancelled, in three layers so
    /// that whichever one vanilla really goes through is covered. Each layer writes one line to the mod log the first
    /// few times it fires, so the log shows which layer did the work.
    ///   1. Player.TileInteractionsUse (the world right-click handler) is skipped when the tile is a minecart track.
    ///   2. A grappling hook created by that right click is removed (see CartTrackHookBlocker).
    ///   3. Mount.SetMount refuses a cart mount while right mouse is held over a track.
    /// Every other way of mounting (items, keybinds) and normal grappling hooks are untouched.
    /// Minecart tracks, booster tracks and pressure plate tracks are all TileID.MinecartTrack (different frames).
    /// </summary>
    public class CartMountBlocker : ModSystem
    {
        private static int logged;

        public override void Load()
        {
            On_Player.TileInteractionsUse += BlockTrackInteraction;
            On_Mount.SetMount += BlockTrackMount;
        }

        public override void Unload()
        {
            On_Player.TileInteractionsUse -= BlockTrackInteraction;
            On_Mount.SetMount -= BlockTrackMount;
        }

        public static bool IsTrackTile(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 2))
            {
                return false;
            }

            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == TileID.MinecartTrack;
        }

        public static void Log(string what)
        {
            if (logged < 8)
            {
                logged++;
                ModContent.GetInstance<CartMountBlocker>().Mod.Logger.Info("Cart track click blocked: " + what);
            }
        }

        private static void BlockTrackInteraction(On_Player.orig_TileInteractionsUse orig, Player self, int myX, int myY)
        {
            if (self.whoAmI == Main.myPlayer && IsTrackTile(myX, myY))
            {
                Log("layer 1, TileInteractionsUse skipped");
                return;
            }

            orig(self, myX, myY);
        }

        private static void BlockTrackMount(On_Mount.orig_SetMount orig, Mount self, int m, Player mountedPlayer, bool faceLeft)
        {
            if (m >= 0 && m < MountID.Sets.Cart.Length && MountID.Sets.Cart[m] && IsTrackRightClick(mountedPlayer))
            {
                Log("layer 3, SetMount refused");
                return;
            }

            orig(self, m, mountedPlayer, faceLeft);
        }

        public static bool IsTrackRightClick(Player player)
        {
            return player.whoAmI == Main.myPlayer
                && Main.mouseRight
                && player.itemAnimation <= 0
                && IsTrackTile(Player.tileTargetX, Player.tileTargetY);
        }
    }

    /// <summary>
    /// Layer 2: a grappling hook that appears while right mouse is held over a track (and no item is being used) is
    /// the track click's hook, so it is removed on the spot and can never latch onto a track.
    /// </summary>
    public class CartTrackHookBlocker : GlobalProjectile
    {
        public override bool InstancePerEntity
        {
            get { return true; }
        }

        private bool fromTrackClick;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return lateInstantiation && entity.aiStyle == ProjAIStyleID.Hook;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (projectile.owner == Main.myPlayer && CartMountBlocker.IsTrackRightClick(Main.LocalPlayer))
            {
                fromTrackClick = true;
                CartMountBlocker.Log("layer 2, hook from the track click removed");
                projectile.Kill();
            }
        }

        public override bool? GrappleCanLatchOnTo(Projectile projectile, Player player, int x, int y)
        {
            if (fromTrackClick && CartMountBlocker.IsTrackTile(x, y))
            {
                return false;
            }

            return null;
        }
    }
}