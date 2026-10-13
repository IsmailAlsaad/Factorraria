using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Statues
{
    // Draws every awakened statue's saturated sprite over the vanilla statue, with a pulsing outline behind it.
    public class AwakenedStatueDrawSystem : ModSystem
    {
        public float OutlinePulseSpeed = 3f;          // radians per second
        public float OutlineScaleAmplitude = 0.001f;   // +/- fraction of the statue size
        public float OutlineMinOpacity = 0.3f;

        // The sprite and outline PNGs are the statue footprint plus this much transparent padding (pixels), so art can overhang.
        // A 2x3 statue with 16 left/right/top is a 64x64 PNG with the footprint in the bottom middle.
        public int PadLeft = 16;
        public int PadRight = 16;
        public int PadTop = 16;
        public int PadBottom = 0;

        public override void PostDrawTiles()
        {
            if (Main.dedServ) return;

            var system = ModContent.GetInstance<AwakenedStatueSystem>();
            bool altHeld = Main.keyState.IsKeyDown(Keys.LeftAlt);
            if (system.Awakened.Count == 0 && !altHeld) return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (AwakenedStatue statue in system.Awakened.Values)
                DrawStatue(spriteBatch, system, statue);

            spriteBatch.End();
        }

        void DrawStatue(SpriteBatch spriteBatch, AwakenedStatueSystem system, AwakenedStatue statue)
        {
            Texture2D sprite = statue.Entry.Sprite?.Value;
            if (sprite == null) return;

            Vector2 size = new Vector2(statue.Width * 16, statue.Height * 16);
            bool flip = statue.Entry.FlipHorizontal;
            Vector2 offset = statue.Entry.DrawOffset;
            if (flip) offset.X = -offset.X;   // the right-facing statue mirrors the offset too
            SpriteEffects effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 center = statue.Center + offset;
            if (!BaseMachine.IsOnScreen(center, Math.Max(size.X, size.Y) + Math.Max(Math.Max(PadLeft, PadRight), Math.Max(PadTop, PadBottom)))) return;
            if (!system.IsStillAwakened(statue)) return;

            Vector2 screenCenter = center - Main.screenPosition;

            Texture2D outline = statue.Entry.Outline?.Value;
            if (outline != null)
            {
                float wave = MathF.Sin(Main.GlobalTimeWrappedHourly * OutlinePulseSpeed + (statue.Origin.X + statue.Origin.Y) * 0.37f);
                float pulse = 0.5f + 0.5f * wave;
                Vector2 outlineScale = CanvasScale(outline, size) * (1f + OutlineScaleAmplitude * wave);
                Color outlineColor = Color.White * MathHelper.Lerp(OutlineMinOpacity, 1f, pulse);
                spriteBatch.Draw(outline, screenCenter, null, outlineColor, 0f, CanvasOrigin(outline, size, flip), outlineScale, effects, 0f);
            }

            Color light = Lighting.GetColor(statue.Origin.X + statue.Width / 2, statue.Origin.Y + statue.Height / 2);
            Vector2 spriteScale = CanvasScale(sprite, size);
            spriteBatch.Draw(sprite, screenCenter, null, light, 0f, CanvasOrigin(sprite, size, flip), spriteScale, effects, 0f);
        }

        // Scale from PNG pixels to world pixels: the PNG covers the footprint plus the padding.
        Vector2 CanvasScale(Texture2D texture, Vector2 footprint)
            => new Vector2((footprint.X + PadLeft + PadRight) / texture.Width, (footprint.Y + PadTop + PadBottom) / texture.Height);

        // The point of the PNG (in PNG pixels) that sits on the footprint's center. SpriteBatch mirrors only the texture, not the
        // origin, so a flipped draw needs the origin X mirrored too (stays correct if the left and right padding differ).
        Vector2 CanvasOrigin(Texture2D texture, Vector2 footprint, bool flip = false)
        {
            Vector2 scale = CanvasScale(texture, footprint);
            float x = (PadLeft + footprint.X / 2f) / scale.X;
            if (flip) x = texture.Width - x;
            return new Vector2(x, (PadTop + footprint.Y / 2f) / scale.Y);
        }
    }
}