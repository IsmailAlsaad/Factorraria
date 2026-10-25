using Factorraria.Common.Machines;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.UI.CustomUIElements
{
    public class ElectricityUIIcon : UIElement
    {
        Asset<Texture2D> ElectricityFullIcon;
        Asset<Texture2D> ElectricityEmptyIcon;

        Asset<Texture2D> ElectricityIconBackground;
        int BackgroundLifespan = 60;      // frames a pulse lives
        int BackgroundSpawnInterval = 60; // frames between pulses
        int PulseCount = 0;
        int Timer = 0;

        Func<float> FillAmount;
        Func<bool> IsVisible; // optional: the icon is skipped while this returns false
        Func<bool> IsDisabled; // optional: while this returns true (switched off by a wire) the icon shows the disabled background
        Asset<Texture2D> ElectricityDisabledBackground; // Lightning_Off_Background.png; null until the art exists

        BaseMachine Machine; // optional: when set, hovering the icon shows the power readout next to the cursor

        public ElectricityUIIcon(Func<float> getAmount, Func<bool> isVisible = null, Func<bool> isDisabled = null, BaseMachine machine = null)
        {
            FillAmount = getAmount;
            Machine = machine;
            IsVisible = isVisible;
            IsDisabled = isDisabled;

            // RequestIfExists: a missing PNG logs a warning instead of crashing (the draw code falls back to a tinted background).
            ModContent.RequestIfExists<Texture2D>("Factorraria/Common/UI/CustomUIElements/Lightning_Off_Background", out ElectricityDisabledBackground);

            ElectricityFullIcon = ModContent.Request<Texture2D>("Factorraria/Common/UI/CustomUIElements/Lightning_On");
            ElectricityEmptyIcon = ModContent.Request<Texture2D>("Factorraria/Common/UI/CustomUIElements/Lightning_Off");
            ElectricityIconBackground = ModContent.Request<Texture2D>("Factorraria/Common/UI/CustomUIElements/Lightning_On_Background");
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (IsVisible != null && !IsVisible()) return;

            ShowHoverText();

            CalculatedStyle dimensions = GetDimensions();
            Vector2 drawPosition = dimensions.Position();

            float scale = dimensions.Width / ElectricityEmptyIcon.Width();

            // Switched off by a wire signal: a static disabled background behind the empty bolt, no pulses and no fill.
            if (IsDisabled != null && IsDisabled())
            {
                spriteBatch.Draw(ElectricityEmptyIcon.Value, drawPosition, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                Texture2D disabledBackground = ElectricityDisabledBackground?.Value;
                if (disabledBackground != null)
                    spriteBatch.Draw(disabledBackground, drawPosition, null, Color.White, 0f, Vector2.Zero, dimensions.Width / disabledBackground.Width, SpriteEffects.None, 0f);
                else
                    spriteBatch.Draw(ElectricityIconBackground.Value, drawPosition, null, new Color(110, 110, 110) * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                return;
            }

            spriteBatch.Draw(ElectricityEmptyIcon.Value, drawPosition, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            //
            //FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            //

            float drawPercent = FillAmount();

            Timer++;
            if (drawPercent > 0f)
            {
                PulseCount = (int)BackgroundLifespan / BackgroundSpawnInterval;

                for (int k = 0; k < PulseCount; k++)
                {
                    int age = (Timer + k * BackgroundSpawnInterval) % BackgroundLifespan;
                    float lifeFraction = age / (float)BackgroundLifespan;

                    Color backgroundColor = Color.White * MathF.Pow(1f - lifeFraction, 2f);
                    float scaleMultiplier = 1f + 0.5f * lifeFraction;

                    Vector2 pulseOrigin = ElectricityIconBackground.Size() / 2f;
                    Vector2 pulseCenter = drawPosition + ElectricityIconBackground.Size() * scale / 2f;

                    spriteBatch.Draw(ElectricityIconBackground.Value, pulseCenter, null, backgroundColor, 0f, pulseOrigin, scale * scaleMultiplier, SpriteEffects.None, 0f);
                }
            }

            drawPercent = drawPercent == -1 ? 0f : Remap(drawPercent, 0f, 1f, 0.12f, 0.95f);

            int drawWidth = ElectricityFullIcon.Width();
            int drawHeight = (int)(ElectricityFullIcon.Height() * drawPercent);
            int yOffset = ElectricityFullIcon.Height() - drawHeight;

            Rectangle spriteSlice = new Rectangle(0, yOffset, drawWidth, drawHeight);
            Vector2 overlayPosition = drawPosition + new Vector2(0, yOffset * scale);

            spriteBatch.Draw(ElectricityFullIcon.Value, overlayPosition, spriteSlice, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        // Hover readout, drawn at the cursor by ElectricityHoverTip: "{demand}/{supply}" coloured by load (green under 40%, yellow under 80%,
        // red above), "Off" in red when a wire switched the machine off, "Off" in grey when it shares no grid with another machine,
        // "No power" in red when the grid cannot cover its demand (or has no supply at all).
        void ShowHoverText()
        {
            if (Machine == null || !IsMouseHovering) return;

            Main.LocalPlayer.mouseInterface = true;

            if (Machine.WireToggle)
            {
                ElectricityHoverTip.Show("Off", new Color(240, 70, 70));
                return;
            }

            if (!Machine.PowerNetLinked)
            {
                ElectricityHoverTip.Show("Off", new Color(150, 150, 150));
                return;
            }

            float demand = Machine.PowerNetDemand;
            float supply = Machine.PowerNetSupply;

            if (supply <= 0f || demand > supply)
            {
                ElectricityHoverTip.Show("No power", new Color(240, 70, 70));
                return;
            }

            float ratio = demand / supply;
            Color color = ratio < 0.4f ? new Color(80, 220, 80) : ratio < 0.8f ? new Color(255, 220, 60) : new Color(240, 70, 70);
            ElectricityHoverTip.Show((int)MathF.Round(demand) + "/" + (int)MathF.Round(supply), color);
        }

        float Remap(float value, float fromLow, float fromHigh, float toLow, float toHigh)
        {
            float temp1 = (value - fromLow) / (fromHigh - fromLow);
            float temp2 = toHigh - toLow;

            return toLow + temp1 * temp2;
        }
    }
}
