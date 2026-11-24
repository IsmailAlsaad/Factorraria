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
    // edge pass (BuildEdgeRuns). While Main.SmartCursorIsUsed, every cart within the player's click range (the normal tile interaction
    // range, see CartSystem.IsInInteractRange) gets an outline: white = in range, gold = the cart the smart cursor targets.
    // Carts take part in the machines' smart cursor targeting (UpdateTarget -> ConsiderCarts): the closest thing to the cursor within
    // SmartRadiusPixels wins, a tie goes to the machine, and vanilla's own target wins when it is closer. A right click then acts on the
    // targeted cart (CartSystem.FindCartForRightClick); left clicks still only act on a cart directly under the cursor.
    // The outline is built in the cart's own space (relative to the rail point, x right, y down) from the pixels that are drawn: the body
    // sprite, its extra layer and the installed chest / terrarium icon (not the liquid or the rider). It is cached per sprite frame / facing /
    // module, then each run is drawn as a rotated rectangle (same rotation as CartSystem.DrawCartSprite), so it follows the cart on slopes.
    // Called from CartSystem.PostDrawTiles right after the cart is drawn, so it sits over the cart and the module.
    public partial class MachineSmartOutlineSystem
    {
        readonly Dictionary<ulong, Outline> cartOutlineCache = new Dictionary<ulong, Outline>();

        // The cart the smart cursor targets this tick (null = none, or a machine / vanilla target is closer). Set by UpdateTarget.
        Cart smartCartTarget;
        public Cart SmartCartTarget => smartCartTarget;


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
            target = smartCartTarget;

            return true;
        }

        // Carts compete with the machines for the smart cursor: the closest one to the cursor wins (a tie goes to the machine). Returns whether
        // there is a target now (a machine or a cart); bestDistance is updated to the winner's distance, like the machine loop in UpdateTarget.
        bool ConsiderCarts(Vector2 mouse, Player player, ref float bestDistance, bool found)
        {
            smartCartTarget = null;

            foreach (Cart cart in CartSystem.Carts)
            {
                if (!cart.Active || !CartSystem.IsInInteractRange(player, cart, 0f))
                    continue;

                float distance = CartGeometry.Contains(cart, mouse, 4f) ? 0f : CartGeometry.DistanceTo(cart, mouse);
                if (distance > bestDistance || (found && distance >= bestDistance))
                    continue;

                bestDistance = distance;
                smartCartTarget = cart;
                found = true;
            }

            return found;
        }

        public void DrawCartOutline(Cart cart, Vector2 screen, bool selected)
        {
            // Same reach rule the click uses: if the click would be ignored, there is nothing to highlight.
            if (!CartSystem.IsInInteractRange(cartOutlinePlayer, cart, 0f))
                return;

            CartSkinTable.PlacedSprite sprite = CartSkinTable.GetPlacedSprite(cart.SkinType, cart.AnimFrame);
            if (sprite.Texture == null)
                return;

            Outline outline = GetCartOutline(cart, sprite);
            if (outline == null || outline.Runs.Count == 0)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new Rectangle(0, 0, 1, 1);   // MagicPixel is probably not 1x1
            Color color = selected ? SelectedColor : HighlightColor;
            float cos = (float)Math.Cos(cart.Rotation);
            float sin = (float)Math.Sin(cart.Rotation);

            foreach (Rectangle run in outline.Runs)
            {
                // Runs are in the cart's own space relative to the rail point; rotate them about it like the sprite.
                float lx = outline.OriginX + run.X;
                float ly = outline.OriginY + run.Y;
                Vector2 rotated = new Vector2(lx * cos - ly * sin, lx * sin + ly * cos);

                Main.spriteBatch.Draw(pixel, screen + rotated, one, color, cart.Rotation, Vector2.Zero, new Vector2(run.Width, run.Height), SpriteEffects.None, 0f);
            }
        }

        Outline GetCartOutline(Cart cart, CartSkinTable.PlacedSprite sprite)
        {
            if (!FitsTexture(sprite.Texture, sprite.Source))
                return null;

            Texture2D extra = sprite.Extra != null && FitsTexture(sprite.Extra, sprite.Source) ? sprite.Extra : null;
            bool flipped = cart.Facing < 0;
            int cellSize = Math.Max(1, OutlineScale);
            CartSystem.TryGetModuleSprite(cart, sprite, out Texture2D icon, out float lift, out float moduleScale);   // icon = null: no module

            ulong signature = 14695981039346656037UL;
            signature = Mix(signature, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(sprite.Texture));
            signature = Mix(signature, extra == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(extra));
            signature = Mix(signature, icon == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(icon));
            signature = Mix(signature, sprite.Source.X);
            signature = Mix(signature, sprite.Source.Y);
            signature = Mix(signature, sprite.Source.Width);
            signature = Mix(signature, sprite.Source.Height);
            signature = Mix(signature, BitConverter.SingleToInt32Bits(sprite.Origin.X));
            signature = Mix(signature, BitConverter.SingleToInt32Bits(sprite.Origin.Y));
            signature = Mix(signature, BitConverter.SingleToInt32Bits(sprite.Scale));
            signature = Mix(signature, BitConverter.SingleToInt32Bits(lift));
            signature = Mix(signature, BitConverter.SingleToInt32Bits(moduleScale));
            signature = Mix(signature, flipped ? 1 : 0);
            signature = Mix(signature, cellSize);
            signature = Mix(signature, AlphaThreshold);
            signature = Mix(signature, OutlineInside ? 1 : 0);

            if (cartOutlineCache.TryGetValue(signature, out Outline cached))
                return cached;

            if (cartOutlineCache.Count >= 256)
                cartOutlineCache.Clear();

            float scale = sprite.Scale;
            float originX = sprite.Origin.X;
            float originY = sprite.Origin.Y;

            // Bounds of everything that is drawn, in pixels relative to the rail point (x right, y down).
            float minXf = -originX * scale;
            float maxXf = (sprite.Source.Width - originX) * scale;
            float minYf = -originY * scale;
            float maxYf = (sprite.Source.Height - originY) * scale;
            if (icon != null)
            {
                minXf = Math.Min(minXf, -icon.Width * 0.5f * moduleScale);
                maxXf = Math.Max(maxXf, icon.Width * 0.5f * moduleScale);
                minYf = Math.Min(minYf, -icon.Height * moduleScale - lift);
                maxYf = Math.Max(maxYf, -lift);
            }

            int minX = FloorTo((int)Math.Floor(minXf), cellSize) - cellSize;   // one grid cell of room for the outline
            int minY = FloorTo((int)Math.Floor(minYf), cellSize) - cellSize;
            int maxX = CeilTo((int)Math.Ceiling(maxXf), cellSize) + cellSize;
            int maxY = CeilTo((int)Math.Ceiling(maxYf), cellSize) + cellSize;
            int width = (maxX - minX) / cellSize;
            int height = (maxY - minY) / cellSize;
            bool[] solid = new bool[width * height];

            MarkCartSolid(solid, width, height, minX, minY, cellSize, sprite.Texture, sprite.Source, flipped, originX, originY, scale, 0f);
            if (extra != null)
                MarkCartSolid(solid, width, height, minX, minY, cellSize, extra, sprite.Source, flipped, originX, originY, scale, 0f);
            if (icon != null)
                MarkCartSolid(solid, width, height, minX, minY, cellSize, icon, new Rectangle(0, 0, icon.Width, icon.Height), flipped, icon.Width * 0.5f, icon.Height, moduleScale, lift);

            Outline built = new Outline { OriginX = minX, OriginY = minY, Runs = BuildEdgeRuns(solid, width, height, cellSize) };
            cartOutlineCache[signature] = built;
            return built;
        }

        // Marks the grid cells that a sprite's solid pixels land on. The sprite is drawn with its (originX, originY) on the rail point, scaled,
        // lifted by lift pixels along the cart's up, and mirrored inside its own quad when flipped (SpriteEffects.FlipHorizontally does not move the origin).
        void MarkCartSolid(bool[] solid, int width, int height, int minX, int minY, int cellSize, Texture2D texture, Rectangle source,
            bool flipped, float originX, float originY, float scale, float lift)
        {
            byte[] alpha = GetAlpha(texture);
            if (alpha == null)
                return;

            for (int sy = 0; sy < source.Height; sy++)
            {
                int rowStart = (source.Y + sy) * texture.Width + source.X;
                for (int sx = 0; sx < source.Width; sx++)
                {
                    if (alpha[rowStart + sx] <= AlphaThreshold)
                        continue;

                    int drawnX = flipped ? source.Width - 1 - sx : sx;
                    float localX = (drawnX + 0.5f - originX) * scale;
                    float localY = (sy + 0.5f - originY) * scale - lift;

                    int gridX = (int)Math.Floor((localX - minX) / cellSize);
                    int gridY = (int)Math.Floor((localY - minY) / cellSize);
                    if (gridX < 0 || gridX >= width || gridY < 0 || gridY >= height)
                        continue;

                    solid[gridY * width + gridX] = true;
                }
            }
        }
    }
}