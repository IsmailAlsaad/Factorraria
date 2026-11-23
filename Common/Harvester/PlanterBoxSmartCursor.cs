using System;
using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Smart cursor for our boxes, in the style of the vanilla planter box. Vanilla's planter box step (SmartCursorHelper.Step_PlanterBox) is keyed on
    // tile type 380 only, so our own box tiles never trigger it. This runs after vanilla's lookup and does the same job for them:
    //  - holding a Harvester Box or one of the plain custom planter boxes (Empty, Mushroom, Hallowed, Stone), the cursor snaps to the empty cell in reach
    //    nearest the mouse that sits right next to a box on the same row (our boxes or a vanilla planter box), so a row is built as a straight line;
    //  - if the cursor is already on a box it gets out of the way (no target), so you can still click the box itself.
    // With no box nearby there is nothing to line up with and the cursor stays where the mouse is.
    // Owner only and purely client side: it only changes which cell the normal placement uses, so there is no packet and single-player behaves the same.
    public class PlanterBoxSmartCursor : ModSystem
    {
        // How far from the mouse (in tiles) a cell can be and still be picked.
        public static int SearchRadiusTiles = 4;

        public override void Load()
        {
            On_SmartCursorHelper.SmartCursorLookup += AddBoxTarget;
        }

        static void AddBoxTarget(On_SmartCursorHelper.orig_SmartCursorLookup orig, Player player)
        {
            orig(player);

            if (Main.dedServ || player.whoAmI != Main.myPlayer || !Main.SmartCursorIsUsed)
                return;

            Item held = player.HeldItem;
            if (held == null || held.IsAir || held.createTile < 0 || !IsOurBox(held.createTile))
                return;

            Vector2 mouse = Main.MouseWorld;
            int mouseTileX = (int)(mouse.X / 16f);
            int mouseTileY = (int)(mouse.Y / 16f);

            // Cursor already on a box: no target, like vanilla.
            if (WorldGen.InWorld(mouseTileX, mouseTileY, 1))
            {
                Tile onMouse = Main.tile[mouseTileX, mouseTileY];
                if (onMouse.HasTile && IsAnyBox(onMouse.TileType))
                {
                    Main.SmartCursorShowing = false;
                    return;
                }
            }

            float best = float.MaxValue;
            int bestX = -1;
            int bestY = -1;

            for (int x = mouseTileX - SearchRadiusTiles; x <= mouseTileX + SearchRadiusTiles; x++)
            {
                for (int y = mouseTileY - SearchRadiusTiles; y <= mouseTileY + SearchRadiusTiles; y++)
                {
                    if (!WorldGen.InWorld(x, y, 2) || Main.tile[x, y].HasTile)
                        continue;

                    if (!HasBoxBeside(x, y) || !FactorrariaNet.InReach(player, x, y))
                        continue;

                    float dx = mouse.X - (x * 16f + 8f);
                    float dy = mouse.Y - (y * 16f + 8f);
                    float distance = dx * dx + dy * dy;

                    if (distance < best)
                    {
                        best = distance;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            if (bestX < 0)
                return;

            // Vanilla sets both: the yellow box reads SmartCursorX/Y, but the placement preview and the actual placement read the player's tile target.
            Main.SmartCursorShowing = true;
            Main.SmartCursorX = Player.tileTargetX = bestX;
            Main.SmartCursorY = Player.tileTargetY = bestY;
        }

        // A tile is "ours" if it is a Harvester Box or a plain custom planter box.
        static bool IsOurBox(int tileType) => HarvesterBoxRegistry.BoxTypes.Contains(tileType) || HarvesterBoxRegistry.NoHerbBoxes.Contains(tileType);

        // Ours or a vanilla planter box.
        static bool IsAnyBox(int tileType) => tileType == TileID.PlanterBox || IsOurBox(tileType);

        static bool HasBoxBeside(int x, int y)
        {
            Tile left = Main.tile[x - 1, y];
            Tile right = Main.tile[x + 1, y];
            return (left.HasTile && IsAnyBox(left.TileType)) || (right.HasTile && IsAnyBox(right.TileType));
        }
    }
}