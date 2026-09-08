using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Liquids
{
    public static class LiquidRecolorer
    {
        private static float SrgbToLinear(float c) =>
            c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static float LinearToSrgb(float c)
        {
            c = Math.Clamp(c, 0f, 1f);
            return c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;
        }

        private static void RgbToOklab(Color c, out float L, out float A, out float B)
        {
            float r = SrgbToLinear(c.R / 255f);
            float g = SrgbToLinear(c.G / 255f);
            float b = SrgbToLinear(c.B / 255f);

            float l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b;
            float m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b;
            float s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b;

            l = MathF.Cbrt(l); m = MathF.Cbrt(m); s = MathF.Cbrt(s);

            L = 0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s;
            A = 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s;
            B = 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s;
        }

        private static Color OklabToRgb(float L, float A, float B, byte alpha)
        {
            float l_ = L + 0.3963377774f * A + 0.2158037573f * B;
            float m_ = L - 0.1055613458f * A - 0.0638541728f * B;
            float s_ = L - 0.0894841775f * A - 1.2914855480f * B;

            float l = l_ * l_ * l_;
            float m = m_ * m_ * m_;
            float s = s_ * s_ * s_;

            float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
            float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
            float b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;

            byte R = (byte)Math.Clamp(LinearToSrgb(r) * 255f, 0f, 255f);
            byte G = (byte)Math.Clamp(LinearToSrgb(g) * 255f, 0f, 255f);
            byte Bc = (byte)Math.Clamp(LinearToSrgb(b) * 255f, 0f, 255f);

            return new Color(R, G, Bc, alpha);
        }

        private static void RgbToOklch(Color c, out float L, out float C, out float H)
        {
            RgbToOklab(c, out L, out float A, out float B);
            C = MathF.Sqrt(A * A + B * B);
            H = MathF.Atan2(B, A);
        }

        private static Color OklchToRgb(float L, float C, float H, byte alpha)
        {
            float A = C * MathF.Cos(H);
            float B = C * MathF.Sin(H);
            return OklabToRgb(L, A, B, alpha);
        }

        /// <summary>
        /// Unified liquid+metal recolor: hue is always a full override
        /// (never a delta, since hue is meaningless at zero chroma), lightness
        /// anchors to the target with the source pixel's own deviation from
        /// Reference preserved, chroma is a ratio (gray-safe), and "liquidness"
        /// (derived purely from chroma, independent of brightness) blends
        /// between full liquid recolor and a faint ambient tint for near-gray
        /// areas like tank metal/outlines.
        /// </summary>
        public static Texture2D Recolor(GraphicsDevice device, Texture2D source,
            Color reference, Color target, float ambientStrength = 0.15f)
        {
            RgbToOklch(reference, out float refL, out float refC, out _);
            RgbToOklch(target, out float tgtL, out float tgtC, out float tgtH);

            if (refC <= 0.0001f)
                throw new ArgumentException("Reference color has ~zero chroma - pick a reference that actually has saturation.");

            var data = new Color[source.Width * source.Height];
            source.GetData(data);

            for (int i = 0; i < data.Length; i++)
            {
                Color px = data[i];
                if (px.A == 0) continue; // leave fully transparent pixels alone

                RgbToOklch(px, out float pL, out float pC, out _);

                float liquidness = Math.Clamp(pC / refC, 0f, 1f);

                float lLiquid = Math.Clamp(tgtL + (pL - refL), 0f, 1f);
                float newL = MathHelper.Lerp(pL, lLiquid, liquidness);

                float cLiquid = MathF.Max(0f, tgtC * (pC / refC));
                float cAmbient = ambientStrength * tgtC;
                float newC = MathHelper.Lerp(cAmbient, cLiquid, liquidness);

                data[i] = OklchToRgb(newL, newC, tgtH, px.A);
            }

            var result = new Texture2D(device, source.Width, source.Height);
            result.SetData(data);
            return result;
        }
    }
    public static class LiquidTextureCache
    {
        // Keyed by (liquid name, texture identifier) so the same source art
        // can be baked separately per liquid without collisions.
        private static readonly Dictionary<(string liquidName, string textureKey), Texture2D> _cache = new();

        public static Texture2D GetOrCreate(string textureKey, Texture2D sourceTexture, LiquidTypeDefinition liquid, float ambientStrength = 0.15f)
        {
            Color reference = LiquidTypeRegistry.Get(LiquidID.Water).RenderColor; // reference color is always water, since that's what the source art is painted for
            
            var cacheKey = (liquid.Name, textureKey);
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            var baked = LiquidRecolorer.Recolor(Main.graphics.GraphicsDevice, sourceTexture,
                reference, liquid.RenderColor, ambientStrength);

            _cache[cacheKey] = baked;
            return baked;
        }

        // Call from a ModSystem's Unload() so GPU textures are disposed
        // cleanly and everything rebakes fresh on the next mod reload.
        public static void Clear()
        {
            foreach (var tex in _cache.Values)
                tex?.Dispose();
            _cache.Clear();
        }
    }
}