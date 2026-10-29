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

        // Optional, set with an object initializer. The Sun Capsule uses all three: its icon shows its own charge instead of the grid.
        public Func<(string text, Color color)> HoverOverride; // replaces the whole power readout while hovering
        public Func<bool> PulseWhen;                           // replaces the "fill above 0" rule for the background pulses
        public bool EmptyAtZero;                               // a fill of 0 draws no bolt (the normal icon never drops below its 12% floor)

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
            bool pulsing = PulseWhen != null ? PulseWhen() : drawPercent > 0f;
            if (pulsing)
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

            drawPercent = drawPercent == -1 || (EmptyAtZero && drawPercent <= 0f) ? 0f : Remap(Math.Clamp(drawPercent, 0f, 1f), 0f, 1f, 0.12f, 0.95f);
            if (drawPercent <= 0f) return;   // nothing to fill (the fill is the last thing drawn)

            int drawWidth = ElectricityFullIcon.Width();
            int drawHeight = (int)(ElectricityFullIcon.Height() * drawPercent);
            int yOffset = ElectricityFullIcon.Height() - drawHeight;

            Rectangle spriteSlice = new Rectangle(0, yOffset, drawWidth, drawHeight);
            Vector2 overlayPosition = drawPosition + new Vector2(0, yOffset * scale);

            spriteBatch.Draw(ElectricityFullIcon.Value, overlayPosition, spriteSlice, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        // Hover readout, drawn at the cursor by ElectricityHoverTip: "{demand}/{supply}" coloured by load (green under 40%, yellow under 80%,
        // red above), "Off" in red when a wire switched the machine off, "No power" in red when the grid's demand is above its supply.
        // Consumers only: "Off" in grey when the machine shares no grid with another machine, and "No power" when the grid has no supply at all.
        // Producers always show their readout (a lone producer or an empty grid reads 0/0).
        void ShowHoverText()
        {
            if (Machine == null || !IsMouseHovering) return;

            Main.LocalPlayer.mouseInterface = true;

            if (HoverOverride != null)
            {
                var (overrideText, overrideColor) = HoverOverride();
                ElectricityHoverTip.Show(overrideText, overrideColor);
                return;
            }

            if (Machine.WireToggle)
            {
                ElectricityHoverTip.Show("Off", new Color(240, 70, 70));
                return;
            }

            bool producer = Machine is IElectricProducer;

            if (!producer && !Machine.PowerNetLinked)
            {
                ElectricityHoverTip.Show("Off", new Color(150, 150, 150));
                return;
            }

            float demand = Machine.PowerNetDemand;
            float supply = Machine.PowerNetSupply;
            float charge = Machine.PowerNetCharge;                 // what the batteries are charging with right now (0 = none charging)
            float dischargeCapacity = Machine.PowerNetDischarge;   // the most the working batteries could give (0 = none)

            Color red = new Color(240, 70, 70);
            Color orange = new Color(255, 150, 40);
            Color blue = new Color(90, 170, 255);
            Color separator = new Color(190, 190, 190);

            // demand and supply are the grid's own numbers. While the batteries are lifting the grid it is switched on although demand is
            // above the plain supply: that is not "No power".
            bool liftedByBatteries = Machine.isOn && demand > supply;

            if (!liftedByBatteries && (demand > supply || (!producer && supply <= 0f) || (demand == 0 && supply == 0)))
            {
                // Batteries that are waiting for enough company: keep the numbers (red) so the player sees demand is above supply + capacity.
                if (dischargeCapacity > 0f)
                    ElectricityHoverTip.Show((PowerText(demand) + "/" + PowerText(supply), red), (" + ", separator), (PowerText(dischargeCapacity), blue));
                else
                    ElectricityHoverTip.Show("No power", red);
                return;
            }

            float effectiveSupply = liftedByBatteries ? demand : supply;
            float ratio = effectiveSupply > 0f ? demand / effectiveSupply : 0f;
            Color color = ratio < 0.4f ? new Color(80, 220, 80) : ratio < 0.8f ? new Color(255, 220, 60) : red;

            // "{charge} + {demand}/{supply} + {discharge capacity}": the charge only while a battery really charges (orange),
            // the capacity only when it is above 0 (blue); with neither it is just "demand/supply".
            var segments = new List<(string text, Color color)>();
            if (charge > 0f)
            {
                segments.Add((PowerText(charge), orange));
                segments.Add((" + ", separator));
            }

            segments.Add((PowerText(demand) + "/" + PowerText(supply), color));

            if (dischargeCapacity > 0f)
            {
                segments.Add((" + ", separator));
                segments.Add((PowerText(dischargeCapacity), blue));
            }

            ElectricityHoverTip.Show(segments.ToArray());
        }

        // Plain whole numbers. A lightning surge is logarithmic now (cosmic patch C6b), so the supply never gets astronomical and no
        // scientific notation is needed; the clamp only keeps a stacked, hand-raised MaxSurgePower from overflowing the int.
        static string PowerText(float value)
        {
            return ((int)MathF.Round(MathF.Min(value, int.MaxValue))).ToString();
        }

        float Remap(float value, float fromLow, float fromHigh, float toLow, float toHigh)
        {
            float temp1 = (value - fromLow) / (fromHigh - fromLow);
            float temp2 = toHigh - toLow;

            return toLow + temp1 * temp2;
        }
    }
}
