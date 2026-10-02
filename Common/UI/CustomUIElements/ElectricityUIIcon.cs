using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
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
            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            //

            float drawPercent = FillAmount();
            drawPercent = drawPercent == -1 ? 0f : Remap(drawPercent, 0f, 1f, 0.15f, 0.95f);

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
