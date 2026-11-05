using Factorraria.Common.Net;
using Factorraria.Common.Systems;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Wires
{
    // Smart cursor for the Iron / Lead Cutter on tile wires. With smart cursor on, the cutter snaps to the wired tile nearest the mouse
    // (like the vanilla wire cutter) instead of needing the cursor exactly on it. Owner only and purely client side: it only changes
    // which tile the existing cut request names; the server still checks reach (FactorrariaNet.InReach) and does the cut, so there is
    // no new packet and single-player behaves the same. Pole links are not affected (they are hit-tested against the mouse itself).
    public class WireCutterSmartCursorPlayer : ModPlayer
    {
        // How far from the mouse (in tiles) a wire can be and still be picked.
        public int SearchRadiusTiles = 4;

        // After a smart cut the target is locked: no snapping until the button is released or the mouse moves to another tile,
        // so holding the button on one spot cuts one wire instead of eating along the whole line at the use-time rate.
        bool smartLocked;
        int lockMouseTileX;
        int lockMouseTileY;

        /// <summary>Owner only. Called by the cutters right after a smart cut.</summary>
        public void LockSmartTarget()
        {
            Microsoft.Xna.Framework.Vector2 mouse = Main.MouseWorld;
            lockMouseTileX = (int)(mouse.X / 16f);
            lockMouseTileY = (int)(mouse.Y / 16f);
            smartLocked = true;
        }

        bool HoldingCutter()
        {
            int held = Player.HeldItem.type;
            return held == ModContent.ItemType<IronCutterItem>() || held == ModContent.ItemType<LeadCutterItem>();
        }

        /// <summary>
        /// Owner only. True with the tile to cut when smart cursor is on and a wired tile in reach is near the mouse; false means
        /// "use the plain mouse tile" (smart cursor off, or nothing to snap to).
        /// </summary>
        public bool TryGetTarget(out int tileX, out int tileY)
        {
            tileX = Player.tileTargetX;
            tileY = Player.tileTargetY;

            if (Main.dedServ || !FactorrariaNet.IsOwner(Player) || !Main.SmartCursorIsUsed)
                return false;

            // Inside the player update vanilla has already zoom-corrected the mouse, so Main.MouseWorld is right here.
            Microsoft.Xna.Framework.Vector2 mouse = Main.MouseWorld;
            int mouseTileX = (int)(mouse.X / 16f);
            int mouseTileY = (int)(mouse.Y / 16f);

            if (smartLocked)
            {
                if (!Player.controlUseItem || mouseTileX != lockMouseTileX || mouseTileY != lockMouseTileY)
                    smartLocked = false;
                else
                    return false;
            }

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int x = mouseTileX - SearchRadiusTiles; x <= mouseTileX + SearchRadiusTiles; x++)
            {
                for (int y = mouseTileY - SearchRadiusTiles; y <= mouseTileY + SearchRadiusTiles; y++)
                {
                    if (CustomWireSystem.WireAt(x, y) == CustomWireType.None)
                        continue;

                    if (!FactorrariaNet.InReach(Player, x, y))
                        continue;

                    float dx = mouse.X - (x * 16f + 8f);
                    float dy = mouse.Y - (y * 16f + 8f);
                    float distance = dx * dx + dy * dy;

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        tileX = x;
                        tileY = y;
                        found = true;
                    }
                }
            }

            return found;
        }

        public override void PostUpdate()
        {
            // Runs after vanilla's own smart cursor lookup, so this wins the yellow box for the cutters.
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || !HoldingCutter())
                return;

            if (TryGetTarget(out int x, out int y))
            {
                Main.SmartCursorShowing = true;
                Main.SmartCursorX = x;
                Main.SmartCursorY = y;
            }
        }
    }
}