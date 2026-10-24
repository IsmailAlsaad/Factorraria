using Factorraria.Content.Tiles.Statues;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Statues
{
    public class MeteorHeadStatueItem : ModItem
    {
        // Until MeteorHeadStatueItem.png exists the item borrows the vanilla Armor Statue icon, so the mod loads without the art.
        string artPath = "Factorraria/Content/Items/Statues/MeteorHeadStatueItem";
        public override string Texture => ModContent.HasAsset(artPath) ? artPath : "Terraria/Images/Item_" + ItemID.ArmorStatue;

        public override void SetDefaults()
        {
            Item.CloneDefaults(ItemID.ArmorStatue);
            Item.createTile = ModContent.TileType<MeteorHeadStatueTile>();
            Item.placeStyle = 0;
        }
    }
}