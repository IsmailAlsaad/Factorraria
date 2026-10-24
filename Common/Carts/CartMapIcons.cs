using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Factorraria.Content.Configs;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Builds the map icon for a (cart skin, module item) pair: the module's item sprite with the skin's item sprite drawn over it (the module sits behind the cart), then a
    /// white ring and a black ring around the silhouette (the look of the vanilla NPC head icons). Built once on first use and cached.
    /// Client / main thread only (it reads item textures and makes a Texture2D); the map layer is the only caller.
    /// If a texture cannot be read the plain skin sprite is used instead, so the map never breaks.
    /// </summary>
    public static class CartMapIcons
    {
        public static int ModuleScalePercent = 60;   // size of the module sprite relative to its item sprite
        public static int ModuleBottomPercent = 45;  // how far down the cart sprite the module's bottom edge sits (LOWER = module sits HIGHER; was 60)
        // The outline is CartMapConfig.OutlineThickness (a float) pixels of white, then the same of black; the canvas is padded by twice that, rounded up.

        static readonly Dictionary<long, Texture2D> cache = new Dictionary<long, Texture2D>();
        static readonly List<Texture2D> owned = new List<Texture2D>();

        public static Texture2D Get(int skinType, int moduleItemType)
        {
            if (moduleItemType < 0)
                moduleItemType = 0;

            long key = ((long)skinType << 32) | (uint)moduleItemType;
            if (cache.TryGetValue(key, out Texture2D icon))
                return icon;

            icon = Build(skinType, moduleItemType);
            cache[key] = icon;
            return icon;
        }

        /// <summary>Forget every built icon (the config changed); they are rebuilt on the next map draw.</summary>
        public static void Rebuild()
        {
            Unload();
        }

        public static void Unload()
        {
            List<Texture2D> toDispose = new List<Texture2D>(owned);
            cache.Clear();
            owned.Clear();

            if (toDispose.Count == 0)
                return;

            Main.QueueMainThreadAction(() =>
            {
                for (int i = 0; i < toDispose.Count; i++)
                    toDispose[i].Dispose();
            });
        }

        static Texture2D Build(int skinType, int moduleItemType)
        {
            Main.instance.LoadItem(skinType);
            Texture2D body = TextureAssets.Item[skinType].Value;

            try
            {
                Texture2D module = null;
                if (moduleItemType > 0)
                {
                    Main.instance.LoadItem(moduleItemType);
                    module = TextureAssets.Item[moduleItemType].Value;
                }

                int bw = body.Width;
                int bh = body.Height;
                Color[] bodyPixels = new Color[bw * bh];
                body.GetData(bodyPixels);

                Color[] modulePixels = null;
                int mw = 0;
                int mh = 0;
                if (module != null)
                {
                    Color[] source = new Color[module.Width * module.Height];
                    module.GetData(source);

                    // nearest-neighbour shrink, keeps the pixel art crisp
                    mw = Math.Max(1, module.Width * ModuleScalePercent / 100);
                    mh = Math.Max(1, module.Height * ModuleScalePercent / 100);
                    modulePixels = new Color[mw * mh];
                    for (int y = 0; y < mh; y++)
                    {
                        for (int x = 0; x < mw; x++)
                        {
                            int sx = Math.Min(module.Width - 1, x * 100 / ModuleScalePercent);
                            int sy = Math.Min(module.Height - 1, y * 100 / ModuleScalePercent);
                            modulePixels[y * mw + x] = source[sy * module.Width + sx];
                        }
                    }
                }

                // The module may stick out above the cart body, so the canvas grows upwards (and sideways) when it has to.
                int moduleBottom = bh * ModuleBottomPercent / 100;
                int topExtra = modulePixels != null ? Math.Max(0, mh - moduleBottom) : 0;
                int innerW = Math.Max(bw, mw);
                float thickness = Math.Max(0f, CartMapConfig.Current.OutlineThickness);
                int pad = (int)Math.Ceiling(thickness * 2f);
                int w = innerW + pad * 2;
                int h = bh + topExtra + pad * 2;

                // Module first, then the cart body on top: the chest / terrarium sits BEHIND the cart.
                Color[] canvas = new Color[w * h];
                if (modulePixels != null)
                    Blit(canvas, w, modulePixels, mw, mh, pad + (innerW - mw) / 2, pad + topExtra + moduleBottom - mh);

                Blit(canvas, w, bodyPixels, bw, bh, pad + (innerW - bw) / 2, pad + topExtra);

                bool[] solid = new bool[w * h];
                for (int i = 0; i < solid.Length; i++)
                    solid[i] = canvas[i].A > 0;

                // ringOf[i] = how many 8-neighbour steps pixel i is from the silhouette (0 = on it, int.MaxValue = out of range).
                // A fractional thickness makes the outermost ring of each colour partly transparent.
                int maxRing = (int)Math.Ceiling(thickness * 2f);
                int[] ringOf = new int[w * h];
                for (int i = 0; i < ringOf.Length; i++)
                    ringOf[i] = solid[i] ? 0 : int.MaxValue;

                bool[] reached = solid;
                for (int ring = 1; ring <= maxRing; ring++)
                {
                    bool[] grown = Dilate(reached, w, h);
                    for (int i = 0; i < grown.Length; i++)
                    {
                        if (grown[i] && !reached[i])
                            ringOf[i] = ring;
                    }

                    reached = grown;
                }

                Color[] result = new Color[w * h];
                for (int i = 0; i < result.Length; i++)
                {
                    int ring = ringOf[i];
                    if (thickness > 0f && ring > 0 && ring != int.MaxValue)
                    {
                        float white = Math.Clamp(thickness - (ring - 1), 0f, 1f);
                        float dark = Math.Clamp(thickness * 2f - (ring - 1), 0f, 1f);
                        if (dark > 0f)
                        {
                            // premultiplied: white over black
                            int value = (int)Math.Round(255f * white);
                            int alpha = (int)Math.Round(255f * (white + dark * (1f - white)));
                            result[i] = new Color(value, value, value, alpha);
                        }
                    }

                    Over(result, i, canvas[i]);
                }

                Texture2D icon = new Texture2D(Main.instance.GraphicsDevice, w, h);
                icon.SetData(result);
                owned.Add(icon);
                return icon;
            }
            catch (Exception exception)
            {
                ModContent.GetInstance<Factorraria>().Logger.Warn("[CartMap] could not build the icon for skin " + skinType + " module item " + moduleItemType + ": " + exception.Message);
                return body;
            }
        }

        static void Blit(Color[] destination, int destinationWidth, Color[] source, int sourceWidth, int sourceHeight, int offsetX, int offsetY)
        {
            for (int y = 0; y < sourceHeight; y++)
            {
                for (int x = 0; x < sourceWidth; x++)
                {
                    int dx = x + offsetX;
                    int dy = y + offsetY;
                    if (dx < 0 || dy < 0 || dx >= destinationWidth)
                        continue;

                    int index = dy * destinationWidth + dx;
                    if (index >= destination.Length)
                        continue;

                    Over(destination, index, source[y * sourceWidth + x]);
                }
            }
        }

        // Premultiplied "source over destination" (Terraria textures are premultiplied).
        static void Over(Color[] destination, int index, Color source)
        {
            if (source.A == 0)
                return;

            if (source.A == 255)
            {
                destination[index] = source;
                return;
            }

            Color under = destination[index];
            int inverse = 255 - source.A;
            destination[index] = new Color(source.R + under.R * inverse / 255, source.G + under.G * inverse / 255, source.B + under.B * inverse / 255, source.A + under.A * inverse / 255);
        }

        // A pixel is set if it or any of its 8 neighbours is set.
        static bool[] Dilate(bool[] mask, int w, int h)
        {
            bool[] grown = new bool[mask.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool set = false;
                    for (int dy = -1; dy <= 1 && !set; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < w && ny < h && mask[ny * w + nx])
                            {
                                set = true;
                                break;
                            }
                        }
                    }

                    grown[y * w + x] = set;
                }
            }

            return grown;
        }
    }
}