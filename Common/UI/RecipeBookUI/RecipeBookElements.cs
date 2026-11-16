using Factorraria.Common.Knowledge;
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

namespace Factorraria.Common.UI.RecipeBookUI
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

    /// <summary>
    /// Machine name divider in the list, with a small triangle (right = collapsed, down = expanded). Clicking the row
    /// toggles that machine's recipe list. onToggle == null means the row is not toggleable (a search is active, so
    /// every match is shown); the triangle then draws expanded and clicks do nothing.
    /// </summary>
    public class RecipeBookHeaderRow : UIElement
    {
        string text;
        int[] items;                   // sprites shown left of the name, cycled (only the ones the world has held); none for By Hand / Notes
        string[] inherited;            // stations listed in the hover tip (only once the tab's own machine is discovered)
        int builtVersion = -1, builtSeenVersion = -1;

        // Title, sprites and tip depend on what has been discovered, which can change while the book is open (a held item bumps SeenVersion).
        void Refresh()
        {
            builtVersion = RecipeKnowledgeSystem.Version;
            builtSeenVersion = RecipeKnowledgeSystem.SeenVersion;

            string[] shown = RecipeTabDiscovery.HeaderStations(Machine);
            var names = new string[shown.Length];
            for (int i = 0; i < shown.Length; i++) names[i] = RecipeBookLayout.MachineName(shown[i]);
            text = string.Join(" / ", names);
            items = RecipeTabDiscovery.ItemsOf(shown);
            inherited = RecipeTabDiscovery.IsOwnDiscovered(Machine) ? RecipeTabDiscovery.InheritedNames(Machine) : new string[0];
        }
        public readonly string Machine;
        /// <summary>Seconds each sprite stays up when a tab has several (Iron Anvil, Lead Anvil ...). Not const, so hot reload can tweak it.</summary>
        public static float IconSeconds = 1f;
        public readonly bool Collapsed;
        /// <summary>Set by RecipeBookState while the header is stuck to the top margin: it then fills its own background so rows scrolling under it do not show through.</summary>
        public bool Pinned;
        readonly Action<string> onToggle;
        readonly System.Collections.Generic.IList<string> childKeys;   // keys of the entries listed under this tab (the tab shows "!" while any of them is new, open or folded)

        public RecipeBookHeaderRow(string machine, bool collapsed, Action<string> onToggle, System.Collections.Generic.IList<string> childKeys = null)
        {
            Machine = machine;
            Collapsed = collapsed;
            this.onToggle = onToggle;
            this.childKeys = childKeys;
            Refresh();
            Width.Set(0f, 1f);
            Height.Set(RecipeBookLayout.HeaderRowHeight, 0f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
        }

        // The tab is a few pixels taller on top than what it draws: the hit area (and the band behind the title) reach up RecipeBookLayout.HeaderTopExtra px
        // over the bottom of the row above. Nothing else moves. A header stuck to the top margin has no room above it, so it gets none.
        int RoomAbove()
        {
            float listTop = Parent != null ? Parent.GetDimensions().Y : GetDimensions().Y;
            return (int)Math.Max(0f, Math.Min(RecipeBookLayout.HeaderTopExtra, GetDimensions().Y - listTop));
        }

        public override bool ContainsPoint(Vector2 point)
        {
            CalculatedStyle d = GetDimensions();
            return point.X > d.X && point.X < d.X + d.Width && point.Y > d.Y - RoomAbove() && point.Y < d.Y + d.Height;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().MarkViewed(RecipeBookNew.TabKey(Machine));
            if (onToggle == null) return;
            SoundEngine.PlaySound(SoundID.MenuTick);
            onToggle(Machine);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();
            if (Pinned) BookDraw.Rect(sb, r, RecipeBookArt.HeaderBackground());
            bool toggleable = onToggle != null;
            bool tabNew = RecipeBookNew.IsTabNew(Machine);
            int up = Math.Min(RecipeBookLayout.HeaderBandTopExtra, RoomAbove());
            var band = new Rectangle(r.X + 2, r.Y + 2 - up, r.Width - 4, r.Height - 6 + up);
            BookDraw.Rect(sb, band, RecipeBookLayout.HeaderTint);                       // machine tabs sit a little darker and browner than the page
            if (tabNew) BookDraw.Rect(sb, band, RecipeBookNew.RowColor());
            if (toggleable && IsMouseHovering) BookDraw.Rect(sb, band, RecipeBookLayout.HeaderHoverColor);

            // Triangle built from 1px strips (no art needed). Right-pointing when collapsed, down-pointing when expanded.
            Color c = RecipeBookLayout.PageBorder;
            int cx = r.X + 10;
            int cy = r.Y + 13;
            for (int i = 0; i < 5; i++)
            {
                if (Collapsed && toggleable) BookDraw.Rect(sb, new Rectangle(cx - 2 + i, cy - 4 + i, 1, 9 - 2 * i), c);
                else BookDraw.Rect(sb, new Rectangle(cx - 4 + i, cy - 2 + i, 9 - 2 * i, 1), c);
            }

            if (builtVersion != RecipeKnowledgeSystem.Version || builtSeenVersion != RecipeKnowledgeSystem.SeenVersion) Refresh();
            int textX = r.X + 20;
            if (items.Length > 0)
            {
                int type = items[(int)(Main.GlobalTimeWrappedHourly / Math.Max(0.1f, IconSeconds)) % items.Length];
                RecipeIcon.Draw(sb, RecipeOutputKey.ForItem(type), new Vector2(r.X + 31, r.Y + 13), 20f);
                textX = r.X + 46;
            }
            bool flagged = tabNew || RecipeBookNew.AnyNew(childKeys);   // stays until nothing of this tab is new, open or folded
            string shownText = BookDraw.Fit(text, r.Right - 6 - textX - (flagged ? RecipeBookNew.MarkWidth + RecipeBookNew.MarkGap : 0), 1.05f, out bool truncated);
            BookDraw.Text(sb, shownText, new Vector2(textX, r.Y + 4), RecipeBookLayout.PageBorder, 1.05f);
            if (flagged) RecipeBookNew.DrawMark(sb, textX + FontAssets.MouseText.Value.MeasureString(shownText).X * 1.05f + RecipeBookNew.MarkGap, r.Y + 13, RecipeBookNew.TabMarkOffset);

            // Hover over the title (not the fold arrow): "Inherits from [icon] Anvils and ...", or the full name when it was cut short.
            Vector2 mouse = UserInterface.ActiveInstance.MousePosition;
            if (IsMouseHovering && mouse.X >= r.X + 18 && mouse.Y < r.Bottom - 4)
            {
                if (inherited.Length > 0) RecipeBookTabTip.Request(inherited);
                else if (truncated) Main.hoverItemName = text;
            }
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
        public readonly bool Favorite;
        public readonly RecipeArtRegistry.Entry Art;     // set for art entries (Group is null then)
        public bool Selected;
        public Action<string> OnChosen;

        public RecipeBookEntry(string key, string machine, RecipeOutputGroup group, RecipeState state, bool favorite)
        {
            Key = key;
            Favorite = favorite;
            Machine = machine;
            Group = group;
            State = state;
            ProductName = state == RecipeState.Crafted ? group.Key.DisplayName : RecipeBookLayout.UnknownName;
            Width.Set(0f, 1f);
            Height.Set(RecipeBookLayout.EntryRowHeight, 0f);
        }

        /// <summary>Art entry: no product icon, the PNG's file name is the label. Unlocked art is never faded (there is nothing to hide).</summary>
        public RecipeBookEntry(string key, string machine, RecipeArtRegistry.Entry art, bool favorite)
        {
            Key = key;
            Favorite = favorite;
            Machine = machine;
            Art = art;
            State = RecipeState.FadedParchment;
            ProductName = art.Name;
            Width.Set(0f, 1f);
            Height.Set(RecipeBookLayout.EntryRowHeight, 0f);
        }

        public bool IsFaded => Art == null && State != RecipeState.Crafted;

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            SoundEngine.PlaySound(SoundID.MenuTick);
            if (RecipeBookLayout.StarRect(GetDimensions().ToRectangle()).Contains(evt.MousePosition.ToPoint()))
            {
                // Toggle only; the book re-sorts on its next Update (never mid-click). The row is not selected.
                Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().ToggleFavorite(Key);
                return;
            }
            OnChosen?.Invoke(Key);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();
            bool hover = IsMouseHovering;
            bool isNew = !Selected && RecipeBookNew.IsNew(Key);

            if (Selected) BookDraw.Frame(sb, r, new Color(214, 178, 48) * 0.55f, new Color(150, 110, 30), 2);
            else
            {
                if (Favorite) BookDraw.Rect(sb, r, RecipeBookLayout.FavoriteRowColor);   // faint orange
                if (isNew) BookDraw.Rect(sb, r, RecipeBookNew.RowColor());                // pulsing gold until the recipe is viewed
                if (hover) BookDraw.Rect(sb, r, Color.White * 0.25f);                     // whitish on top (less orange when favorited)
            }

            int box = RecipeBookLayout.EntryIconBox;
            if (Art == null) RecipeIcon.Draw(sb, Group.Key, new Vector2(r.X + 6 + box / 2f, r.Center.Y), box, 1f, IsFaded);

            float textX = Art == null ? r.X + 6 + box + 8 : r.X + 10;
            float maxWidth = r.Right - textX - 4 - RecipeBookLayout.StarHitSize - RecipeBookLayout.StarRightPad - (isNew ? RecipeBookNew.MarkWidth + RecipeBookNew.MarkGap : 0);
            string shown = BookDraw.Fit(ProductName, maxWidth, 0.95f, out bool truncated);
            Vector2 size = FontAssets.MouseText.Value.MeasureString(shown) * 0.95f;
            BookDraw.Text(sb, shown, new Vector2(textX, r.Center.Y - size.Y / 2f + 2f),
                IsFaded ? RecipeBookLayout.TextFadedColor : RecipeBookLayout.TextColor, 0.95f);
            if (isNew) RecipeBookNew.DrawMark(sb, textX + size.X + RecipeBookNew.MarkGap, r.Center.Y, RecipeBookNew.EntryMarkOffset);

            if (hover && truncated) Main.hoverItemName = ProductName;

            Rectangle star = RecipeBookLayout.StarRect(r);
            bool starHover = hover && star.Contains(Main.MouseScreen.ToPoint());
            if (Favorite || hover) DrawStar(sb, star, Favorite, starHover);   // the off star only appears while the row is hovered
            if (starHover) Main.hoverItemName = RecipeBookLayout.Text(Favorite ? "FavoriteRemove" : "FavoriteAdd");
        }

        // Code-made placeholder star, used until the RecipeBookStar_* PNGs exist.
        static readonly string[] StarPattern =
        {
            "-----#-----",
            "-----#-----",
            "----###----",
            "###########",
            "-#########-",
            "--#######--",
            "--#######--",
            "-####-####-",
            "-###---###-",
            "-#-------#-",
        };

        static void DrawStar(SpriteBatch sb, Rectangle box, bool on, bool hover)
        {
            var art = RecipeBookArt.Star(on, hover);
            if (art != null)
            {
                Texture2D tex = art.Value;
                sb.Draw(tex, new Vector2(box.Center.X - tex.Width / 2, box.Center.Y - tex.Height / 2), Color.White);
                return;
            }

            Color c = on
                ? (hover ? new Color(255, 232, 110) : new Color(236, 190, 40))
                : (hover ? RecipeBookLayout.PageBorder * 0.8f : RecipeBookLayout.PageBorder * 0.4f);
            int w = StarPattern[0].Length;
            int h = StarPattern.Length;
            int ox = box.Center.X - w / 2;
            int oy = box.Center.Y - h / 2;
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    if (StarPattern[y][x] != '#') { x++; continue; }
                    int start = x;
                    while (x < w && StarPattern[y][x] == '#') x++;
                    BookDraw.Rect(sb, new Rectangle(ox + start, oy + y, x - start, 1), c);
                }
            }
        }
    }


}