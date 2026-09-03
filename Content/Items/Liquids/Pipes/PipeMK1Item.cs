using Factorraria.Content.Tiles.Liquids.Pipes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Liquids.Pipes
{
    internal class PipeMK1Item : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 12; 
            Item.height = 12;
            Item.maxStack = 9999;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<PipeMK1Tile>();
        }
    }
}