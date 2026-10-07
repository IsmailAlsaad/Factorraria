using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;

namespace Factorraria.Common.UI
{
    /// <summary>
    /// The recipe book's title bar. Hold the left mouse button on it to move the whole book; right-click to put it back
    /// in the center. Covers RecipeBookLayout.DragBar only, so it never overlaps the pages, search bar or scrollbar.
    /// Uses the UI-scaled mouse position (like ZoomScrollbar) so dragging is correct at any UI scale.
    /// </summary>
    public class RecipeBookDragHandle : UIElement
    {
        readonly RecipeBookState book;
        bool dragging;
        Vector2 grab;     // mouse position minus the panel offset at the moment the drag started

        public RecipeBookDragHandle(RecipeBookState book)
        {
            this.book = book;
            Rectangle b = RecipeBookLayout.DragBar;
            Left.Set(b.X, 0f);
            Top.Set(b.Y, 0f);
            Width.Set(b.Width, 0f);
            Height.Set(b.Height, 0f);
        }

        public override void LeftMouseDown(UIMouseEvent evt)
        {
            base.LeftMouseDown(evt);
            dragging = true;
            grab = evt.MousePosition - book.PanelOffset;
        }

        public override void RightClick(UIMouseEvent evt)
        {
            base.RightClick(evt);
            book.ResetPanelOffset();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (dragging)
            {
                if (!Main.mouseLeft) dragging = false;
                else book.SetPanelOffset(UserInterface.ActiveInstance.MousePosition - grab);
            }

            if (dragging || IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            if (!dragging && !IsMouseHovering) return;

            // Faint highlight so the strip reads as grabbable.
            BookDraw.Rect(sb, GetDimensions().ToRectangle(), Color.White * (dragging ? 0.18f : 0.10f));
        }
    }
}