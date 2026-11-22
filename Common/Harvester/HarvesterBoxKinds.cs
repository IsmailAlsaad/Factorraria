using Terraria.ID;

namespace Factorraria.Common.Harvester
{
    // The herb tile styles (frameX / 18 of the vanilla herb tiles 82, 83 and 84). Verified in WorldGen.CheckAlch (probe 2).
    public enum HerbStyle
    {
        Daybloom = 0,
        Moonglow = 1,
        Blinkroot = 2,
        Deathweed = 3,
        Waterleaf = 4,
        Fireblossom = 5,
        Shiverthorn = 6
    }

    // One Harvester Box per vanilla planter box. Vanilla has two Deathweed boxes (Corrupt and Crimson), so there are eight, not seven.
    // Phase 5 trees (Ismail, 2026-11-17): the Corrupt box always grows corruption trees and the Crimson box always grows crimson trees. No random rule.
    public enum HarvesterBoxKind
    {
        Daybloom,
        Moonglow,
        Corrupt,
        Crimson,
        Blinkroot,
        Waterleaf,
        Shiverthorn,
        Fireblossom,

        // Phase 3 of docs/planter_boxes/ROADMAP.md: our own boxes. They have NO vanilla planter box item (their planter boxes are our own items, see
        // PlanterBoxItemBase) and no herb, so PlanterItemName and Herb answer null for them. Appended at the end on purpose.
        Mushroom,
        Hallowed,
        Stone
    }

    public static class HarvesterBoxKinds
    {
        // The vanilla ItemID NAME of the planter box each kind upgrades. Always looked up by name (ItemID.Search), never by id.
        public static string PlanterItemName(HarvesterBoxKind kind) => kind switch
        {
            HarvesterBoxKind.Daybloom => "DayBloomPlanterBox",
            HarvesterBoxKind.Moonglow => "MoonglowPlanterBox",
            HarvesterBoxKind.Corrupt => "CorruptPlanterBox",
            HarvesterBoxKind.Crimson => "CrimsonPlanterBox",
            HarvesterBoxKind.Blinkroot => "BlinkrootPlanterBox",
            HarvesterBoxKind.Waterleaf => "WaterleafPlanterBox",
            HarvesterBoxKind.Shiverthorn => "ShiverthornPlanterBox",
            HarvesterBoxKind.Fireblossom => "FireBlossomPlanterBox",
            _ => null   // Mushroom, Hallowed, Stone: our own planter box items, not vanilla ones. Callers must check for null.
        };

        // Phase 5: the vanilla soil tile a box stands in for when vanilla runs its tree rules (look, wood, acorns, sapling style). 0 = this box grows no trees yet
        // (Waterleaf would be a palm and Fireblossom an ash tree: both use other tree tiles and other growth code, a later patch). Corrupt always grows
        // corruption trees and Crimson crimson trees (Ismail, 2026-11-17).
        public static int TreeSoil(HarvesterBoxKind kind) => kind switch
        {
            HarvesterBoxKind.Daybloom => TileID.Grass,
            HarvesterBoxKind.Blinkroot => TileID.Grass,
            HarvesterBoxKind.Moonglow => TileID.JungleGrass,
            HarvesterBoxKind.Corrupt => TileID.CorruptGrass,
            HarvesterBoxKind.Crimson => TileID.CrimsonGrass,
            HarvesterBoxKind.Shiverthorn => TileID.SnowBlock,
            HarvesterBoxKind.Fireblossom => 633,   // an ash tree (tile 634): AshTreeGroundTest takes ash grass (633) only
            HarvesterBoxKind.Waterleaf => TileID.Sand,   // a palm tree (tile 323): GrowPalmTree takes sand (53) as its soil
            HarvesterBoxKind.Mushroom => TileID.MushroomGrass,   // Phase 4: giant mushroom trees are tile 5 trees on mushroom grass (70), grown by GrowTree with no sapling (MushroomGrowthRules)
            HarvesterBoxKind.Hallowed => TileID.HallowedGrass,   // Phase 5a: hallowed trees (pearlwood) from acorns, vanilla soil 109
            HarvesterBoxKind.Stone => TileID.Stone,   // Phase 5b: gem trees from gemcorns only (GemTreeGroundTest takes stone and moss, so stone (1) passes)
            _ => 0
        };

