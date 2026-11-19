using Factorraria.Common.Harvester;
using Terraria.Enums;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox
{
    // One tree per box that grows trees (Phase 5a). Waterleaf (palm) and Fireblossom (ash) have none yet. Everything here is a tunable: wood, shake loot,
    // sapling row and the vanilla top/branch sheet. The TopStyle numbers and frame sizes are from memory (roadmap 6g): if a top looks wrong, change them here.
    public class DaybloomHarvesterBoxTree : HarvesterBoxTreeBase<DaybloomHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Daybloom;
        protected override TreeTypes TreeKind => TreeTypes.Forest;
        protected override int WoodItem => ItemID.Wood;
        protected override int SaplingRow => 0;
        protected override int TopStyle => 0;
    }

    public class BlinkrootHarvesterBoxTree : HarvesterBoxTreeBase<BlinkrootHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Blinkroot;
        protected override TreeTypes TreeKind => TreeTypes.Forest;
        protected override int WoodItem => ItemID.Wood;
        protected override int SaplingRow => 0;
        protected override int TopStyle => 0;
    }

    public class MoonglowHarvesterBoxTree : HarvesterBoxTreeBase<MoonglowHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Moonglow;
        protected override TreeTypes TreeKind => TreeTypes.Jungle;
        protected override int WoodItem => ItemID.RichMahogany;
        protected override int SaplingRow => 2;
        protected override int TopStyle => 2;
        protected override int TopFrameWidth => 114;
        protected override int TopFrameHeight => 96;
    }

    public class CorruptHarvesterBoxTree : HarvesterBoxTreeBase<CorruptHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Corrupt;
        protected override TreeTypes TreeKind => TreeTypes.Corrupt;
        protected override int WoodItem => ItemID.Ebonwood;
        protected override int SaplingRow => 3;
        protected override int TopStyle => 1;
    }

    public class CrimsonHarvesterBoxTree : HarvesterBoxTreeBase<CrimsonHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Crimson;
        protected override TreeTypes TreeKind => TreeTypes.Crimson;
        protected override int WoodItem => ItemID.Shadewood;
        protected override int SaplingRow => 4;
        protected override int TopStyle => 5;
    }

    public class ShiverthornHarvesterBoxTree : HarvesterBoxTreeBase<ShiverthornHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Shiverthorn;
        protected override TreeTypes TreeKind => TreeTypes.Snow;
        protected override int WoodItem => ItemID.BorealWood;
        protected override int SaplingRow => 1;
        protected override int TopStyle => 4;
        protected override int TopFrameWidth => 118;
        protected override int TopFrameHeight => 96;
    }
}