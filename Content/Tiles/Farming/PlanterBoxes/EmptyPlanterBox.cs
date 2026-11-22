using Factorraria.Common.Harvester;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Farming.PlanterBoxes
{
    // The empty planter box: the only planter box the Dryad sells. It is placed like any planter box, but nothing can be planted on it. Every other planter box
    // is crafted from it at a work bench (Common/Harvester/PlanterBoxRecipes.cs).
    // Art (Ismail), next to this file: EmptyPlanterBoxItem.png 32x32 and EmptyPlanterBoxTile.png 72x16 (frame layout in PlanterBoxTileBase).
    // Until they exist the item borrows the vanilla planter box icon and the tile the vanilla planter box sheet.
    public class EmptyPlanterBoxItem : ModItem
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
            Item.value = Item.buyPrice(silver: 5);   // what the Dryad charges
            Item.createTile = ModContent.TileType<EmptyPlanterBoxTile>();
        }
    }

    public class EmptyPlanterBoxTile : PlanterBoxTileBase<EmptyPlanterBoxItem>
    {
        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            // Nothing grows on it: HarvesterBoxRules.HerbAllowedOnSoil answers false for every herb (planting is refused, a herb that is somehow there breaks).
            HarvesterBoxRegistry.NoHerbBoxes.Add(Type);
        }
    }
}