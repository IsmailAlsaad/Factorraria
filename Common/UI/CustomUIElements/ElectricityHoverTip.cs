using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.UI.CustomUIElements
{
    // Draws the electricity icon's hover text in a colour at the cursor. Vanilla's hoverItemName is always white, so the icon hands its text
    // here with Show() while it is drawn, and this layer (inserted right before "Vanilla: Mouse Text", so nothing covers it) draws it.
    public class ElectricityHoverTip : ModSystem
    {
        static string pendingText;
        static Color pendingColor;

        public static void Show(string text, Color color)
        {
            pendingText = text;
            pendingColor = color;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseText = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseText == -1) return;

            layers.Insert(mouseText, new LegacyGameInterfaceLayer("Factorraria: Electricity Hover Text", delegate
            {
                DrawPending();
                return true;
            }, InterfaceScaleType.UI));
        }

        static void DrawPending()
        {
            if (pendingText == null) return;

            string text = pendingText;
            Color color = pendingColor;
            pendingText = null;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text);

            float x = Main.mouseX + 16f;
            float y = Main.mouseY + 16f;
            if (x + size.X > Main.screenWidth - 4) x = Main.screenWidth - size.X - 4f;
            if (y + size.Y > Main.screenHeight - 4) y = Main.screenHeight - size.Y - 4f;

            Utils.DrawBorderStringFourWay(Main.spriteBatch, font, text, x, y, color, Color.Black, Vector2.Zero);
        }

        public override void Unload()
        {
            pendingText = null;
        }
    }
}