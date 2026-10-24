using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// The little button on the fullscreen map that cycles which carts are shown (none -> all -> motor only). Client-only, local choice.
    /// Art (Ismail makes it): Content/UI/CartMap/CartMapButton_Off.png, CartMapButton_All.png, CartMapButton_Motor.png, 32x32 each.
    /// Until the PNGs exist it draws a plain box with a text label, so the feature works without them.
    /// </summary>
    public class CartMapSystem : ModSystem
    {
        // The position is in the mod config (CartMapConfig.ButtonX / ButtonY); the size is the size of the art.
        public int ButtonSize = 32;

        readonly Asset<Texture2D>[] icons = new Asset<Texture2D>[3];
        bool iconsRequested;

        public override void Unload()
        {
            CartMapIcons.Unload();
        }



        void RequestIcons()
        {
            if (iconsRequested)
                return;

            iconsRequested = true;
            string[] names = { "CartMapButton_Off", "CartMapButton_All", "CartMapButton_Motor" };

            for (int i = 0; i < names.Length; i++)
            {
                // RequestIfExists: a missing PNG only means the text fallback is drawn, it never crashes.
                if (ModContent.RequestIfExists<Texture2D>("Factorraria/Content/UI/CartMap/" + names[i], out Asset<Texture2D> asset, AssetRequestMode.ImmediateLoad))
                    icons[i] = asset;
            }
        }

        public void DrawButton(ref string text)
        {
            if (!Main.mapFullscreen || Main.LocalPlayer == null)
                return;

            RequestIcons();

            CartMapMode mode = CartMapSettings.Mode;
            CartMapConfig config = CartMapConfig.Current;
            Rectangle area = new Rectangle(config.ButtonX, config.ButtonY, ButtonSize, ButtonSize);
            bool hover = area.Contains(Main.mouseX, Main.mouseY);

            Asset<Texture2D> icon = icons[(int)mode];
            if (icon != null && icon.IsLoaded)
            {
                Main.spriteBatch.Draw(icon.Value, area, hover ? Color.White : new Color(220, 220, 220));
            }
            else
            {
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, new Rectangle(0, 0, 1, 1), hover ? new Color(90, 90, 140) * 0.9f : new Color(40, 40, 90) * 0.85f);
                string label = mode == CartMapMode.Off ? "Off" : mode == CartMapMode.All ? "All" : "Mot";
                Utils.DrawBorderString(Main.spriteBatch, label, area.Center.ToVector2() - new Vector2(9f, 8f), Color.White, 0.8f);
            }

            if (!hover)
                return;

            Main.LocalPlayer.mouseInterface = true;
            text = "Show " + CartMapSettings.Describe(mode);

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease = false;
                CartMapSettings.Cycle();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }
    }
}