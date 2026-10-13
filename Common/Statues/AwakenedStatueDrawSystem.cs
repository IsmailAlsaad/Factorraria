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
        public float OutlineScaleAmplitude = 0.06f;   // +/- fraction of the statue size
        public float OutlineMinOpacity = 0.55f;

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
            Vector2 center = statue.Center + statue.Entry.DrawOffset;
            if (!BaseMachine.IsOnScreen(center, Math.Max(size.X, size.Y))) return;
            if (!system.IsStillAwakened(statue)) return;

            Vector2 screenCenter = center - Main.screenPosition;

            Texture2D outline = statue.Entry.Outline?.Value;
            if (outline != null)
            {
                float wave = MathF.Sin(Main.GlobalTimeWrappedHourly * OutlinePulseSpeed + statue.Origin.X * 0.37f);
                float pulse = 0.5f + 0.5f * wave;
                Vector2 outlineScale = new Vector2(size.X / outline.Width, size.Y / outline.Height) * (1f + OutlineScaleAmplitude * wave);
                Color outlineColor = Color.White * MathHelper.Lerp(OutlineMinOpacity, 1f, pulse);
                spriteBatch.Draw(outline, screenCenter, null, outlineColor, 0f, new Vector2(outline.Width / 2f, outline.Height / 2f), outlineScale, SpriteEffects.None, 0f);
            }

            Color light = Lighting.GetColor(statue.Origin.X + statue.Width / 2, statue.Origin.Y + statue.Height / 2);
            Vector2 spriteScale = new Vector2(size.X / sprite.Width, size.Y / sprite.Height);
            spriteBatch.Draw(sprite, screenCenter, null, light, 0f, new Vector2(sprite.Width / 2f, sprite.Height / 2f), spriteScale, SpriteEffects.None, 0f);
        }
    }
}