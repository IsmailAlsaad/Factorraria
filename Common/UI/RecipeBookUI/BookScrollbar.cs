using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>
    /// Flat parchment-coloured scrollbar for the recipe book. Draws and drags itself (vanilla's drag depends on the
    /// vanilla textures, same reason as ZoomScrollbar). Invisible and inert while there is nothing to scroll.
    /// Feed it with SetBookView(view, max) instead of SetView.
    /// </summary>
    public class BookScrollbar : UIScrollbar
    {
        const float MinThumb = 18f;

        float view, max, dragOffset;
        bool dragging;

        /// <summary>True when the content is taller than the visible area.</summary>
        public bool Needed => max > view + 0.5f;

        public BookScrollbar()
        {
            Width.Set(RecipeBookLayout.ScrollbarWidth, 0f);
            MaxWidth.Set(RecipeBookLayout.ScrollbarWidth, 0f);
            PaddingTop = PaddingBottom = 0f;
        }

        public void SetBookView(float viewSize, float maxViewSize)
        {
            view = viewSize;
            max = maxViewSize;
            SetView(viewSize, maxViewSize);
        }

        Rectangle ThumbRect(out float travel, out float range)
        {
            CalculatedStyle inner = GetInnerDimensions();
            float track = inner.Height;
            float h = MathHelper.Clamp(track * view / System.Math.Max(max, 1f), MinThumb, track);
            travel = track - h;
            range = System.Math.Max(max - view, 0f);
            float y = range > 0f ? inner.Y + travel * ViewPosition / range : inner.Y;
            return new Rectangle((int)inner.X, (int)y, (int)inner.Width, (int)h);
        }

        public override void LeftMouseDown(UIMouseEvent evt)
        {
            if (!Needed) return;
            Rectangle t = ThumbRect(out _, out _);
            dragging = true;
            if (t.Contains(evt.MousePosition.ToPoint()))
                dragOffset = evt.MousePosition.Y - t.Y;
            else
                dragOffset = t.Height / 2f;   // clicking the track centers the thumb on the click, then keeps dragging
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Needed && IsMouseHovering)
                Main.LocalPlayer.mouseInterface = true;
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            if (!Needed)
            {
                dragging = false;
                return;
            }

            Vector2 mouse = UserInterface.ActiveInstance.MousePosition;
            CalculatedStyle inner = GetInnerDimensions();

            if (dragging)
            {
                if (!Main.mouseLeft) dragging = false;
                else
                {
                    ThumbRect(out float travel, out float range);
                    if (travel > 0f)
                        ViewPosition = (mouse.Y - inner.Y - dragOffset) / travel * range;   // setter clamps
                }
            }

            Rectangle outer = GetDimensions().ToRectangle();
            BookDraw.Frame(sb, outer, RecipeBookLayout.ScrollTrack, RecipeBookLayout.ScrollTrackOutline, 1);

            Rectangle t = ThumbRect(out _, out _);
            bool hover = t.Contains(mouse.ToPoint());
            Color fill = dragging ? RecipeBookLayout.ScrollThumbDrag
                       : hover ? RecipeBookLayout.ScrollThumbHover
                       : RecipeBookLayout.ScrollThumb;
            BookDraw.Frame(sb, t, fill, RecipeBookLayout.PageBorder, 1);
        }
    }
}