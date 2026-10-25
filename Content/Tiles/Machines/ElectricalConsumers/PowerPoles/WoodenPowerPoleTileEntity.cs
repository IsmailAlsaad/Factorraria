using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    public class WoodenPowerPoleTileEntity : PowerPoleTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<WoodenPowerPoleTile>();
        public override float PowerDemand => 15f;

        // One board on tile row 12 (from the bottom) of the 14-tall pole. Size and link points are the real art values (pixels inside the board PNG,
        // from its top-left): left tip (9, 4), right tip (86, 10).
        readonly PoleBoard[] boards = new[]
        {
            new PoleBoard("Factorraria/Content/Tiles/Machines/ElectricalConsumers/PowerPoles/WoodenPowerPole_Crossboard", new Vector2(96f, 34f), 14, 12, new[] { new Vector2(9f, 4f), new Vector2(86f, 10f) })
        };
        public override PoleBoard[] Boards => boards;
    }
}