        // The sapling style (frameX / 54) vanilla's Check1x2 gives each soil. Verified in probe 4 (Check1x2 switch).
        public static int SaplingStyle(int vanillaSoil) => vanillaSoil switch
        {
            TileID.SnowBlock => 1,
            TileID.JungleGrass => 2,
            TileID.CorruptGrass => 3,
            TileID.CrimsonGrass => 4,
            TileID.HallowedGrass => 5,   // Check1x2 gives hallowed grass (109) and its second kind (492) style 5, which AttemptToGrowTreeFromSapling grows with GrowTree
            633 => 10,   // ash grass: Check1x2 gives style 10, which AttemptToGrowTreeFromSapling grows through TryGrowingTreeByType(634)
            TileID.Sand => 6,   // Check1x2: sand (53) gives style 6, which AttemptToGrowTreeFromSapling grows with GrowPalmTree
            _ => 0
        };

        // Gemcorns (Phase 5b). The seven GemTree<Gem>Seed items are consecutive (Topaz 4851 to Amber 4857, probe 3). Each plants the gem sapling (tile 590) with the style
        // 0 to 6 in that order, and AttemptToGrowTreeFromSapling grows tile 583 + style from it (Topaz 583 ... Amber 589, probe 2). Expression-bodied on purpose.
        public static int GemSaplingTile => 590;
        public static int GemTreeFirstTile => 583;
        public static bool IsGemcorn(int itemType) => itemType >= ItemID.GemTreeTopazSeed && itemType <= ItemID.GemTreeAmberSeed;
        public static int GemcornStyle(int itemType) => itemType - ItemID.GemTreeTopazSeed;
        public static bool IsGemTreeTile(int tileType) => tileType >= GemTreeFirstTile && tileType <= GemTreeFirstTile + 6;

        // The tile whose break dust a planter box of that kind uses (the block or grass its soil is made of). Used for the vanilla planter boxes (PlanterBoxGlobalTile).
        public static int PlanterDustTile(HarvesterBoxKind kind) => kind switch
        {
            HarvesterBoxKind.Daybloom => TileID.Grass,
            HarvesterBoxKind.Moonglow => TileID.JungleGrass,
            HarvesterBoxKind.Corrupt => TileID.CorruptGrass,
            HarvesterBoxKind.Crimson => TileID.CrimsonGrass,
            HarvesterBoxKind.Blinkroot => TileID.Dirt,
            HarvesterBoxKind.Waterleaf => TileID.Sand,
            HarvesterBoxKind.Shiverthorn => TileID.SnowBlock,
            HarvesterBoxKind.Mushroom => TileID.MushroomGrass,
            HarvesterBoxKind.Hallowed => TileID.HallowedGrass,
            HarvesterBoxKind.Stone => TileID.Stone,
            _ => TileID.Ash   // Fireblossom
        };

        // The one herb that grows on that box, or null when the box grows no herb (Mushroom, Hallowed and Stone: HarvesterBoxRegistry.Register
        // puts those tiles in NoHerbBoxes, so planting a herb on them is refused).
        public static HerbStyle? Herb(HarvesterBoxKind kind) => kind switch
        {
            HarvesterBoxKind.Daybloom => HerbStyle.Daybloom,
            HarvesterBoxKind.Moonglow => HerbStyle.Moonglow,
            HarvesterBoxKind.Corrupt => HerbStyle.Deathweed,
            HarvesterBoxKind.Crimson => HerbStyle.Deathweed,
            HarvesterBoxKind.Blinkroot => HerbStyle.Blinkroot,
            HarvesterBoxKind.Waterleaf => HerbStyle.Waterleaf,
            HarvesterBoxKind.Shiverthorn => HerbStyle.Shiverthorn,
            HarvesterBoxKind.Fireblossom => HerbStyle.Fireblossom,
            _ => null
        };
    }
}