using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Carts
{
    /// <summary>
    /// The skin for vanilla's default cart mount (mount 13), which vanilla has no item for. Like every cart skin it is told apart by its
    /// mountType, so CartSkinTable, CartRide, the chain art and the save key (Factorraria/WoodenMinecartItem) all treat it as a normal skin
    /// with no table entry. VanillaCartItemGlobal blocks the vanilla use and CartSystem places it on a track. Equipping it in the minecart
    /// slot changes nothing: a player with no cart equipped already rides mount 13.
    /// Art: Content/Items/Carts/WoodenMinecartItem.png
    /// </summary>
    public class WoodenMinecartItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 24;
            Item.maxStack = 99;
            Item.value = Item.sellPrice(silver: 5);
            Item.rare = ItemRarityID.White;

            Item.mountType = 13;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
        }

        // Using the item must never mount the player (vanilla mounts on use for any item with a mountType). The item blocks it itself
        // instead of relying only on VanillaCartItemGlobal. Placing on a track is CartSystem's click handling and does not need this hook.
        public override bool CanUseItem(Player player)
        {
            return false;
        }
    }
}