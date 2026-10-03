using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Materials
{
    public class SteelBarItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Blue;
        }
    }
}
