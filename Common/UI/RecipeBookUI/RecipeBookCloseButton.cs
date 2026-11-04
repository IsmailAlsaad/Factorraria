using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.UI;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>
    /// Small "X" in the book's top border (right end of the drag bar) that closes the book. It is appended after the drag handle,
    /// so it sits on top of it and a click on the X never starts a drag. Placeholder X is drawn from rectangles until
    /// RecipeBookClose.png exists (see RecipeBookArt). The close runs from Update, not from the click itself, so the book's
    /// UserInterface is never torn down in the middle of its own event handling.
    /// </summary>
    public class RecipeBookCloseButton : UIElement
    {
        bool closeRequested;

        public RecipeBookCloseButton()
        {
            Rectangle b = RecipeBookLayout.CloseButton;
            Left.Set(b.X, 0f);
            Top.Set(b.Y, 0f);
            Width.Set(b.Width, 0f);
            Height.Set(b.Height, 0f);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            closeRequested = true;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (IsMouseHovering) Main.LocalPlayer.mouseInterface = true;

            if (closeRequested)
            {
                closeRequested = false;
                RecipeBookSystem.Close(true);   // plays the close sound
            }
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();
            bool hover = IsMouseHovering;

            var art = hover && RecipeBookArt.CloseButtonHover != null ? RecipeBookArt.CloseButtonHover : RecipeBookArt.CloseButton;
            if (art != null)
            {
                Texture2D tex = art.Value;
                sb.Draw(tex, new Vector2(r.Center.X - tex.Width / 2, r.Center.Y - tex.Height / 2), Color.White);
                return;
            }

            // Placeholder: a 2px-thick X built from 2x2 steps, brighter on hover.
            Color c = hover ? new Color(255, 232, 110) : RecipeBookLayout.PageColor;
            int inset = 3;
            int length = Math.Min(r.Width, r.Height) - 2 * inset - 1;
            for (int k = 0; k < length; k++)
            {
                BookDraw.Rect(sb, new Rectangle(r.X + inset + k, r.Y + inset + k, 2, 2), c);
                BookDraw.Rect(sb, new Rectangle(r.Right - inset - 2 - k, r.Y + inset + k, 2, 2), c);
            }
        }
    }
}