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
        Fireblossom
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
            _ => "FireBlossomPlanterBox"
        };

        // The one herb that grows on that box.
        public static HerbStyle Herb(HarvesterBoxKind kind) => kind switch
        {
            HarvesterBoxKind.Daybloom => HerbStyle.Daybloom,
            HarvesterBoxKind.Moonglow => HerbStyle.Moonglow,
            HarvesterBoxKind.Corrupt => HerbStyle.Deathweed,
            HarvesterBoxKind.Crimson => HerbStyle.Deathweed,
            HarvesterBoxKind.Blinkroot => HerbStyle.Blinkroot,
            HarvesterBoxKind.Waterleaf => HerbStyle.Waterleaf,
            HarvesterBoxKind.Shiverthorn => HerbStyle.Shiverthorn,
            _ => HerbStyle.Fireblossom
        };
    }
}