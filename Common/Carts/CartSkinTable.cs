using Factorraria.Content.Items.Carts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Maps a minecart item (the skin) to how a placed cart looks. A skin is any item whose mount is a minecart
    /// (MountID.Sets.Cart), so every vanilla cart works and so do carts from other mods.
    /// PLACEHOLDER ART: every skin still draws its item icon scaled up. Phase 4 replaces GetPlacedTexture with the
    /// real mount textures and speed-driven frames; nothing else needs to change.
    /// </summary>
    public static class CartSkinTable
    {
        public const float PlaceholderScale = 1.05f;

        /// <summary>The item the player is "holding" for cart interactions: the item on the cursor if there is one, else the selected hotbar item.</summary>
        public static Item GetHeldItem(Player player)
        {
            if (Main.mouseItem != null && !Main.mouseItem.IsAir)
            {
                return Main.mouseItem;
            }

            return player.HeldItem;
        }

        /// <summary>True for vanilla/modded minecart items. Using one places a cart instead of mounting.</summary>
        public static bool IsSkinItem(Item item)
        {
            return item != null && !item.IsAir && item.mountType > 0 && MountID.Sets.Cart[item.mountType];
        }

        /// <summary>True for anything that places a cart: skin items and the legacy Track Cart item.</summary>
        public static bool IsPlaceable(Item item)
        {
            return item != null && !item.IsAir && (IsSkinItem(item) || item.type == ModContent.ItemType<CartItem>());
        }

        /// <summary>Item type whose skin a placeable item would produce.</summary>
        public static int SkinTypeFor(Item item)
        {
            return item.type == ModContent.ItemType<CartItem>() ? ItemID.Minecart : item.type;
        }

        public static Texture2D GetPlacedTexture(int skinItemType, out Vector2 origin, out float scale)
        {
            Main.instance.LoadItem(skinItemType);
            Texture2D texture = TextureAssets.Item[skinItemType].Value;
            origin = new Vector2(texture.Width / 2f, texture.Height - 2f);
            scale = PlaceholderScale;
            return texture;
        }
    }
}