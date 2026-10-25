using Factorraria.Content.Items.Materials;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Machines
{
    public class PlatinumPowerPoleItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 32;
            Item.maxStack = Item.CommonMaxStack;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.consumable = true;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(gold: 1);
            Item.createTile = ModContent.TileType<PlatinumPowerPoleTile>();
        }
    }
}