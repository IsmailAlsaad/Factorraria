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
        static (string text, Color color)[] pendingSegments;

        public static void Show(string text, Color color)
        {
            pendingSegments = new[] { (text, color) };
        }

        // Several pieces drawn one after the other on the same line, each in its own colour (the battery readout: orange, grey "+", blue).
        public static void Show(params (string text, Color color)[] segments)
        {
            pendingSegments = segments;
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
            if (pendingSegments == null) return;

            var segments = pendingSegments;
            pendingSegments = null;

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            float width = 0f;
            float height = 0f;
            foreach (var segment in segments)
            {
                Vector2 segmentSize = font.MeasureString(segment.text);
                width += segmentSize.X;
                if (segmentSize.Y > height) height = segmentSize.Y;
            }

            float x = Main.mouseX + 16f;
            float y = Main.mouseY + 16f;
            if (x + width > Main.screenWidth - 4) x = Main.screenWidth - width - 4f;
            if (y + height > Main.screenHeight - 4) y = Main.screenHeight - height - 4f;

            foreach (var segment in segments)
            {
                Utils.DrawBorderStringFourWay(Main.spriteBatch, font, segment.text, x, y, segment.color, Color.Black, Vector2.Zero);
                x += font.MeasureString(segment.text).X;
            }
        }

        public override void Unload()
        {
            pendingSegments = null;
        }
    }
}