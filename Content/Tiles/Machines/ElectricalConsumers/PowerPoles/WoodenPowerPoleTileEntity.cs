using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    public class WoodenPowerPoleTileEntity : PowerPoleTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<WoodenPowerPoleTile>();
        public override float PowerDemand => 15f;

        // One board on tile row 12 (from the bottom) of the 14-tall pole. Its sheet is 480x144 (5 columns x 4 rows of 96x36 cells; the cell is the old
        // 96x34 board plus 2 transparent pixels on top, so every link point is 2 px lower in the cell than before). Link points are pixels inside one
        // cell, from its top-left: left tip (9, 6), right tip (86, 12).
        readonly PoleBoard[] boards = new[]
        {
            new PoleBoard("Factorraria/Content/Tiles/Machines/ElectricalConsumers/PowerPoles/WoodenPowerPole_Crossboard", new Vector2(96f, 36f), 2f, 4, 14, 12, new[] { new Vector2(9f, 6f), new Vector2(86f, 12f) })
        };
        public override PoleBoard[] Boards => boards;
    }
}