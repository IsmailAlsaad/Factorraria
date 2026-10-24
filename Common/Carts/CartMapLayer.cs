using Factorraria.Common.Net;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Carts
{
    public enum CartMapMode : byte
    {
        Off = 0,
        All = 1,
        MotorOnly = 2
    }

    /// <summary>The local player's choice of which carts the map shows. Client-only, never sent anywhere, resets to All every launch.</summary>
    public static class CartMapSettings
    {
        public static CartMapMode Mode = CartMapMode.All;

        /// <summary>Off -> All -> Motor only -> Off.</summary>
        public static void Cycle()
        {
            switch (Mode)
            {
                case CartMapMode.Off:
                    Mode = CartMapMode.All;
                    break;
                case CartMapMode.All:
                    Mode = CartMapMode.MotorOnly;
                    break;
                default:
                    Mode = CartMapMode.Off;
                    break;
            }
        }

        public static string Describe(CartMapMode mode)
        {
            switch (mode)
            {
                case CartMapMode.Off:
                    return "no carts";
                case CartMapMode.All:
                    return "all carts";
                default:
                    return "motor carts only";
            }
        }
    }

    /// <summary>
    /// Draws a small icon on the map for every player-placed cart. Which carts exist comes from CartMapNet (server snapshot on a
    /// multiplayer client, the live list otherwise). Carts made by world generation are never listed (Cart.PlacedByPlayer).
    /// The icon is built per skin + module item combination (CartMapIcons), so no art is needed for the many combinations.
    /// </summary>
    public class CartMapLayer : ModMapLayer
    {


        readonly List<CartMapEntry> entries = new List<CartMapEntry>();

        // Drawn under the vanilla icons (spawn, pylons, pings) so a cart never hides them.
        public override Position GetDefaultPosition() => BeforeFirstVanillaLayer;

        public override void Draw(ref MapOverlayDrawContext context, ref string text)
        {
            // The button is drawn from here (inside the map draw), so it always shows wherever the icons do. Fullscreen map only.
            if (Main.mapFullscreen)
                ModContent.GetInstance<CartMapSystem>().DrawButton(ref text);

            CartMapMode mode = CartMapSettings.Mode;
            if (mode == CartMapMode.Off)
                return;

            float scale = CartMapConfig.Current.IconSize;
            CartMapNet.Collect(entries);

            for (int i = 0; i < entries.Count; i++)
            {
                CartMapEntry entry = entries[i];

                if (mode == CartMapMode.MotorOnly && entry.Module != CartModule.Motor)
                    continue;

                Texture2D icon = CartMapIcons.Get(entry.SkinType, entry.ModuleItemType, entry.PassengerNpcType);
                if (icon == null)
                    continue;

                // Minecart and Mechanical Cart item sprites face left, all the others face right: flip those two the other way.
                bool spriteFacesLeft = CartMapConfig.Current.FlipLeftFacingSkins && (entry.SkinType == Terraria.ID.ItemID.Minecart || Cart.IsMechSkin(entry.SkinType));
                bool flip = ((entry.Facing < 0) != CartMapConfig.Current.InvertIconDirection) != spriteFacesLeft;
                MapOverlayDrawContext.DrawResult result = context.Draw(icon, new Vector2(entry.TileX, entry.TileY), Color.White, new SpriteFrame(1, 1, 0, 0), scale, scale * 1.33f, Alignment.Center, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);

                if (result.IsMouseOver)
                {
                    CartMapHoverSystem.HoveredCartId = entry.Id;   // CartMapHoverSystem draws the cart's contents panel for it
                    text = Lang.GetItemNameValue(entry.SkinType);
                    if (entry.ModuleItemType > 0)
                        text += " + " + Lang.GetItemNameValue(entry.ModuleItemType);

                    if (entry.PassengerNpcType > 0)
                        text += " + " + Lang.GetNPCNameValue(entry.PassengerNpcType);
                }
            }
        }
    }
}