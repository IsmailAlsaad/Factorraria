using Factorraria.Common.Liquids;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Content.UI
{
    // Draw order (back to front): background -> liquid (clipped, bobbing) -> border (always on top).
    // All three are drawn directly here in DrawSelf rather than as separate UIElement
    // children — UIElement's own OverflowHidden wrapping resets sampler/rasterizer state
    // in ways that broke crispness for whatever drew after it, and SpriteSortMode.Deferred
    // only applies the LAST scissor rectangle set before End() to the whole batch, so
    // per-layer clipping via child elements couldn't work correctly either way.
    public class LiquidTankUIElement : UIElement
    {
        const string BorderPath = "Factorraria/Common/UI/Fluid_tank_UI_Border";
        const string BackgroundPath = "Factorraria/Common/UI/Fluid_tank_UI_Background";
        const string LiquidPath = "Factorraria/Common/UI/Fluid_tank_UI_Liquid";

        const float BobAmplitude = 1.2f;
        const float BobSpeed = 0.05f;

        // ScissorTestEnable is required for GraphicsDevice.ScissorRectangle to have any
        // effect at all — CullCounterClockwise alone (vanilla UI's usual state) ignores it.
        static readonly RasterizerState ScissorEnabledCrisp = new RasterizerState
        {
            CullMode = CullMode.CullCounterClockwiseFace,
            ScissorTestEnable = true
        };

        Asset<Texture2D> borderTexture;
        Asset<Texture2D> backgroundTexture;
        Asset<Texture2D> liquidTexture;

        Func<LiquidStack> GetLiquidStack;

        public LiquidTankUIElement(Func<LiquidStack> _GetLiquidStack)
        {
            borderTexture = ModContent.Request<Texture2D>(BorderPath, AssetRequestMode.ImmediateLoad);
            backgroundTexture = ModContent.Request<Texture2D>(BackgroundPath, AssetRequestMode.ImmediateLoad);
            liquidTexture = ModContent.Request<Texture2D>(LiquidPath, AssetRequestMode.ImmediateLoad);

            GetLiquidStack = _GetLiquidStack;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            LiquidStack stack = GetLiquidStack();
            int currentType = (stack == null || stack.LiquidType == -1) ? LiquidTypeRegistry.Water : stack.LiquidType;
            LiquidTypeDefinition liquidDef = LiquidTypeRegistry.Get(currentType);

            CalculatedStyle dimensions = GetDimensions();
            float scale = dimensions.Width / borderTexture.Width();

            float fillPercent = (stack != null && stack.Capacity > 0f)
                ? Math.Clamp(stack.Amount / stack.Capacity, 0f, 1f)
                : 0f;

            Texture2D backgroundTex = LiquidTextureCache.GetOrCreate(BackgroundPath, backgroundTexture.Value, liquidDef);
            Texture2D liquidTex = LiquidTextureCache.GetOrCreate(LiquidPath, liquidTexture.Value, liquidDef);
            Texture2D borderTex = LiquidTextureCache.GetOrCreate(BorderPath, borderTexture.Value, liquidDef);

            GraphicsDevice device = spriteBatch.GraphicsDevice;
            Rectangle previousScissor = device.ScissorRectangle;

            // Immediate, not Deferred — we need the scissor rectangle to change
            // mid-batch (unclipped background -> clipped liquid -> unclipped border).
            // Deferred would only apply whichever scissor was last set before End() to
            // the whole flushed batch at once, silently ignoring the per-call changes.
            spriteBatch.End();
            spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                ScissorEnabledCrisp,
                null,
                Main.UIScaleMatrix);

            // 1. Background — bottom layer, unclipped (fills the whole box anyway).
            spriteBatch.Draw(backgroundTex, dimensions.Position(), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // 2. Liquid — clipped to this element's own screen-space rect, intersected
            // with whatever scissor was already active (so a scrollable parent's own
            // clip region is still respected too).
            if (fillPercent > 0f)
            {
                Rectangle tankScreenRect = ToScreenRectangle(dimensions);
                Rectangle clipRect = Rectangle.Intersect(previousScissor, tankScreenRect);

                if (clipRect.Width > 0 && clipRect.Height > 0)
                {
                    device.ScissorRectangle = clipRect;

                    float translateY = (1f - fillPercent) * liquidTex.Height * scale;
                    float bob = MathF.Sin(Main.GameUpdateCount * BobSpeed) * BobAmplitude;
                    Vector2 liquidPosition = dimensions.Position() + new Vector2(0, translateY + bob);

                    spriteBatch.Draw(liquidTex, liquidPosition, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                    device.ScissorRectangle = previousScissor;
                }
            }

            // 3. Border — always on top, unclipped.
            spriteBatch.Draw(borderTex, dimensions.Position(), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            spriteBatch.End();
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.UIScaleMatrix);
        }

        // GetDimensions() returns coordinates in the UI's own local unit space —
        // GraphicsDevice.ScissorRectangle wants actual screen pixels, which is what
        // Main.UIScaleMatrix (the transform this whole batch draws through) maps into.
        // Skipping this and using dimensions directly as pixels would misalign the crop
        // whenever the player's UI Scale setting isn't 100%.
        static Rectangle ToScreenRectangle(CalculatedStyle dimensions)
        {
            Vector2 topLeft = Vector2.Transform(new Vector2(dimensions.X, dimensions.Y), Main.UIScaleMatrix);
            Vector2 bottomRight = Vector2.Transform(new Vector2(dimensions.X + dimensions.Width, dimensions.Y + dimensions.Height), Main.UIScaleMatrix);
            return new Rectangle(
                (int)topLeft.X,
                (int)topLeft.Y,
                (int)MathF.Round(bottomRight.X - topLeft.X),
                (int)MathF.Round(bottomRight.Y - topLeft.Y));
        }
    }
}