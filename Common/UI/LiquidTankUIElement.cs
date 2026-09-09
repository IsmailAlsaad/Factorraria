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
    // Draw order: border (this element's own DrawSelf, bottom layer) -> liquid (a child
    // with OverflowHidden = true, so Terraria's own UI clipping crops anything that
    // overflows the tank rect — no manual scissor/render-target code needed) -> background
    // (a second child, appended after liquid, so it draws on top and frames it).
    public class LiquidTankUIElement : UIElement
    {
        const string BorderPath = "Factorraria/Common/UI/Fluid_tank_UI_Border";
        const string BackgroundPath = "Factorraria/Common/UI/Fluid_tank_UI_Background";
        const string LiquidPath = "Factorraria/Common/UI/Fluid_tank_UI_Liquid";

        Asset<Texture2D> borderTexture;
        Asset<Texture2D> backgroundTexture;
        Asset<Texture2D> liquidTexture;

        Func<LiquidStack> GetLiquidStack;

        LiquidLayer liquidLayer;
        BackgroundLayer backgroundLayer;

        public LiquidTankUIElement(Func<LiquidStack> _GetLiquidStack)
        {
            borderTexture = ModContent.Request<Texture2D>(BorderPath);
            backgroundTexture = ModContent.Request<Texture2D>(BackgroundPath);
            liquidTexture = ModContent.Request<Texture2D>(LiquidPath);

            GetLiquidStack = _GetLiquidStack;

            // Both children sized as 100% of the parent (percent, not fixed pixels) so they
            // auto-track this element's own Width/Height whenever zoom changes — no manual
            // resize-on-Recalculate bookkeeping needed.
            liquidLayer = new LiquidLayer { OverflowHidden = true };
            liquidLayer.Width.Set(0f, 1f);
            liquidLayer.Height.Set(0f, 1f);
            Append(liquidLayer);

            backgroundLayer = new BackgroundLayer();
            backgroundLayer.Width.Set(0f, 1f);
            backgroundLayer.Height.Set(0f, 1f);
            Append(backgroundLayer);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            LiquidStack stack = GetLiquidStack();
            int currentType = (stack == null || stack.LiquidType == -1) ? 0 : stack.LiquidType;
            LiquidTypeDefinition liquidDef = LiquidTypeRegistry.Get(currentType);

            CalculatedStyle dimensions = GetDimensions();
            float scale = dimensions.Width / borderTexture.Width();

            Texture2D borderTex = LiquidTextureCache.GetOrCreate(BorderPath, borderTexture.Value, liquidDef);
            Texture2D backgroundTex = LiquidTextureCache.GetOrCreate(BackgroundPath, backgroundTexture.Value, liquidDef);
            Texture2D liquidTex = LiquidTextureCache.GetOrCreate(LiquidPath, liquidTexture.Value, liquidDef);

            float fillPercent = (stack != null && stack.Capacity > 0f)
                ? Math.Clamp(stack.Amount / stack.Capacity, 0f, 1f)
                : 0f;

            // 1. Border — bottom layer, drawn as part of this element's own DrawSelf, so it
            // renders before either child (liquid, background) below it.
            spriteBatch.Draw(borderTex, dimensions.Position(), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Hand this frame's values to the children — they draw themselves right after
            // this method returns, in the order they were Appended (liquid, then background).
            liquidLayer.Texture = liquidTex;
            liquidLayer.Scale = scale;
            liquidLayer.FillPercent = fillPercent;

            backgroundLayer.Texture = backgroundTex;
            backgroundLayer.Scale = scale;
        }

        // 2. Liquid — clipped to this element's own rectangle by OverflowHidden, so anything
        // that spills past the tank simply doesn't draw. Rectangular, not pixel-accurate to
        // the background's shape — but simple, and good enough for a tank interior.
        class LiquidLayer : UIElement
        {
            public Texture2D Texture;
            public float Scale;
            public float FillPercent;

            const float BobAmplitude = 1.2f;
            const float BobSpeed = 0.05f;

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                if (Texture == null || FillPercent <= 0f) return;

                CalculatedStyle dimensions = GetDimensions();
                float translateY = (1f - FillPercent) * Texture.Height * Scale;
                float bob = MathF.Sin(Main.GameUpdateCount * BobSpeed) * BobAmplitude;
                Vector2 drawPosition = dimensions.Position() + new Vector2(0, translateY + bob);

                spriteBatch.Draw(Texture, drawPosition, null, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            }
        }

        // 3. Background — drawn last (appended after liquidLayer), so it renders on top.
        class BackgroundLayer : UIElement
        {
            public Texture2D Texture;
            public float Scale;

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                if (Texture == null) return;

                CalculatedStyle dimensions = GetDimensions();
                spriteBatch.Draw(Texture, dimensions.Position(), null, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            }
        }
    }
}