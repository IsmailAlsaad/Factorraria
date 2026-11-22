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
    public class EmptyPlanterBoxItem : PlanterBoxItemBase<EmptyPlanterBoxTile> { }

    // Nothing grows on it: the tile base registers every plain custom planter box in HarvesterBoxRegistry.NoHerbBoxes.
    public class EmptyPlanterBoxTile : PlanterBoxTileBase<EmptyPlanterBoxItem> { }
}