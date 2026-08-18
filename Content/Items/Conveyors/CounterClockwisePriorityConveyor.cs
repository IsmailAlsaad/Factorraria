using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Conveyors
{
    public class ClockwisePriorityConveyor : ModItem
    {
        public override void SetDefaults()
        {
            // Item inventory hitboxes
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = Item.CommonMaxStack;

            // Placement animation settings
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;

            // Economy and rarity
            Item.value = Item.buyPrice(copper: 50);
            Item.rare = ItemRarityID.White;

            // Binds this item to place your custom tile
            Item.createTile = ModContent.TileType<Tiles.Conveyors.ClockwisePriorityConveyorTile>();
        }
    }
}