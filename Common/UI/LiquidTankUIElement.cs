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
    // General-purpose tank readout for ANY LiquidStack (machine InputLiquids/OutputLiquids
    // slot). Same shape as FireUIElement: a Func<> getter passed in by whoever builds the
    // UI, re-evaluated every draw so it always reflects the live machine state.
    public class LiquidTankUIElement : UIElement
    {
        // Cache keys — same string as the asset path, matching the convention
        // MachineVisualOverride.LiquidOverlayLayer already uses (CacheKey = texturePath).
        const string BorderPath = "Factorraria/Common/UI/Fluid_tank_UI_Border";
        const string BackgroundPath = "Factorraria/Common/UI/Fluid_tank_UI_Background";
        const string LiquidPath = "Factorraria/Common/UI/Fluid_tank_UI_Liquid";

        Asset<Texture2D> borderTexture;
        Asset<Texture2D> backgroundTexture;
        Asset<Texture2D> liquidTexture;

        Func<LiquidStack> GetLiquidStack;

        // Remembers the last real liquid type this tank held, since LiquidStack.LiquidType
        // itself gets reset to -1 the moment Amount drains to 0 (see
        // LiquidNetwork.WithdrawFromSlot) — the stack can't be trusted to remember its own
        // last type, so the UI element has to. Only stays at Water if a real type never showed up.
        //int lastKnownLiquidType = LiquidTypeRegistry.Water;

        // Slush bob — deliberately tiny, in on-screen pixels (post-scale), so it reads as
        // "liquid settling" rather than actual waves.
        const float BobAmplitude = 1.2f;
        const float BobSpeed = 0.05f;

        static readonly RasterizerState ScissorRasterizer = new RasterizerState
        {
            CullMode = CullMode.CullCounterClockwiseFace,
            ScissorTestEnable = true
        };

        public LiquidTankUIElement(Func<LiquidStack> _GetLiquidStack)
        {
            borderTexture = ModContent.Request<Texture2D>(BorderPath);
            backgroundTexture = ModContent.Request<Texture2D>(BackgroundPath);
            liquidTexture = ModContent.Request<Texture2D>(LiquidPath);

            GetLiquidStack = _GetLiquidStack;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            LiquidStack stack = GetLiquidStack();
            int currentType = stack.LiquidType;

            if (stack == null || stack.LiquidType == -1)
            {
                currentType = 0;
            }

            LiquidTypeDefinition liquidDef = LiquidTypeRegistry.Get(currentType);

            CalculatedStyle dimensions = GetDimensions();
            Vector2 drawPosition = dimensions.Position();
            float scale = dimensions.Width / borderTexture.Width();

            Texture2D backgroundTex = LiquidTextureCache.GetOrCreate(BackgroundPath, backgroundTexture.Value, liquidDef);
            Texture2D liquidTex = LiquidTextureCache.GetOrCreate(LiquidPath, liquidTexture.Value, liquidDef);
            Texture2D borderTex = LiquidTextureCache.GetOrCreate(BorderPath, borderTexture.Value, liquidDef);

            float fillPercent = (stack != null && stack.Capacity > 0f)
                ? Math.Clamp(stack.Amount / stack.Capacity, 0f, 1f)
                : 0f;

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            Rectangle previousScissor = device.ScissorRectangle;

            // Convert this element's UI-space bounds to real back-buffer pixels — the
            // inverse of what MachineUISystem.UpdateUIPosition does when it divides by
            // Main.UIScale to go from screen pixels into UI space.
            float uiScale = Main.UIScale;
            Rectangle tankScissorRect = new Rectangle(
                (int)(dimensions.X * uiScale),
                (int)(dimensions.Y * uiScale),
                (int)(dimensions.Width * uiScale),
                (int)(dimensions.Height * uiScale)
            );
            tankScissorRect = Rectangle.Intersect(tankScissorRect, device.Viewport.Bounds);

            spriteBatch.End();
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.NonPremultiplied,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                ScissorRasterizer,
                null,
                Main.UIScaleMatrix
            );

            device.ScissorRectangle = tankScissorRect;

            // 1. Background — drawn first, ends up visually behind everything else.
            spriteBatch.Draw(backgroundTex, drawPosition, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // 2. Liquid — NOT cropped. The full texture (surface art included) is
            // translated down as the fill percent drops, so the surface always renders
            // intact instead of being sliced away. The scissor rect (set above) hides
            // whatever spills below the tank's bottom edge once it's translated down.
            if (fillPercent > 0f)
            {
                float fullHeightScaled = liquidTexture.Height() * scale;
                float translateY = (1f - fillPercent) * fullHeightScaled;

                float bob = MathF.Sin(Main.GameUpdateCount * BobSpeed) * BobAmplitude;
                Vector2 liquidPosition = drawPosition + new Vector2(0, translateY + bob);

                spriteBatch.Draw(liquidTex, liquidPosition, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            // 3. Border — drawn last, on top, framing the tank.
            spriteBatch.Draw(borderTex, drawPosition, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            spriteBatch.End();

            device.ScissorRectangle = previousScissor;

            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.UIScaleMatrix);
        }
    }
}