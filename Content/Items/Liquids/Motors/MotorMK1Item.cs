using Factorraria.Content.Tiles.Liquids.Motors;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Liquids.Motors
{
    internal class MotorMK1Item : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 16; 
            Item.height = 16;
            Item.maxStack = 9999;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<MotorMK1Tile>();
        }
    }
}
