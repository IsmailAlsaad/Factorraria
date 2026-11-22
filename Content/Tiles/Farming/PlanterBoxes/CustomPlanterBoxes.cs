using Factorraria.Common.Harvester;
using Terraria.Audio;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Farming.PlanterBoxes
{
    // The three custom planter boxes (docs/planter_boxes/ROADMAP.md, Phase 2). Each is crafted from the Empty Planter Box (PlanterBoxRecipes).
    // Art (Ismail), next to this file: <Name>PlanterBoxItem.png 32x32 and <Name>PlanterBoxTile.png 72x16 (frame layout in PlanterBoxTileBase).
    // What grows on them comes later: Mushroom grows glowing mushrooms and giant mushroom trees (Phases 4 and 5), Hallowed grows pearlwood from acorns and
    // Stone grows the gem trees from gemcorns (Phase 5). None of them grows a herb.
    public class MushroomPlanterBoxItem : PlanterBoxItemBase<MushroomPlanterBoxTile> { }

    public class MushroomPlanterBoxTile : PlanterBoxTileBase<MushroomPlanterBoxItem>
    {
        protected override int DustTile => TileID.MushroomGrass;

        protected override int TreeSoil => TileID.MushroomGrass;   // glowing mushrooms and giant mushroom trees grow on it (MushroomGrowthRules)
    }

    public class HallowedPlanterBoxItem : PlanterBoxItemBase<HallowedPlanterBoxTile> { }

    public class HallowedPlanterBoxTile : PlanterBoxTileBase<HallowedPlanterBoxItem>
    {
        protected override int DustTile => TileID.HallowedGrass;
    }

    public class StonePlanterBoxItem : PlanterBoxItemBase<StonePlanterBoxTile> { }

    public class StonePlanterBoxTile : PlanterBoxTileBase<StonePlanterBoxItem>
    {
        protected override int DustTile => TileID.Stone;

        protected override SoundStyle BreakSound => SoundID.Tink;
    }
}