using Microsoft.Xna.Framework.Graphics;

namespace Factorraria.Common.Machines
{
    // A machine that needs something painted UNDER its own tile art (e.g. a liquid seen through a window in the casing).
    // MachineVisualOverride.PreDraw calls DrawUnderTile for every cell of the machine, inside the tile pass, right before that
    // cell's art is drawn, so the tile always covers it. Client only; cells off screen are never asked.
    public interface IMachineTileUnderlay
    {
        void DrawUnderTile(SpriteBatch spriteBatch, int i, int j);
    }
}