using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>
    /// The "Inherits from [icon] Anvils and [icon] ..." tip of a recipe book tab header. A header asks for it while the mouse is on its
    /// title; RecipeBookTabTipOverlay (the last child of the book panel) draws it after everything else, so no row or clip box covers it.
    /// Client-only, nothing is saved or synced.
    /// </summary>
    public static class RecipeBookTabTip
    {
        static string[] pending;

        /// <summary>Catalog names of the stations to list. Call from a header's draw; the request lasts one frame.</summary>
        public static void Request(string[] stationNames) => pending = stationNames;

        internal static string[] TakePending()
        {
            string[] taken = pending;
            pending = null;
            return taken;
        }
    }

    public class RecipeBookTabTipOverlay : UIElement
    {
        // Tunables (not const, so hot reload can change them).
        public static float TextScale = 0.9f;
        public static int IconBox = 22;
        public static int Pad = 8;

        public RecipeBookTabTipOverlay()
        {
            Width.Set(0f, 1f);
            Height.Set(0f, 1f);
            IgnoresMouseInteraction = true;
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            string[] stations = RecipeBookTabTip.TakePending();
            if (stations == null || stations.Length == 0) return;

            var font = FontAssets.MouseText.Value;
            string lead = RecipeBookLayout.Text("InheritsFrom");
            string andText = " " + RecipeBookLayout.Text("And") + " ";
            int second = (int)Main.GlobalTimeWrappedHourly;   // the sprite of a station with several items changes once a second

            var types = new int[stations.Length];
            var names = new string[stations.Length];
            float leadWidth = font.MeasureString(lead).X * TextScale + 6f;
            float andWidth = font.MeasureString(andText).X * TextScale;
            float width = Pad * 2 + leadWidth;
            for (int i = 0; i < stations.Length; i++)
            {
                names[i] = RecipeBookLayout.MachineName(stations[i]);
                int[] items = RecipeTabDiscovery.ItemsOf(stations[i]);
                types[i] = items.Length > 0 ? items[second % items.Length] : 0;
                if (i > 0) width += andWidth;
                width += (types[i] > 0 ? IconBox + 4 : 0) + font.MeasureString(names[i]).X * TextScale;
            }
            float height = IconBox + Pad * 2;

            Vector2 mouse = UserInterface.ActiveInstance.MousePosition;
            float x = mouse.X + 14f;
            float y = mouse.Y + 18f;
            float maxX = Main.screenWidth / Main.UIScale - 4f;
            float maxY = Main.screenHeight / Main.UIScale - 4f;
            if (x + width > maxX) x = maxX - width;
            if (y + height > maxY) y = mouse.Y - height - 6f;

            var box = new Rectangle((int)x, (int)y, (int)width, (int)height);
            BookDraw.Frame(sb, box, RecipeBookLayout.PageColor, RecipeBookLayout.PageBorder, 2);

            float textY = box.Center.Y - font.LineSpacing * TextScale / 2f;
            float cursor = box.X + Pad;
            BookDraw.Text(sb, lead, new Vector2(cursor, textY), RecipeBookLayout.TextColor, TextScale);
            cursor += leadWidth;
            for (int i = 0; i < stations.Length; i++)
            {
                if (i > 0)
                {
                    BookDraw.Text(sb, andText, new Vector2(cursor, textY), RecipeBookLayout.TextColor, TextScale);
                    cursor += andWidth;
                }
                if (types[i] > 0)
                {
                    RecipeIcon.Draw(sb, RecipeOutputKey.ForItem(types[i]), new Vector2(cursor + IconBox / 2f, box.Center.Y), IconBox);
                    cursor += IconBox + 4;
                }
                BookDraw.Text(sb, names[i], new Vector2(cursor, textY), RecipeBookLayout.TextColor, TextScale);
                cursor += font.MeasureString(names[i]).X * TextScale;
            }
        }
    }
}