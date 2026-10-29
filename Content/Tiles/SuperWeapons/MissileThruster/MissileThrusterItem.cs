using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileThruster
{
    // Super weapons patch S2.3: places one missile thruster (facing follows the way the player faces). A weapon needs three. The recipe is a
    // placeholder: the real parts come from automation, designed in patch S5.
    public class MissileThrusterItem : ModItem
    {
        // Until MissileThrusterItem.png exists the item borrows a vanilla icon, so the mod loads without the art.
        string artPath = "Factorraria/Content/Tiles/SuperWeapons/MissileThruster/MissileThrusterItem";
        public override string Texture => ModContent.HasAsset(artPath) ? artPath : "Terraria/Images/Item_" + ItemID.Cog;

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = Item.CommonMaxStack;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.consumable = true;
            Item.value = Item.buyPrice(silver: 50);
            Item.createTile = ModContent.TileType<MissileThrusterTile>();
            Item.placeStyle = 0;
        }
    }
}