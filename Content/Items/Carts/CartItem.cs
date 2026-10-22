using Factorraria.Common.Carts;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Carts
{
    /// <summary>
    /// LEGACY. Carts are now placed by using any vanilla minecart item. This item stays loadable so existing inventories
    /// and chests do not lose it, and it still places a plain Terraria/Minecart cart, but it is no longer craftable.
    /// </summary>
    public class CartItem : ModItem
    {
        // Placeholder art: borrow the vanilla Minecart icon until we have our own sprite.
        public override string Texture
        {
            get { return "Terraria/Images/Item_" + ItemID.Minecart; }
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 24;
            Item.maxStack = 99;
            Item.value = Item.sellPrice(silver: 5);

            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
        }

        public override bool CanUseItem(Player player)
        {
            Vector2 railPoint;
            return CartSystem.CanPlaceAt(player, Player.tileTargetX, Player.tileTargetY, out railPoint);
        }

        public override bool? UseItem(Player player)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // The server spawns the cart and the item is consumed when it answers "accepted" (patch 9.3), so the use itself consumes nothing.
                if (player.whoAmI == Main.myPlayer)
                {
                    global::Factorraria.Common.Net.CartInputNet.RequestPlace(player, CartSkinTable.GetHeldItem(player));
                }

                return false;
            }

            Vector2 railPoint;
            if (!CartSystem.CanPlaceAt(player, Player.tileTargetX, Player.tileTargetY, out railPoint))
            {
                return false;
            }

            CartSystem.SpawnCart(railPoint);
            SoundEngine.PlaySound(SoundID.Dig, railPoint);

            return true;
        }

        // No recipe: legacy item (see class comment).
    }
}