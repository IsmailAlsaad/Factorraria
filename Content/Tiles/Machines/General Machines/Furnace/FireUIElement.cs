using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Machines.Furnace
{
    public class FireUIElement : UIElement
    {
        Asset<Texture2D> fireFullTexture;
        Asset<Texture2D> fireEmptyTexture;
        Func<float> GetProgress;

        public FireUIElement(Func<float> _GetProgress)
        {
            fireFullTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Machines/General Machines/Furnace/Fire_Full");
            fireEmptyTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Machines/General Machines/Furnace/Fire_Empty");

            GetProgress = _GetProgress;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.NonPremultiplied,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.UIScaleMatrix
            );

            CalculatedStyle dimensions = GetDimensions();
            Vector2 drawPosition = dimensions.Position();

            // NEW: work out the current scale by comparing our actual on-screen width
            // (set externally, every frame, by MachineUIStateBase.SetZoomScale) to the
            // texture's real pixel width. This replaces the old remembered "scale" field.
            float scale = dimensions.Width / fireEmptyTexture.Width();

            spriteBatch.Draw(fireEmptyTexture.Value, drawPosition, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();

            float drawPercent = GetProgress();
            drawPercent = drawPercent == -1 ? 0f : Remap(drawPercent, 0f, 1f, config.fireCropOffset, 1f);

            int drawWidth = fireFullTexture.Width();
            int drawHeight = (int)(fireFullTexture.Height() * drawPercent);
            int yOffset = fireFullTexture.Height() - drawHeight;

            Rectangle spriteSlice = new Rectangle(0, yOffset, drawWidth, drawHeight);
            Vector2 overlayPosition = drawPosition + new Vector2(0, yOffset * scale);

            spriteBatch.Draw(fireFullTexture.Value, overlayPosition, spriteSlice, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            spriteBatch.End();
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.UIScaleMatrix);
        }

        float Remap(float value, float fromLow, float fromHigh, float toLow, float toHigh)
        {
            float temp1 = (value - fromLow) / (fromHigh - fromLow);
            float temp2 = toHigh - toLow;
            return toLow + temp1 * temp2;
        }
    }
}