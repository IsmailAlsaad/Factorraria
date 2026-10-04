using Factorraria.Common.Carts;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Carts
{
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
            Vector2 railPoint;
            if (!CartSystem.CanPlaceAt(player, Player.tileTargetX, Player.tileTargetY, out railPoint))
            {
                return false;
            }

            CartSystem.SpawnCart(railPoint);
            SoundEngine.PlaySound(SoundID.Dig, railPoint);

            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(RecipeGroupID.IronBar, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}