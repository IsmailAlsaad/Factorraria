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
        // Lines stacked under each other, each line made of coloured pieces.
        static (string text, Color color)[][] pendingSegments;

        public static void Show(string text, Color color)
        {
            pendingSegments = new[] { new[] { (text, color) } };
        }

        // Several pieces drawn one after the other on the same line, each in its own colour (the battery readout: orange, grey "+", blue).
        public static void Show(params (string text, Color color)[] segments)
        {
            pendingSegments = new[] { segments };
        }

        // Several lines stacked under each other, each line made of coloured pieces (the priority conveyor readout).
        public static void ShowLines(params (string text, Color color)[][] lines)
        {
            pendingSegments = lines;
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

            var lines = pendingSegments;
            pendingSegments = null;

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            float width = 0f;
            float height = 0f;
            float[] lineHeights = new float[lines.Length];
            for (int l = 0; l < lines.Length; l++)
            {
                float lineWidth = 0f;
                foreach (var segment in lines[l])
                {
                    Vector2 segmentSize = font.MeasureString(segment.text);
                    lineWidth += segmentSize.X;
                    if (segmentSize.Y > lineHeights[l]) lineHeights[l] = segmentSize.Y;
                }
                if (lineWidth > width) width = lineWidth;
                height += lineHeights[l];
            }

            float x = Main.mouseX + 16f;
            float y = Main.mouseY + 16f;
            if (x + width > Main.screenWidth - 4) x = Main.screenWidth - width - 4f;
            if (y + height > Main.screenHeight - 4) y = Main.screenHeight - height - 4f;

            for (int l = 0; l < lines.Length; l++)
            {
                float lineX = x;
                foreach (var segment in lines[l])
                {
                    Utils.DrawBorderStringFourWay(Main.spriteBatch, font, segment.text, lineX, y, segment.color, Color.Black, Vector2.Zero);
                    lineX += font.MeasureString(segment.text).X;
                }
                y += lineHeights[l];
            }
        }

        public override void Unload()
        {
            pendingSegments = null;
        }
    }
}