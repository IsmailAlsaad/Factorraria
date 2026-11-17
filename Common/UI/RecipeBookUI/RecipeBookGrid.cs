using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>
    /// R7: the vanilla inventory's favorite mode, reused. Holding Main.FavoriteKey (Left Alt by default) is what the inventory checks, and
    /// CursorOverrideID.FavoriteStar is the cursor it shows. Client-only, nothing to sync.
    /// </summary>
    public static class RecipeBookFavoriteMode
    {
        public static bool Held => Main.keyState.IsKeyDown(Main.FavoriteKey);

        public static void ApplyCursor() => Main.cursorOverride = CursorOverrideID.FavoriteStar;
    }

    /// <summary>
    /// R7: an entry in Grid mode: a small square showing only the product item. Everything else (selection, favorites, new marks, the
    /// jumps from the right page) is inherited from RecipeBookEntry, so RecipeBookState treats it exactly like a row.
    /// </summary>
    public class RecipeBookGridCell : RecipeBookEntry
    {
        public RecipeBookGridCell(string key, string machine, RecipeOutputGroup group, RecipeState state, bool favorite)
            : base(key, machine, group, state, favorite) { Size(); }

        public RecipeBookGridCell(string key, string machine, RecipeArtRegistry.Entry art, bool favorite)
            : base(key, machine, art, favorite) { Size(); }

        void Size()
        {
            Width.Set(RecipeBookLayout.GridCellSize, 0f);
            Height.Set(RecipeBookLayout.GridCellSize, 0f);
        }

        protected override bool StarClicked(UIMouseEvent evt) => false;

        static void Outline(SpriteBatch sb, Rectangle r, Color c, int t)
        {
            BookDraw.Rect(sb, new Rectangle(r.X, r.Y, r.Width, t), c);
            BookDraw.Rect(sb, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
            BookDraw.Rect(sb, new Rectangle(r.X, r.Y + t, t, r.Height - 2 * t), c);
            BookDraw.Rect(sb, new Rectangle(r.Right - t, r.Y + t, t, r.Height - 2 * t), c);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();
            bool hover = IsMouseHovering;
            bool isNew = !Selected && RecipeBookNew.IsNew(Key);

            if (Selected) BookDraw.Frame(sb, r, new Color(214, 178, 48) * 0.55f, new Color(150, 110, 30), 2);
            else
            {
                BookDraw.Rect(sb, r, RecipeBookLayout.GridCellFill);
                Outline(sb, r, RecipeBookLayout.GridCellBorder, 1);
                if (isNew || Favorite) BookDraw.Rect(sb, r, RecipeBookLayout.FavoriteRowColor);   // new and favorited squares share the plain favorite tint
                if (hover) BookDraw.Rect(sb, r, Color.White * 0.25f);
            }

            int box = Math.Max(4, r.Width - 2 * RecipeBookLayout.GridIconInset);
            if (Art == null) RecipeIcon.Draw(sb, Group.Key, r.Center.ToVector2(), box, 1f, IsFaded);
            else DrawArt(sb, r, box);

            if (Favorite) DrawSmallStar(sb, r);   // not clickable in the grid

            if (isNew)
            {
                var mark = RecipeBookArt.NewMark;
                float markHeight = mark != null ? mark.Value.Height : 14f;
                RecipeBookNew.DrawMark(sb, r.Right - RecipeBookNew.MarkWidth, r.Y + markHeight / 2f, RecipeBookLayout.GridMarkOffset);
            }

            if (hover)
            {
                Main.hoverItemName = ProductName;   // "???" while the recipe is faded
                if (RecipeBookFavoriteMode.Held) RecipeBookFavoriteMode.ApplyCursor();
            }
        }

        // An art entry has no product item: its own PNG is shown shrunk into the square (or its first letter until the PNG exists).
        void DrawArt(SpriteBatch sb, Rectangle r, int box)
        {
            Texture2D tex = Art.Texture;
            if (tex == null)
            {
                string letter = string.IsNullOrEmpty(ProductName) ? "?" : ProductName.Substring(0, 1);
                BookDraw.Text(sb, letter, new Vector2(r.Center.X - 4f, r.Center.Y - 10f), RecipeBookLayout.TextColor);
                return;
            }
            float fit = Math.Min(1f, box / (float)Math.Max(tex.Width, tex.Height));
            sb.Draw(tex, r.Center.ToVector2(), null, Color.White, 0f, new Vector2(tex.Width, tex.Height) / 2f, fit, SpriteEffects.None, 0f);
        }

        static void DrawSmallStar(SpriteBatch sb, Rectangle r)
        {
            Point pad = RecipeBookLayout.GridStarPad;
            var art = RecipeBookArt.Star(true, false);
            if (art != null)
                sb.Draw(art.Value, new Vector2(r.X + pad.X, r.Y + pad.Y), null, Color.White, 0f, Vector2.Zero, RecipeBookLayout.GridStarScale, SpriteEffects.None, 0f);
            else
                DrawStar(sb, new Rectangle(r.X + pad.X, r.Y + pad.Y, 12, 12), true, false);   // code-made placeholder
        }
    }

    /// <summary>
    /// R7: the Rows / Grid button at the top right of the left page. Shows the art of the mode a click switches TO
    /// (RecipeBookLayout.ModeIconShowsTarget = false shows the current mode instead); the _Highlight art is the hover state.
    /// The mode is saved per character in RecipeBookPlayer; the book rebuilds itself on its next Update.
    /// </summary>
    public class RecipeBookModeButton : UIElement
    {
        readonly RecipeBookState book;

        public RecipeBookModeButton(RecipeBookState book)
        {
            this.book = book;
            Rectangle r = RecipeBookLayout.ModeButton;
            Left.Set(r.X, 0f);
            Top.Set(r.Y, 0f);
            Width.Set(r.Width, 0f);
            Height.Set(r.Height, 0f);
        }

        // "The pages are blank" has no list and no search bar, so no button either.
        public override bool ContainsPoint(Vector2 point) => !book.IsEmpty && base.ContainsPoint(point);

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!book.IsEmpty && IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            if (book.IsEmpty) return;
            SoundEngine.PlaySound(SoundID.MenuTick);
            RecipeBookPlayer player = Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>();
            player.SetGridMode(!player.GridMode);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            if (book.IsEmpty) return;
            Rectangle r = GetDimensions().ToRectangle();
            bool hover = IsMouseHovering;
            bool grid = Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().GridMode;
            bool artGrid = RecipeBookLayout.ModeIconShowsTarget ? !grid : grid;

            var art = (hover ? RecipeBookArt.Mode(artGrid, true) : null) ?? RecipeBookArt.Mode(artGrid, false);
            if (art != null)
            {
                Texture2D tex = art.Value;
                sb.Draw(tex, new Vector2(r.Center.X - tex.Width / 2, r.Center.Y - tex.Height / 2), Color.White);
            }
            else DrawPlaceholder(sb, r, artGrid, hover);

            if (hover)
            {
                Main.hoverItemName = grid
                    ? Language.GetOrRegister("Mods.Factorraria.RecipeBook.ModeToRows", () => "Switch to list view").Value
                    : Language.GetOrRegister("Mods.Factorraria.RecipeBook.ModeToGrid", () => "Switch to grid view").Value;
            }
        }

        // Code-made glyph until the PNGs exist: three bars (Rows) or four squares (Grid).
        static void DrawPlaceholder(SpriteBatch sb, Rectangle r, bool gridGlyph, bool hover)
        {
            BookDraw.Frame(sb, r, RecipeBookLayout.SearchBackground, hover ? RecipeBookLayout.SearchFocusBorder : RecipeBookLayout.SearchIdleBorder, 2);
            Color ink = RecipeBookLayout.PageBorder;
            if (!gridGlyph)
            {
                for (int i = 0; i < 3; i++)
                    BookDraw.Rect(sb, new Rectangle(r.X + 5, r.Y + 6 + i * 6, r.Width - 10, 3), ink);
                return;
            }
            int s = Math.Max(2, (r.Width - 10 - 4) / 2);
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                    BookDraw.Rect(sb, new Rectangle(r.X + 5 + cx * (s + 4), r.Y + 5 + cy * (s + 4), s, s), ink);
        }
    }
}