using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    public class WoodenPowerPoleTileEntity : PowerPoleTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<WoodenPowerPoleTile>();
        public override float PowerDemand => 1f;

        // PLACEHOLDER crossarm tips (two). Replace with the real art coordinates: canvas pixel (cx, cy) -> (cx - 48, cy).
        readonly Vector2[] linkPointOffsets = new[] { new Vector2(-40f, 8f), new Vector2(56f, 8f) };
        public override Vector2[] LinkPointOffsets => linkPointOffsets;
    }
}