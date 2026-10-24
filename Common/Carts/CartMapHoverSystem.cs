using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Fullscreen map: hovering a cart icon shows that cart's panel (chest slots, tank, motor fuel) next to the cursor, view-only.
    /// CartMapLayer sets HoveredCartId during the map draw; PostDrawFullscreenMap draws the panel right after, the same frame.
    /// View-only because the interface is only ever Drawn, never Updated: nothing receives mouse hover or clicks.
    ///
    /// Multiplayer: the contents come from the local Cart object. Single-player always has them. A multiplayer client only has a
    /// Cart object for carts the server replicates (near the player), and only a tank travels with that replica, so there only
    /// terrarium / liquid carts show a panel; chest and fuel contents of far carts would need a new server request (not done).
    /// </summary>
    public class CartMapHoverSystem : ModSystem
    {
        /// <summary>Set by CartMapLayer while drawing the map; 0 = no cart hovered. Cleared here every frame.</summary>
        public static int HoveredCartId;

        public float HoverZoom = 1f;       // size of the preview panel (1 = same as the in-world panel at zoom 1)
        public float GapAboveCursor = 24f; // UI pixels between the bottom of the panel and the cursor

        UserInterface hoverInterface;
        CartChestUIState state;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            hoverInterface = new UserInterface();
        }

        public override void Unload()
        {
            hoverInterface = null;
            state = null;
        }

        public override void OnWorldUnload()
        {
            HoveredCartId = 0;
            Hide();
        }

        void Hide()
        {
            if (state == null)
                return;

            if (hoverInterface != null)
                hoverInterface.SetState(null);

            state = null;
        }

        // Runs at the very end of the fullscreen map draw (same place the map button is drawn, which is known to work), after every
        // map layer has had its Draw, so HoveredCartId is already set for this frame. The map draws before the interface layers
        // and tModLoader stops drawing layers as soon as one returns false, so a normal interface layer was not a safe place.
        public override void PostDrawFullscreenMap(ref string mouseText)
        {
            DrawHover();
        }

        void DrawHover()
        {
            int id = HoveredCartId;
            HoveredCartId = 0;

            if (!Main.mapFullscreen || hoverInterface == null || id <= 0 || CartMapSettings.Mode == CartMapMode.Off)
            {
                Hide();
                return;
            }

            Cart cart = CartSystem.FindById(id);
            if (cart == null || !cart.Active || !cart.HasPanel || (Main.netMode == NetmodeID.MultiplayerClient && cart.Tank == null))
            {
                Hide();
                return;
            }

            if (state == null || state.Cart != cart)
            {
                Hide();
                state = new CartChestUIState(cart, true);
                hoverInterface.SetState(state);
            }

            if (state.Panel == null)
                return;

            state.SetZoomScale(HoverZoom);

            // Same coordinates the map button uses: Main.mouseX / screenWidth already follow the active zoom.
            Vector2 mouse = new Vector2(Main.mouseX, Main.mouseY);
            float maxX = Math.Max(0f, Main.screenWidth - state.PanelW);
            float maxY = Math.Max(0f, Main.screenHeight - state.PanelH);

            float left = (float)Math.Round(MathHelper.Clamp(mouse.X - state.PanelW / 2f, 0f, maxX));
            float top = (float)Math.Round(MathHelper.Clamp(mouse.Y - state.PanelH - GapAboveCursor, 0f, maxY));

            state.Panel.Left.Set(left, 0f);
            state.Panel.Top.Set(top, 0f);
            state.Recalculate();

            hoverInterface.Draw(Main.spriteBatch, new GameTime());
        }
    }
}