using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace Factorraria.Common.UI
{
    static class BookDraw
    {
        public static void Rect(SpriteBatch sb, Rectangle r, Color c) => sb.Draw(TextureAssets.MagicPixel.Value, r, c);

        public static void Frame(SpriteBatch sb, Rectangle r, Color fill, Color border, int thickness = 2)
        {
            Rect(sb, r, border);
            Rect(sb, new Rectangle(r.X + thickness, r.Y + thickness, r.Width - 2 * thickness, r.Height - 2 * thickness), fill);
        }

        /// <summary>Shortens text with "..." so it fits maxWidth at the given scale.</summary>
        public static string Fit(string text, float maxWidth, float scale, out bool truncated)
        {
            var font = FontAssets.MouseText.Value;
            truncated = false;
            if (font.MeasureString(text).X * scale <= maxWidth) return text;
            truncated = true;
            while (text.Length > 1 && font.MeasureString(text + "...").X * scale > maxWidth)
                text = text.Substring(0, text.Length - 1);
            return text + "...";
        }

        public static void Text(SpriteBatch sb, string text, Vector2 pos, Color color, float scale = 1f) =>
            sb.DrawString(FontAssets.MouseText.Value, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    /// <summary>The inventory button that toggles the recipe book. Placeholder art unless the PNGs exist.</summary>
    public class RecipeBookButton : UIElement
    {
        public Action OnPressed;
        public bool MouseInside { get; private set; }

        public RecipeBookButton()
        {
            Left.Set(RecipeBookLayout.ButtonX, 0f);
            Top.Set(RecipeBookLayout.ButtonY, 0f);
            Width.Set(RecipeBookLayout.ButtonSize, 0f);
            Height.Set(RecipeBookLayout.ButtonSize, 0f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            MouseInside = ContainsPoint(Main.MouseScreen);
            if (MouseInside) Main.LocalPlayer.mouseInterface = true;   // keeps item use / tile placement from firing under the button
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            OnPressed?.Invoke();
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();
            bool hover = MouseInside;

            var art = hover && RecipeBookArt.ButtonHover != null ? RecipeBookArt.ButtonHover : RecipeBookArt.Button;
            if (art != null)
                sb.Draw(art.Value, r, Color.White);
            else
            {
                // Placeholder: a little closed book.
                BookDraw.Frame(sb, r, hover ? new Color(140, 96, 56) : RecipeBookLayout.CoverColor, hover ? new Color(255, 220, 60) : RecipeBookLayout.PageBorder, 2);
                BookDraw.Rect(sb, new Rectangle(r.X + 7, r.Y + 6, 3, r.Height - 12), new Color(70, 44, 22));          // spine
                BookDraw.Rect(sb, new Rectangle(r.X + 13, r.Y + 8, r.Width - 21, 3), RecipeBookLayout.PageColor);    // title lines
                BookDraw.Rect(sb, new Rectangle(r.X + 13, r.Y + 14, r.Width - 21, 3), RecipeBookLayout.PageColor);
                BookDraw.Rect(sb, new Rectangle(r.X + 13, r.Y + 20, r.Width - 27, 3), RecipeBookLayout.PageColor);
            }

            if (hover) Main.hoverItemName = RecipeBookLayout.Text("ButtonTooltip");
        }
    }

    /// <summary>Machine name divider in the list.</summary>
    public class RecipeBookHeaderRow : UIElement
    {
        readonly string text;

        public RecipeBookHeaderRow(string machineName)
        {
            text = RecipeBookLayout.MachineName(machineName);
            Width.Set(0f, 1f);
            Height.Set(RecipeBookLayout.HeaderRowHeight, 0f);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();
            BookDraw.Text(sb, text, new Vector2(r.X + 4, r.Y + 4), RecipeBookLayout.PageBorder, 1.05f);
            BookDraw.Rect(sb, new Rectangle(r.X + 2, r.Bottom - 4, r.Width - 4, 2), RecipeBookLayout.PageBorder * 0.6f);
        }
    }

    /// <summary>One recipe in the list: product icon (black-faded until crafted) and name.</summary>
    public class RecipeBookEntry : UIElement
    {
        public readonly string Key;
        public readonly string Machine;
        public readonly RecipeOutputGroup Group;
        public readonly RecipeState State;
        public readonly string ProductName;
        public bool Selected;
        public Action<string> OnChosen;

        public RecipeBookEntry(string key, string machine, RecipeOutputGroup group, RecipeState state)
        {
            Key = key;
            Machine = machine;
            Group = group;
            State = state;
            ProductName = group.Key.DisplayName;
            Width.Set(0f, 1f);
            Height.Set(RecipeBookLayout.EntryRowHeight, 0f);
        }

        public bool IsFaded => State != RecipeState.Crafted;

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            SoundEngine.PlaySound(SoundID.MenuTick);
            OnChosen?.Invoke(Key);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();
            bool hover = IsMouseHovering;

            if (Selected) BookDraw.Frame(sb, r, new Color(214, 178, 48) * 0.55f, new Color(150, 110, 30), 2);
            else if (hover) BookDraw.Rect(sb, r, Color.White * 0.25f);

            int box = RecipeBookLayout.EntryIconBox;
            RecipeIcon.Draw(sb, Group.Key, new Vector2(r.X + 6 + box / 2f, r.Center.Y), box, 1f, IsFaded);

            float textX = r.X + 6 + box + 8;
            float maxWidth = r.Right - textX - 4;
            string shown = BookDraw.Fit(ProductName, maxWidth, 0.95f, out bool truncated);
            Vector2 size = FontAssets.MouseText.Value.MeasureString(shown) * 0.95f;
            BookDraw.Text(sb, shown, new Vector2(textX, r.Center.Y - size.Y / 2f + 2f),
                IsFaded ? RecipeBookLayout.TextFadedColor : RecipeBookLayout.TextColor, 0.95f);

            if (hover && truncated) Main.hoverItemName = ProductName;
        }
    }

    /// <summary>Right page. Phase 3 shows only a placeholder; Phase 4 replaces the body with the real recipe layout.</summary>
    public class RecipeBookDetail : UIElement
    {
        readonly RecipeBookState book;

        public RecipeBookDetail(RecipeBookState book)
        {
            this.book = book;
            Rectangle a = RecipeBookLayout.DetailArea;
            Left.Set(a.X, 0f);
            Top.Set(a.Y, 0f);
            Width.Set(a.Width, 0f);
            Height.Set(a.Height, 0f);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();
            var font = FontAssets.MouseText.Value;

            if (book.SelectedGroup == null)
            {
                string msg = book.IsEmpty ? RecipeBookLayout.Text("Empty") : book.NoResults ? RecipeBookLayout.Text("NoResults") : RecipeBookLayout.Text("NothingSelected");
                DrawWrapped(sb, msg, new Vector2(r.X + 8, r.Center.Y - 30), r.Width - 16, RecipeBookLayout.TextFadedColor, 1f);
                return;
            }

            bool faded = book.SelectedState != RecipeState.Crafted;
            RecipeIcon.Draw(sb, book.SelectedGroup.Key, new Vector2(r.X + 30, r.Y + 30), 44f, 1f, faded);

            string name = BookDraw.Fit(book.SelectedGroup.Key.DisplayName, r.Width - 70, 1.15f, out _);
            BookDraw.Text(sb, name, new Vector2(r.X + 64, r.Y + 6), RecipeBookLayout.TextColor, 1.15f);
            BookDraw.Text(sb, RecipeBookLayout.MachineName(book.SelectedMachine), new Vector2(r.X + 64, r.Y + 34), RecipeBookLayout.PageBorder, 0.9f);

            BookDraw.Rect(sb, new Rectangle(r.X, r.Y + 66, r.Width, 2), RecipeBookLayout.PageBorder * 0.6f);

            string status = RecipeBookLayout.Text(faded ? "StatusParchment" : "StatusCrafted");
            DrawWrapped(sb, status, new Vector2(r.X + 4, r.Y + 78), r.Width - 8, RecipeBookLayout.TextColor, 0.95f);
            // TODO (Phase 4): replace with inputs -> outputs for every recipe in the group.
            DrawWrapped(sb, RecipeBookLayout.Text("DetailsComing"), new Vector2(r.X + 4, r.Y + 128), r.Width - 8, RecipeBookLayout.TextFadedColor, 0.95f);
        }

        static void DrawWrapped(SpriteBatch sb, string text, Vector2 pos, float width, Color color, float scale)
        {
            var font = FontAssets.MouseText.Value;
            string[] lines = Utils.WordwrapString(text, font, (int)(width / scale), 8, out int count);
            for (int i = 0; i < count; i++)
                if (lines[i] != null) BookDraw.Text(sb, lines[i], pos + new Vector2(0, i * 24f * scale), color, scale);
        }
    }
}