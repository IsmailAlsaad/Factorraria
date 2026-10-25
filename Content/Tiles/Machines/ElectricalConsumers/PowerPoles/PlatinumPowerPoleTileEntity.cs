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
        // (left, right) then lower board (left, right). Sizes and link points are the real art values (pixels inside each board's
        // PNG, from its top-left).
        readonly PoleBoard[] boards = new[]
        {
            new PoleBoard("Factorraria/Content/Tiles/Machines/ElectricalConsumers/PowerPoles/PlatinumPowerPole_CrossboardUpper", new Vector2(96f, 28f), 16, 14, new[] { new Vector2(9f, 4f), new Vector2(87f, 4f) }),
            new PoleBoard("Factorraria/Content/Tiles/Machines/ElectricalConsumers/PowerPoles/PlatinumPowerPole_CrossboardLower", new Vector2(84f, 28f), 16, 12, new[] { new Vector2(9f, 4f), new Vector2(74f, 4f) })
        };
        public override PoleBoard[] Boards => boards;
    }
}