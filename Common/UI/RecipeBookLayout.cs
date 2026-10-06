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
        // Inventory button (absolute UI position). Sits to the right of the coin/ammo slots, above them.
        public const int ButtonX = 590;
        public const int ButtonY = 20;
        public const int ButtonSize = 36;

        // Book panel
        public const int PanelWidth = 780;
        public const int PanelHeight = 470;
        public const int Margin = 22;
        public const int Gutter = 14;          // half the gap between the two pages (the spine)
        public const int TitleHeight = 40;
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

        public static Rectangle DetailArea
        {
            get
            {
                Rectangle p = RightPage;
                return new Rectangle(p.X + 12, p.Y + 12, p.Width - 24, p.Height - 24);
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