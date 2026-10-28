using Factorraria.Content.Tiles.Machines.ElectricalProducers.SunCapsule;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Machines
{
    public class SunCapsuleItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 32;
            Item.maxStack = Item.CommonMaxStack;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.consumable = true;
            Item.value = Item.buyPrice(gold: 2);
            Item.createTile = ModContent.TileType<SunCapsuleTile>();
        }
    }
}