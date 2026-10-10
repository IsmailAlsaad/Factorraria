using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.UI
{
    /// <summary>
    /// Button under the vanilla chest grid that cycles the open chest's conveyor mode (Input/Output -> Input -> Output).
    /// Only shown for chests in ChestConveyorSystem's registry. Drawn immediate-mode in its own interface layer.
    /// </summary>
    public class ChestConveyorModeUI : ModSystem
    {
        // Tweakable (static fields, NOT const, so hot reload can change them).
        public static float ButtonBaseX = 73f;       // same left edge as the vanilla chest grid
        public static float ChestGridRows = 4f;
        public static float SlotPitch = 56f * 0.755f; // vanilla chest slot size at the scale it draws them
        public static float GapBelowGrid = 8f;
        public static float ButtonOffsetX = 0f;      // nudge the button without touching the maths above
        public static float ButtonOffsetY = 0f;
        public static float TextScale = 0.9f;
        public static int PadX = 10;
        public static int PadY = 5;

        public static Color InputColor = new Color(150, 190, 255);   // pastel blue
        public static Color OutputColor = new Color(255, 150, 150);  // pastel red
        public static Color SeparatorColor = new Color(220, 220, 220);

        private struct Segment
        {
            public string Text;
            public Color Color;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex == -1)
            {
                return;
            }

            layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                "Factorraria: Chest Conveyor Mode",
                delegate
                {
                    DrawButton();
                    return true;
                },
                InterfaceScaleType.UI));
        }

        private static void DrawButton()
        {
            Player player = Main.LocalPlayer;

            if (!Main.playerInventory || player.chest < 0 || player.chest >= Main.maxChests)
            {
                return;
            }

            Chest chest = Main.chest[player.chest];
            if (chest == null)
            {
                return;
            }

            if (!ChestConveyorSystem.TryGetEntry(chest.x, chest.y, out ChestConveyorEntry entry))
            {
                return;
            }

            Segment[] segments = GetSegments(entry.Mode);
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            float textWidth = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                textWidth += font.MeasureString(segments[i].Text).X * TextScale;
            }

            float textHeight = font.MeasureString("Input").Y * TextScale;

            float x = ButtonBaseX + ButtonOffsetX;
            float y = Main.instance.invBottom + ChestGridRows * SlotPitch + GapBelowGrid + ButtonOffsetY;

            int width = (int)textWidth + PadX * 2;
            int height = (int)textHeight + PadY * 2;

            // Keep it on screen on small windows
            float maxY = Main.screenHeight / Main.UIScale - height - 4f;
            if (y > maxY)
            {
                y = maxY;
            }

            Rectangle rect = new Rectangle((int)x, (int)y, width, height);
            bool hover = rect.Contains(Main.mouseX, Main.mouseY);

            //Utils.DrawInvBG(Main.spriteBatch, rect, hover ? new Color(100, 105, 190) * 0.9f : new Color(63, 65, 151) * 0.785f);

            float drawX = rect.X + PadX;
            for (int i = 0; i < segments.Length; i++)
            {
                Utils.DrawBorderString(Main.spriteBatch, segments[i].Text, new Vector2(drawX, rect.Y + PadY), segments[i].Color, TextScale);
                drawX += font.MeasureString(segments[i].Text).X * TextScale;
            }

            if (!hover)
            {
                return;
            }

            player.mouseInterface = true;
            Main.instance.MouseText(GetTooltip(entry.Mode));

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease = false;
                ChestConveyorSystem.CycleMode(entry);
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }

        private static Segment[] GetSegments(ChestConveyorMode mode)
        {
            switch (mode)
            {
                case ChestConveyorMode.Input:
                    return new[] { new Segment { Text = "Input", Color = InputColor } };

                case ChestConveyorMode.Output:
                    return new[] { new Segment { Text = "Output", Color = OutputColor } };

                default:
                    return new[]
                    {
                        new Segment { Text = "Input", Color = InputColor },
                        new Segment { Text = "/", Color = SeparatorColor },
                        new Segment { Text = "Output", Color = OutputColor }
                    };
            }
        }

        private static string GetTooltip(ChestConveyorMode mode)
        {
            switch (mode)
            {
                case ChestConveyorMode.Input:
                    return "Takes items in";

                case ChestConveyorMode.Output:
                    return "Puts items out";

                default:
                    return "Takes items in and puts items out";
            }
        }
    }
}