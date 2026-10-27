using Factorraria.Content.Tiles.Machines.ElectricalProducers.BeeWheel;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Machines
{
    // No crafting recipe yet (added by hand later). Icon: Content/Items/Machines/BeeWheelItem.png
    public class BeeWheelItem : ModItem
    {
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
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 2);
            Item.createTile = ModContent.TileType<BeeWheelTile>();
        }
    }
}