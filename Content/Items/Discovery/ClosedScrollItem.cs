using Factorraria.Common.Knowledge;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Discovery
{
    /// <summary>
    /// Found in chests. Right click: the seal breaks and the scroll turns into a Recipe Parchment
    /// with a recipe rolled right now. If the world has nothing left to unlock, the scroll is kept.
    /// </summary>
    public class ClosedScrollItem : ModItem
    {
        // Placeholder art: borrow the vanilla Book icon until we have our own sprite.
        // When ClosedScrollItem.png exists next to this file, delete this override.
        //public override string Texture => "Terraria/Images/Item_" + ItemID.Book;

        bool keepOnThisClick;

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.maxStack = 20;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(silver: 50);
        }

        public override bool CanRightClick() => true;

        public override void RightClick(Player player)
        {
            if (!RecipeCatalog.TryRollLocked(Main.rand, out string key))
            {
                keepOnThisClick = true;
                Main.NewText("This scroll has nothing left to reveal.", 200, 200, 200);
                return;
            }

            Item parchment = new Item();
            parchment.SetDefaults(ModContent.ItemType<RecipeParchmentItem>());
            ((RecipeParchmentItem)parchment.ModItem).CatalogKey = key;
            player.QuickSpawnItem(player.GetSource_OpenItem(Type), parchment);
        }

        // RightClick consumes one by default; returning false keeps the scroll when the roll failed.
        public override bool ConsumeItem(Player player)
        {
            bool consume = !keepOnThisClick;
            keepOnThisClick = false;
            return consume;
        }
    }
}