using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // The item of a plain custom planter box (Empty, Mushroom, Hallowed, Stone). Art (Ismail), next to the item class: <Name>Item.png, 32x32.
    // Until it exists the item borrows the icon of the vanilla planter box, so the mod loads without the art.
    public abstract class PlanterBoxItemBase<TTile> : ModItem where TTile : ModTile
    {
        public override string Texture
        {
            get
            {
                if (ModContent.HasAsset(base.Texture))
                    return base.Texture;

                return ItemID.Search.TryGetId("PlanterBox", out int planterBox) ? "Terraria/Images/Item_" + planterBox : "Terraria/Images/Item_" + ItemID.Wood;
            }
        }

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

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
            Item.value = Item.buyPrice(silver: 5);   // the Empty box is what the Dryad charges; the others use the same value
            Item.createTile = ModContent.TileType<TTile>();
        }
    }
}