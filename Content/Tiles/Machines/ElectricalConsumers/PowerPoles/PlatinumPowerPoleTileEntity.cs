using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    public class PlatinumPowerPoleTileEntity : PowerPoleTileEntityBase
    {
        public override int ValidTileType => ModContent.TileType<PlatinumPowerPoleTile>();
        public override float PowerDemand => 5f;
        public override float SwayScale => 0.5f;   // half the wooden pole's sway

        // Two boards on the 16-tall pole: upper on tile row 14, lower on row 12 (counted from the bottom). Link point order is upper board
        // (left, right) then lower board (left, right). Each board has its own sheet of 5 columns x 4 rows of equal cells (upper 480x120 = 96x30
        // cells, lower 420x120 = 84x30 cells; each cell is the old 28 px tall board plus 2 transparent pixels on top, so every link point is 2 px
        // lower in the cell than before). Link points are pixels inside one cell, from its top-left.
        readonly PoleBoard[] boards = new[]
        {
            new PoleBoard("Factorraria/Content/Tiles/Machines/ElectricalConsumers/PowerPoles/PlatinumPowerPole_CrossboardUpper", new Vector2(96f, 30f), 2f, 4, 16, 14, new[] { new Vector2(9f, 6f), new Vector2(87f, 6f) }),
            new PoleBoard("Factorraria/Content/Tiles/Machines/ElectricalConsumers/PowerPoles/PlatinumPowerPole_CrossboardLower", new Vector2(84f, 30f), 2f, 4, 16, 12, new[] { new Vector2(9f, 6f), new Vector2(74f, 6f) })
        };
        public override PoleBoard[] Boards => boards;
    }
}