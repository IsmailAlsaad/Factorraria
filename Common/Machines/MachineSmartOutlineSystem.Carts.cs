using Factorraria.Common.Carts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace Factorraria.Common.Machines
{
    // Smart cursor outline for carts (client-only, purely visual: nothing is synced). Same look and the same tuning fields as the machine
    // outline in MachineSmartOutlineSystem.cs (HighlightColor, SelectedColor, OutlineInside, OutlineScale, AlphaThreshold), and the same
    // edge pass (BuildEdgeRuns). While Main.SmartCursorIsUsed, every cart within the player's click range gets an outline: white = in range,
    // gold = the cart under the cursor (the one CartSystem.FindCartAtCursor returns, i.e. the one a click acts on).
    // The outline is built once per sprite frame in the sprite's own pixel space (body + extra layer), cached, then each run is drawn as a
    // rotated rectangle with the same origin / flip / rotation as CartSystem.DrawCartSprite, so it follows the cart on slopes.
    // Called from CartSystem.PostDrawTiles right after the cart is drawn, so it sits over the cart and under nothing else of ours.
    // Not outlined: the installed module icon, the liquid and the rider; only the cart body sprite.
    public partial class MachineSmartOutlineSystem
    {
        readonly Dictionary<(Texture2D body, Texture2D extra, Rectangle source, int cell, int threshold, bool inside), List<Rectangle>> cartOutlineCache
            = new Dictionary<(Texture2D body, Texture2D extra, Rectangle source, int cell, int threshold, bool inside), List<Rectangle>>();

        Player cartOutlinePlayer;

        // Once per frame, before the cart loop. False = draw no cart outlines this frame. target = the cart under the cursor (or null).
        public bool BeginCartOutlines(out Cart target)
        {
            target = null;
            cartOutlinePlayer = null;

            if (Main.dedServ || Main.gameMenu || !Main.SmartCursorIsUsed)
                return false;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead)
                return false;

            cartOutlinePlayer = player;
            if (!player.mouseInterface)
                target = CartSystem.FindCartAtCursor(player);

            return true;
        }

        public void DrawCartOutline(Cart cart, Vector2 screen, bool selected)
        {
            // Same reach rule the click uses: if the click would be ignored, there is nothing to highlight.
            if (!CartSystem.IsInInteractRange(cartOutlinePlayer, cart, 0f))
                return;

            CartSkinTable.PlacedSprite sprite = CartSkinTable.GetPlacedSprite(cart.SkinType, cart.AnimFrame);
            if (sprite.Texture == null)
                return;

            int cell = Math.Max(1, (int)Math.Round(Math.Max(1, OutlineScale) / sprite.Scale));   // one outline pixel, in sprite pixels
            List<Rectangle> runs = GetCartRuns(sprite, cell);
            if (runs == null || runs.Count == 0)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new Rectangle(0, 0, 1, 1);   // MagicPixel is probably not 1x1
            Color color = selected ? SelectedColor : HighlightColor;

            bool flipped = cart.Facing < 0;
            float cos = (float)Math.Cos(cart.Rotation);
            float sin = (float)Math.Sin(cart.Rotation);
            int spriteWidth = sprite.Source.Width;

            foreach (Rectangle run in runs)
            {
                // Runs are relative to the mask's top-left, which is one cell above/left of the sprite frame.
                int x0 = run.X - cell;
                int x1 = x0 + run.Width;
                int y0 = run.Y - cell;

                // SpriteEffects.FlipHorizontally mirrors the art inside the same quad (the origin does not move), so mirror the columns, not the origin.
                float left = (flipped ? spriteWidth - x1 - sprite.Origin.X : x0 - sprite.Origin.X) * sprite.Scale;
                float top = (y0 - sprite.Origin.Y) * sprite.Scale;
                Vector2 local = new Vector2(left, top);
                Vector2 rotated = new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
                Vector2 size = new Vector2(run.Width * sprite.Scale, run.Height * sprite.Scale);

                Main.spriteBatch.Draw(pixel, screen + rotated, one, color, cart.Rotation, Vector2.Zero, size, SpriteEffects.None, 0f);
            }
        }

        List<Rectangle> GetCartRuns(CartSkinTable.PlacedSprite sprite, int cell)
        {
            var key = (sprite.Texture, sprite.Extra, sprite.Source, cell, AlphaThreshold, OutlineInside);
            if (cartOutlineCache.TryGetValue(key, out List<Rectangle> cached))
                return cached;

            if (!FitsTexture(sprite.Texture, sprite.Source))
                return null;

            if (cartOutlineCache.Count >= 256)
                cartOutlineCache.Clear();

            // One grid cell of room on every side for the outer outline; the mask's top-left is (-cell, -cell) in sprite pixels.
            int width = (sprite.Source.Width + cell - 1) / cell + 2;
            int height = (sprite.Source.Height + cell - 1) / cell + 2;
            bool[] solid = new bool[width * height];

            MarkCartSolid(solid, width, cell, sprite.Texture, sprite.Source);
            if (sprite.Extra != null && FitsTexture(sprite.Extra, sprite.Source))
                MarkCartSolid(solid, width, cell, sprite.Extra, sprite.Source);

            List<Rectangle> runs = BuildEdgeRuns(solid, width, height, cell);
            cartOutlineCache[key] = runs;
            return runs;
        }

        void MarkCartSolid(bool[] solid, int width, int cell, Texture2D texture, Rectangle source)
        {
            byte[] alpha = GetAlpha(texture);
            if (alpha == null)
                return;

            for (int sy = 0; sy < source.Height; sy++)
            {
                int rowStart = (source.Y + sy) * texture.Width + source.X;
                for (int sx = 0; sx < source.Width; sx++)
                {
                    if (alpha[rowStart + sx] > AlphaThreshold)
                        solid[(sy / cell + 1) * width + sx / cell + 1] = true;
                }
            }
        }
    }
}