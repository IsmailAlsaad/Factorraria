using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Furnace
{
    public class FireUIElement : UIElement
    {
        Asset<Texture2D> fireFullTexture;
        Asset<Texture2D> fireEmptyTexture;
        Func<float> GetProgress;

        float scale;

        public FireUIElement(Func<float> _GetProgress)
        {
            fireFullTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Furnace/Fire_Full");
            fireEmptyTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Furnace/Fire_Empty");

            GetProgress = _GetProgress;

            setScale(1f);
        }

        public void setScale(float _scale)
        {
            scale = _scale;
            Height.Set(fireEmptyTexture.Height() * scale, 0f);
            Width.Set(fireEmptyTexture.Width() * scale, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            // 1. Pause the game's default UI drawing layer
            spriteBatch.End();
            // 2. Restart it using NonPremultiplied blending rules to support standard PNG transparency
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.NonPremultiplied, // <--- THE FIX
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.UIScaleMatrix
            );

            CalculatedStyle dimensions = GetDimensions();
            Vector2 drawPosition = dimensions.Position();

            spriteBatch.Draw(fireEmptyTexture.Value, drawPosition,null, Color.White,0f, Vector2.Zero,scale,SpriteEffects.None,0f);

            //
            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            //

            float drawPercent = Math.Clamp(GetProgress(),0f,1f);
            drawPercent = Remap(drawPercent, 0f, 1f, config.fireCropOffset, 1f);

            //
            //Main.NewText($"Smelt percentage = {drawPercent}");
            //

            int drawWidth = fireFullTexture.Width();
            int drawHeight = (int)(fireFullTexture.Height() * drawPercent);
            int yOffset = fireFullTexture.Height() - drawHeight;

            Rectangle spriteSlice = new Rectangle(0, yOffset, drawWidth, drawHeight);
            Vector2 overlayPosition = drawPosition + new Vector2(0, yOffset * scale);
            //overlayPosition *= scale;

            spriteBatch.Draw(fireFullTexture.Value,overlayPosition, spriteSlice, Color.White,0f,Vector2.Zero,scale,SpriteEffects.None,0f);

            // 3. Close your custom pass and return the batch to default behavior so other UI elements don't break
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

        float Remap(float value, float fromLow, float fromHigh,float toLow, float toHigh)
        {
            float temp1 = (value - fromLow) / (fromHigh - fromLow);
            float temp2 = toHigh - toLow;
            return toLow + temp1 * temp2;
        }
    }
}