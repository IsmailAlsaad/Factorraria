using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Text;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Factorraria.Common.UI
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

        // ButtonX/ButtonY above are the REFERENCE position (1920x1080, UI scale 100%, default accessory slots).
        // At runtime RecipeBookButtonState places the button at tModLoader's AccessorySlotLoader.DefenseIconPosition + (ButtonOffsetX, ButtonOffsetY),
        // which equals (ButtonX, ButtonY) at the reference setup by construction.
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
        public const int ScrollbarWidth = 20;

        // List rows
        public const int HeaderRowHeight = 28;
        public const int EntryRowHeight = 36;
        public const int EntryIconBox = 28;

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

        public static Rectangle ScrollbarArea => new Rectangle(ListArea.Right + 4, ListArea.Y, ScrollbarWidth, ListArea.Height);

        /// <summary>
        /// The book's "title bar": the cover strip above both pages (nothing else lives there). Drag it to move the book.
        /// Relative to the panel's top-left like every other rectangle here.
        /// </summary>
        public static Rectangle DragBar => new Rectangle(0, 0, PanelWidth, Margin);

        /// <summary>When dragging, at least this many pixels of the drag bar stay on screen horizontally.</summary>
        public const int DragKeepVisible = 60;

        /// <summary>Search bar: sits in the title strip (top TitleHeight px) of the left page, above ListArea.</summary>
        public static Rectangle SearchArea
        {
            get
            {
                Rectangle p = LeftPage;
                return new Rectangle(p.X + 10, p.Y + 8, p.Width - 20, 24);
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

            var sb = new StringBuilder();
            for (int i = 0; i < machine.Length; i++)
            {
                if (i > 0 && char.IsUpper(machine[i])) sb.Append(' ');
                sb.Append(machine[i]);
            }
            return sb.ToString();
        }

        // ---- colours (placeholder art) ----
        public static readonly Color PageColor = new Color(224, 203, 156);
        public static readonly Color PageBorder = new Color(98, 68, 38);
        public static readonly Color CoverColor = new Color(104, 70, 40);
        public static readonly Color TextColor = new Color(60, 38, 18);
        public static readonly Color TextFadedColor = new Color(120, 98, 70);

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

        static Asset<Texture2D> button, buttonHover, background;
        static bool loaded;

        public static Asset<Texture2D> Button { get { Load(); return button; } }
        public static Asset<Texture2D> ButtonHover { get { Load(); return buttonHover; } }
        public static Asset<Texture2D> Background { get { Load(); return background; } }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            button = TryRequest(ButtonPath);
            buttonHover = TryRequest(ButtonHoverPath);
            background = TryRequest(BackgroundPath);
        }

        static Asset<Texture2D> TryRequest(string path) =>
            ModContent.HasAsset(path) ? ModContent.Request<Texture2D>(path) : null;

        public static void Clear()
        {
            button = buttonHover = background = null;
            loaded = false;
        }
    }
}