using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // The item of a Harvester Box. Art (Ismail), next to the item class: <Kind>HarvesterBoxItem.png, 32x32.
    // Until it exists the item borrows the icon of the vanilla planter box it upgrades, so the mod loads without the art.
    public abstract class HarvesterBoxItemBase<TTile> : ModItem where TTile : ModTile
    {
        protected abstract HarvesterBoxKind Kind { get; }

        public override string Texture
        {
            get
            {
                if (ModContent.HasAsset(base.Texture))
                    return base.Texture;

                string planterName = HarvesterBoxKinds.PlanterItemName(Kind);   // null for our own kinds (Mushroom, Hallowed, Stone)
                return planterName != null && ItemID.Search.TryGetId(planterName, out int planterBox) ? "Terraria/Images/Item_" + planterBox : "Terraria/Images/Item_" + ItemID.Wood;
            }
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
            Item.value = Item.buyPrice(silver: 50);
            Item.createTile = ModContent.TileType<TTile>();
        }
    }
}