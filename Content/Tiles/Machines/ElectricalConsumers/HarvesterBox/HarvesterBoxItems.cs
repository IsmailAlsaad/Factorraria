using Factorraria.Common.Harvester;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox
{
    // One item per Harvester Box (see HarvesterBoxItemBase).
    public class DaybloomHarvesterBoxItem : HarvesterBoxItemBase<DaybloomHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Daybloom;
    }

    public class MoonglowHarvesterBoxItem : HarvesterBoxItemBase<MoonglowHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Moonglow;
    }

    public class CorruptHarvesterBoxItem : HarvesterBoxItemBase<CorruptHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Corrupt;
    }

    public class CrimsonHarvesterBoxItem : HarvesterBoxItemBase<CrimsonHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Crimson;
    }

    public class BlinkrootHarvesterBoxItem : HarvesterBoxItemBase<BlinkrootHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Blinkroot;
    }

    public class WaterleafHarvesterBoxItem : HarvesterBoxItemBase<WaterleafHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Waterleaf;
    }

    public class ShiverthornHarvesterBoxItem : HarvesterBoxItemBase<ShiverthornHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Shiverthorn;
    }

    public class FireblossomHarvesterBoxItem : HarvesterBoxItemBase<FireblossomHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Fireblossom;
    }

    public class MushroomHarvesterBoxItem : HarvesterBoxItemBase<MushroomHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Mushroom;
    }

    public class HallowedHarvesterBoxItem : HarvesterBoxItemBase<HallowedHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Hallowed;
    }

    public class StoneHarvesterBoxItem : HarvesterBoxItemBase<StoneHarvesterBoxTile>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Stone;
    }
}