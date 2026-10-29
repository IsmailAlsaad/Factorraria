using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileTank
{
    // Super weapons patch S2.2: places the missile fuel tank. The recipe is a placeholder: the real parts come from automation, designed in patch S5.
    public class MissileTankItem : ModItem
    {
        // Until MissileTankItem.png exists the item borrows a vanilla icon, so the mod loads without the art.
        string artPath = "Factorraria/Content/Tiles/SuperWeapons/MissileTank/MissileTankItem";
        public override string Texture => ModContent.HasAsset(artPath) ? artPath : "Terraria/Images/Item_" + ItemID.Barrel;

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
            Item.createTile = ModContent.TileType<MissileTankTile>();
        }
    }
}