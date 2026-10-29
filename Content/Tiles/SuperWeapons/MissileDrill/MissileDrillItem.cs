using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileDrill
{
    // Super weapons patch S2.1: places the missile drill head (facing follows the way the player faces). The recipe is a placeholder: the real
    // parts come from automation, designed in patch S5.
    public class MissileDrillItem : ModItem
    {
        // Until MissileDrillItem.png exists the item borrows a vanilla drill icon, so the mod loads without the art.
        string artPath = "Factorraria/Content/Tiles/SuperWeapons/MissileDrill/MissileDrillItem";
        public override string Texture => ModContent.HasAsset(artPath) ? artPath : "Terraria/Images/Item_" + ItemID.CobaltDrill;

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = Item.CommonMaxStack;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.consumable = true;
            Item.value = Item.buyPrice(gold: 1);
            Item.createTile = ModContent.TileType<MissileDrillTile>();
            Item.placeStyle = 0;
        }
    }
}