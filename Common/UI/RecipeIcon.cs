using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.UI
{
    /// <summary>
    /// Shared product-icon drawing for recipe UIs (machine browser rows, recipe book entries, later the detail page).
    /// Items use their item texture; liquids use their registry icon PNG or a colour swatch.
    /// faded = black silhouette tint (recipe known but not crafted yet).
    /// </summary>
    public static class RecipeIcon
    {
        public static readonly Color FadedTint = Color.Black * 0.7f;

        static readonly Dictionary<string, Asset<Texture2D>> liquidIcons = new();

        public static void Clear() => liquidIcons.Clear();

        /// <param name="iconBox">Unzoomed box the icon is fitted into (never upscaled past 1).</param>
        /// <param name="zoom">Extra scale applied on top (machine browser zoom). Use 1 elsewhere.</param>
        public static void Draw(SpriteBatch sb, RecipeOutputKey key, Vector2 center, float iconBox, float zoom = 1f, bool faded = false)
        {
            Color tint = faded ? FadedTint : Color.White;

            if (key.IsLiquid)
            {
                LiquidTypeDefinition liquid = LiquidTypeRegistry.Get(key.Id);
                if (liquid == null) return;

                if (liquid.IconPath != null)
                {
                    if (!liquidIcons.TryGetValue(liquid.IconPath, out Asset<Texture2D> asset))
                        liquidIcons[liquid.IconPath] = asset = ModContent.Request<Texture2D>(liquid.IconPath);
                    DrawTexture(sb, asset.Value, center, iconBox, zoom, tint);
                }
                else
                {
                    int s = (int)(iconBox * 0.6f * zoom);
                    sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)center.X - s / 2, (int)center.Y - s / 2, s, s),
                        faded ? FadedTint : liquid.RenderColor);
                }
                return;
            }

            Main.instance.LoadItem(key.Id);
            Texture2D itemTex = TextureAssets.Item[key.Id].Value;

            // Animated item sprites (Fallen Star etc.) are vertical sheets: draw only the current frame,
            // the same one vanilla inventory slots show. Client-only drawing, nothing to sync.
            Rectangle? itemFrame = null;
            if (itemTex != null && Main.itemAnimations[key.Id] != null)
                itemFrame = Main.itemAnimations[key.Id].GetFrame(itemTex);

            DrawTexture(sb, itemTex, center, iconBox, zoom, tint, itemFrame);
        }

        /// <param name="frame">Source rectangle inside the texture (animated sheets). Null = whole texture.</param>
        static void DrawTexture(SpriteBatch sb, Texture2D tex, Vector2 center, float iconBox, float zoom, Color tint, Rectangle? frame = null)
        {
            if (tex == null) return;
            Rectangle src = frame ?? tex.Bounds;
            float fit = Math.Min(1f, iconBox / Math.Max(src.Width, src.Height));
            sb.Draw(tex, center, src, tint, 0f, new Vector2(src.Width, src.Height) / 2f, fit * zoom, SpriteEffects.None, 0f);
        }
    }
}