using Factorraria.Common.Harvester;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox
{
    // One Harvester Box tile per vanilla planter box (see HarvesterBoxTileBase).
    public class DaybloomHarvesterBoxTile : HarvesterBoxTileBase<DaybloomHarvesterBoxTileEntity, DaybloomHarvesterBoxItem>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Daybloom;
    }

    public class MoonglowHarvesterBoxTile : HarvesterBoxTileBase<MoonglowHarvesterBoxTileEntity, MoonglowHarvesterBoxItem>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Moonglow;
    }

    public class CorruptHarvesterBoxTile : HarvesterBoxTileBase<CorruptHarvesterBoxTileEntity, CorruptHarvesterBoxItem>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Corrupt;
    }

    public class CrimsonHarvesterBoxTile : HarvesterBoxTileBase<CrimsonHarvesterBoxTileEntity, CrimsonHarvesterBoxItem>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Crimson;
    }

    public class BlinkrootHarvesterBoxTile : HarvesterBoxTileBase<BlinkrootHarvesterBoxTileEntity, BlinkrootHarvesterBoxItem>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Blinkroot;
    }

    public class WaterleafHarvesterBoxTile : HarvesterBoxTileBase<WaterleafHarvesterBoxTileEntity, WaterleafHarvesterBoxItem>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Waterleaf;
    }

    public class ShiverthornHarvesterBoxTile : HarvesterBoxTileBase<ShiverthornHarvesterBoxTileEntity, ShiverthornHarvesterBoxItem>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Shiverthorn;
    }

    public class FireblossomHarvesterBoxTile : HarvesterBoxTileBase<FireblossomHarvesterBoxTileEntity, FireblossomHarvesterBoxItem>
    {
        protected override HarvesterBoxKind Kind => HarvesterBoxKind.Fireblossom;
    }
}