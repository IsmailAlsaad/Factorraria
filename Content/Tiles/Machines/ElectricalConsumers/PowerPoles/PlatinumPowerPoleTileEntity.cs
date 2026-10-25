using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    public class PlatinumPowerPoleTileEntity : PowerPoleTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<PlatinumPowerPoleTile>();
        public override float PowerDemand => 2f;

        // PLACEHOLDER crossarm tips (four, two per crossarm). Replace with the real art coordinates: canvas pixel (cx, cy) -> (cx - 48, cy).
        readonly Vector2[] linkPointOffsets = new[]
        {
            new Vector2(-40f, 8f), new Vector2(56f, 8f),
            new Vector2(-40f, 40f), new Vector2(56f, 40f)
        };
        public override Vector2[] LinkPointOffsets => linkPointOffsets;
    }
}