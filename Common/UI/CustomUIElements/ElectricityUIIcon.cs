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

        public ElectricityUIIcon(Func<float> getAmount)
        {
            FillAmount = getAmount;

            ElectricityFullIcon = ModContent.Request<Texture2D>("Factorraria/Common/UI/CustomUIElements/Lightning_On");
            ElectricityEmptyIcon = ModContent.Request<Texture2D>("Factorraria/Common/UI/CustomUIElements/Lightning_Off");
            ElectricityIconBackground = ModContent.Request<Texture2D>("Factorraria/Common/UI/CustomUIElements/Lightning_On_Background");
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetDimensions();
            Vector2 drawPosition = dimensions.Position();

            float scale = dimensions.Width / ElectricityEmptyIcon.Width();

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

        float Remap(float value, float fromLow, float fromHigh, float toLow, float toHigh)
        {
            float temp1 = (value - fromLow) / (fromHigh - fromLow);
            float temp2 = toHigh - toLow;

            return toLow + temp1 * temp2;
        }
    }
}
