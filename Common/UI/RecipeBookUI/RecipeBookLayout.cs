using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Text;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>
    /// Single source of truth for the recipe book's geometry. All rectangles are relative to the book panel's top-left.
    /// Everything is in UI-scaled pixels, so it follows the player's UI scale automatically.
    /// Change numbers here only; elements never hardcode positions.
    /// </summary>
    public static class RecipeBookLayout
    {
        // Inventory button (absolute UI position).
        public const int ButtonX = 1516;
        public const int ButtonY = 726;
        public const int ButtonSize = 36;

        // ButtonX/ButtonY above are only the button's initial position before the first inventory draw.
        // At runtime RecipeBookButtonState places it from tModLoader's AccessorySlotLoader.DefenseIconPosition (see its DefaultOffset),
        // so ButtonOffsetX/Y and ReferenceDefenseX/Y below are no longer used.
        public const int ReferenceDefenseX = 1920 - 64 - 28;   // 1828: AccessorySlotLoader uses Main.screenWidth - 64 - 28
        // Defense icon Y at the reference setup. 0 = not calibrated yet, and the button then keeps the fixed ButtonX/ButtonY.
        // To calibrate: reference setup, inventory open, run "/recipes defpos" and paste the Y it prints here.
        public static readonly int ReferenceDefenseY = 0;
        public const int ButtonOffsetX = ButtonX - ReferenceDefenseX;
        public static int ButtonOffsetY => ButtonY - ReferenceDefenseY;

        // Book panel
        public const int PanelWidth = 780;
        public const int PanelHeight = 470;

        /// <summary>Tweak: size of the background texture relative to the panel (1 = exactly the panel size). Scaled around the panel's center.</summary>
        public static float BackgroundScale = 1f;
        public const int Margin = 22;
        public const int Gutter = 14;          // half the gap between the two pages (the spine)
        public const int TitleHeight = 40;

        /// <summary>Shown instead of a product's name until the product has been crafted (or its ingredient is held, in the machine browser).</summary>
        public const string UnknownName = "???";
        public const int ScrollbarWidth = 24;
        public static int ScrollbarGrow = 0;   // extra thickness of the scrollbar in px, split between the left and right side (even numbers stay symmetric)

        // List rows
        public const int HeaderRowHeight = 28;
        public const int EntryRowHeight = 36;
        public const int EntryIconBox = 28;

        // Favorite star: native-size art is centered in a StarHitSize square, StarRightPad px from the row's right border.
        // Keep the star PNGs at or below StarHitSize in both directions.
        public const int StarHitSize = 24;
        public const int StarRightPad = 6;

        /// <summary>Hit box of a recipe row's star. `row` is the row's screen rectangle.</summary>
        public static Rectangle StarRect(Rectangle row) =>
            new Rectangle(row.Right - StarRightPad - StarHitSize, row.Center.Y - StarHitSize / 2, StarHitSize, StarHitSize);

        // ---- R7: Rows / Grid mode of the left page (tunables are not const, so hot reload can change them) ----
        public static int GridCellSize = 36;           // side of one grid square, px
        public static int GridGap = 4;                 // space between squares, px
        public static int GridIconInset = 4;           // padding between a square's border and its item icon, px
        public static float GridStarScale = 0.6f;      // favorite star at the top left of a square (1 = native art size)
        public static Point GridStarPad = new Point(2, 2);               // star offset from the square's top left corner
        public static Vector2 GridMarkOffset = new Vector2(-1f, 1f);     // the "!" of a new recipe: shift from the square's top right corner (+x right, +y down)
        public static Color GridCellFill = new Color(98, 68, 38) * 0.10f;
        public static Color GridCellBorder = new Color(98, 68, 38) * 0.35f;
        public static int ModeButtonSize = 24;         // the Rows / Grid button's square, px; its art is drawn at native size, centered
        public static int ModeButtonGap = 4;           // gap between the search bar and the button
        public static bool ModeIconShowsTarget = true; // true: the button shows the art of the mode a click switches TO; false: the art of the current mode

        /// <summary>How many squares fit in one grid row of the list.</summary>
        public static int GridColumns => System.Math.Max(1, (ListArea.Width + GridGap) / (GridCellSize + GridGap));

        public static int GridEdgeInset = 2;   // px kept free at the left and right edge of the list so a square's border is not clipped

        public static float GridLeft(int columns) => columns > 1 ? GridEdgeInset : (ListArea.Width - GridCellSize) / 2f;

        public static float GridGapFor(int columns) => columns > 1 ? (ListArea.Width - 2 * GridEdgeInset - columns * GridCellSize) / (float)(columns - 1) : 0f;

        /// <summary>The Rows / Grid button: right of the search bar, its right edge on the scrollbar's right edge.</summary>
        public static Rectangle ModeButton
        {
            get
            {
                Rectangle p = LeftPage;
                return new Rectangle(p.Right - 10 - ModeButtonSize, p.Y + 8 + (24 - ModeButtonSize) / 2, ModeButtonSize, ModeButtonSize);
            }
        }

        public static Rectangle LeftPage => new Rectangle(Margin, Margin, PanelWidth / 2 - Margin - Gutter, PanelHeight - 2 * Margin);
        public static Rectangle RightPage => new Rectangle(PanelWidth / 2 + Gutter, Margin, PanelWidth / 2 - Margin - Gutter, PanelHeight - 2 * Margin);

        public static Rectangle ListArea
        {
            get
            {
                Rectangle p = LeftPage;
                return new Rectangle(p.X + 10, p.Y + TitleHeight, p.Width - 20 - ScrollbarWidth - 4, p.Height - TitleHeight - 10);
            }
        }

        public static Rectangle ScrollbarArea => new Rectangle(ListArea.Right + 4 - ScrollbarGrow / 2, ListArea.Y, ScrollbarWidth + ScrollbarGrow, ListArea.Height);
        /// <summary>
        /// The book's "title bar": the cover strip above both pages (nothing else lives there). Drag it to move the book.
        /// Relative to the panel's top-left like every other rectangle here.
        /// </summary>
        public static Rectangle DragBar => new Rectangle(0, 0, PanelWidth, Margin);

        /// <summary>When dragging, at least this many pixels of the drag bar stay on screen horizontally.</summary>
        public const int DragKeepVisible = 60;

        // Close "X": a square in the top border (the drag bar strip), CloseButtonPad px from the panel's right edge, centered vertically in the strip.
        public static int CloseButtonSize = 14;
        public static int CloseButtonPad = 6;
        public static Rectangle CloseButton => new Rectangle(PanelWidth - CloseButtonPad - CloseButtonSize, (Margin - CloseButtonSize) / 2, CloseButtonSize, CloseButtonSize);

        /// <summary>Search bar: sits in the title strip (top TitleHeight px) of the left page, above ListArea.</summary>
        public static Rectangle SearchArea
        {
            get
            {
                Rectangle p = LeftPage;
                return new Rectangle(p.X + 10, p.Y + 8, p.Width - 20 - ModeButtonSize - ModeButtonGap, 24);
            }
        }

        public static Rectangle DetailArea
        {
            get
            {
                Rectangle p = RightPage;
                return new Rectangle(p.X + 12, p.Y + 12, p.Width - 24, p.Height - 24);
            }
        }

        // Detail page geometry (all relative to DetailArea's top-left unless noted)
        public const int DetailHeaderHeight = 78;      // product icon, name, machine, divider
        public const int DetailCell = 42;              // one ingredient cell (square)
        public const int DetailCellGap = 4;
        public const int DetailPagerHeight = 26;       // prev / next row at the bottom of DetailArea

        /// <summary>Where the recipe body (inputs, arrow, outputs, time) is drawn. Absolute inside the panel.</summary>
        public static Rectangle DetailRecipeArea
        {
            get
            {
                Rectangle a = DetailArea;
                return new Rectangle(a.X, a.Y + DetailHeaderHeight, a.Width, a.Height - DetailHeaderHeight - DetailPagerHeight);
            }
        }

        // ---- text ----
        const string Prefix = "Mods.Factorraria.RecipeBook.";

        public static string Text(string key) => Language.GetTextValue(Prefix + key);

        /// <summary>Localized machine name for a catalog machine name ("IceMachine"), falling back to a spaced version.</summary>
        public static string MachineName(string machine)
        {
            string key = Prefix + "Machines." + machine;
            if (Language.Exists(key)) return Language.GetTextValue(key);

            // A vanilla station tab ("VanillaFurnaces", see VanillaStationBooks): the spaced tile name, no suffix. A station that has a
            // machine twin never gets its own label: the book merges it into the machine's tab (RecipeCatalog.TabOf).
            bool vanilla = machine.Length > 7 && machine.StartsWith("Vanilla", System.StringComparison.Ordinal);
            string spaced = vanilla ? machine.Substring(7) : machine;

            var sb = new StringBuilder();
            for (int i = 0; i < spaced.Length; i++)
            {
                if (i > 0 && char.IsUpper(spaced[i])) sb.Append(' ');
                sb.Append(spaced[i]);
            }
            return sb.ToString();
        }

        // ---- colours (placeholder art) ----
        public static readonly Color PageColor = new Color(224, 203, 156);
        public static readonly Color PageBorder = new Color(98, 68, 38);
        public static readonly Color CoverColor = new Color(104, 70, 40);
        public static readonly Color TextColor = new Color(60, 38, 18);
        public static readonly Color TextFadedColor = new Color(120, 98, 70);

        /// <summary>Background of a favorited recipe row: the selection orange, but much fainter.</summary>
        public static readonly Color FavoriteRowColor = new Color(214, 178, 48) * 0.25f;

        // Machine tab rows (the headers on the left page): a slightly darker, browner band over the page. Hover is a warm light instead of plain white.
        public static Color HeaderTint = new Color(120, 78, 40) * 0.22f;
        public static Color HeaderHoverColor = new Color(255, 230, 180) * 0.32f;
        public static int HeaderTopExtra = 4;       // px the machine tab's hit area reaches above its row (nothing else moves)
        public static int HeaderBandTopExtra = 4;   // px the tinted band behind the tab reaches above its row (visual only, 0 = old look); never more than HeaderTopExtra

        // ---- search bar + scrollbar palette (tweak the numbers here) ----
        public static readonly Color SearchBackground = new Color(200, 176, 128);
        public static readonly Color SearchIdleBorder = PageBorder;
        public static readonly Color SearchFocusBorder = new Color(214, 178, 48);
        public static readonly Color ScrollTrack = Color.Lerp(PageColor, PageBorder, 0.35f);
        public static readonly Color ScrollTrackOutline = Color.Lerp(PageColor, PageBorder, 0.7f);
        public static readonly Color ScrollThumb = CoverColor;
        public static readonly Color ScrollThumbHover = new Color(140, 96, 56);
        public static readonly Color ScrollThumbDrag = new Color(170, 120, 70);

        /// <summary>Parchment look for the book's RecipeSearchBar (the machine browser passes no theme and stays blue).</summary>
        public static readonly CustomUIElements.SearchBarTheme SearchTheme = new CustomUIElements.SearchBarTheme
        {
            Background = SearchBackground,
            IdleBorder = SearchIdleBorder,
            FocusBorder = SearchFocusBorder,
            Text = TextColor,
            Hint = TextFadedColor,
            Caret = TextColor,
        };
    }

    /// <summary>
    /// Optional real art. Drop PNGs at these paths (no extension) and they are used automatically;
    /// until then the UI draws code-made placeholders. Layout does not depend on the art.
    /// </summary>
    public static class RecipeBookArt
    {
        public const string ButtonPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookButton";
        public const string ButtonHoverPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookButton_Hover";
        public const string BackgroundPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookBackground";
        public const string StarOffPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookStar_Off";
        public const string StarOffHighlightedPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookStar_Off_Highlighted";
        public const string StarOnPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookStar_On";
        public const string StarOnHighlightedPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookStar_On_Highlighted";
        public static string CloseButtonPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookClose";
        public static string CloseButtonHoverPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookClose_Hover";
        public static string NewMarkPath = "Factorraria/Common/UI/CustomUIElements/RecipeBookNew";
        public static string DiscoveredPath = "Factorraria/Common/UI/CustomUIElements/RecipebookIcon_Discovered";
        public static string ModeRowsPath = "Factorraria/Common/UI/CustomUIElements/SwitchRecipeBookMode_Rows";                 // R7: the Rows / Grid button
        public static string ModeGridPath = "Factorraria/Common/UI/CustomUIElements/SwitchRecipeBookMode_Grid";
        public static string ModeRowsHighlightPath = "Factorraria/Common/UI/CustomUIElements/SwitchRecipeBookMode_Rows_Highlight";
        public static string ModeGridHighlightPath = "Factorraria/Common/UI/CustomUIElements/SwitchRecipeBookMode_Grid_Highlight";   // R6: white outline drawn over the book button while something is new

        static Asset<Texture2D> button, buttonHover, background;
        static Asset<Texture2D> closeButton, closeButtonHover;
        static Asset<Texture2D> newMark, discovered;
        static Asset<Texture2D> modeRows, modeGrid, modeRowsHighlight, modeGridHighlight;
        static Asset<Texture2D> starOff, starOffHighlighted, starOn, starOnHighlighted;
        static bool loaded;

        public static Asset<Texture2D> Button { get { Load(); return button; } }
        public static Asset<Texture2D> ButtonHover { get { Load(); return buttonHover; } }
        public static Asset<Texture2D> Background { get { Load(); return background; } }
        /// <summary>Close X art, or null until the PNG exists (the button then draws a placeholder X). Drawn at native size, centered.</summary>
        public static Asset<Texture2D> CloseButton { get { Load(); return closeButton; } }
        public static Asset<Texture2D> CloseButtonHover { get { Load(); return closeButtonHover; } }
        /// <summary>The "!" shown after the name of a new recipe or tab, or null until the PNG exists (a code-drawn placeholder is used). Drawn at native size.</summary>
        public static Asset<Texture2D> NewMark { get { Load(); return newMark; } }
        /// <summary>The white outline PNG of the book button (R6), or null until it exists (a code-drawn border is used then). Tinted with a cycling hue.</summary>
        public static Asset<Texture2D> Discovered { get { Load(); return discovered; } }

        /// <summary>R7: art of the Rows / Grid button for a mode (grid = the Grid art), or null if that PNG does not exist yet (the button then draws a code-made glyph). The highlighted one is the hover state.</summary>
        public static Asset<Texture2D> Mode(bool grid, bool highlighted)
        {
            Load();
            return grid ? (highlighted ? modeGridHighlight : modeGrid) : (highlighted ? modeRowsHighlight : modeRows);
        }

        /// <summary>Favorite star art for a state, or null if that PNG does not exist yet (the entry then draws a placeholder).</summary>
        public static Asset<Texture2D> Star(bool on, bool highlighted)
        {
            Load();
            return on ? (highlighted ? starOnHighlighted : starOn) : (highlighted ? starOffHighlighted : starOff);
        }

        static Color? headerBg;

        /// <summary>
        /// Opaque fill for a pinned machine header: one pixel sampled from the page art just inside the list area, or PageColor
        /// until the art exists (or if sampling fails). Cached; Clear() resets it.
        /// </summary>
        public static Color HeaderBackground()
        {
            if (headerBg.HasValue) return headerBg.Value;
            Color result = RecipeBookLayout.PageColor;
            Asset<Texture2D> bg = Background;
            if (bg != null)
            {
                try
                {
                    Texture2D tex = bg.Value;
                    float k = RecipeBookLayout.BackgroundScale;
                    float w = RecipeBookLayout.PanelWidth * k, h = RecipeBookLayout.PanelHeight * k;
                    float px = RecipeBookLayout.ListArea.X + 4 - (RecipeBookLayout.PanelWidth - w) / 2f;
                    float py = RecipeBookLayout.ListArea.Y + RecipeBookLayout.HeaderRowHeight / 2 - (RecipeBookLayout.PanelHeight - h) / 2f;
                    int x = (int)MathHelper.Clamp((int)(px / w * tex.Width), 0, tex.Width - 1);
                    int y = (int)MathHelper.Clamp((int)(py / h * tex.Height), 0, tex.Height - 1);
                    var pixel = new Color[1];
                    tex.GetData(0, new Rectangle(x, y, 1, 1), pixel, 0, 1);
                    if (pixel[0].A == 255) result = pixel[0];
                }
                catch { }
            }
            headerBg = result;
            return result;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            button = TryRequest(ButtonPath);
            buttonHover = TryRequest(ButtonHoverPath);
            background = TryRequest(BackgroundPath);
            starOff = TryRequest(StarOffPath);
            starOffHighlighted = TryRequest(StarOffHighlightedPath);
            starOn = TryRequest(StarOnPath);
            starOnHighlighted = TryRequest(StarOnHighlightedPath);
            closeButton = TryRequest(CloseButtonPath);
            closeButtonHover = TryRequest(CloseButtonHoverPath);
            newMark = TryRequest(NewMarkPath);
            discovered = TryRequest(DiscoveredPath);
            modeRows = TryRequest(ModeRowsPath);
            modeGrid = TryRequest(ModeGridPath);
            modeRowsHighlight = TryRequest(ModeRowsHighlightPath);
            modeGridHighlight = TryRequest(ModeGridHighlightPath);
        }

        static Asset<Texture2D> TryRequest(string path) =>
            ModContent.HasAsset(path) ? ModContent.Request<Texture2D>(path) : null;

        public static void Clear()
        {
            button = buttonHover = background = null;
            closeButton = closeButtonHover = null;
            newMark = discovered = null;
            modeRows = modeGrid = modeRowsHighlight = modeGridHighlight = null;
            starOff = starOffHighlighted = starOn = starOnHighlighted = null;
            headerBg = null;
            loaded = false;
        }
    }
}