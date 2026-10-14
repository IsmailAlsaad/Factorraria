using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Factorraria.Common.Machines
{
    // A machine that draws something dynamic (a moving arm, an animated part) on top of its tiles, which the fixed-frame
    // MachineVisualRegistry sheets cannot express. MachineDynamicDrawSystem ticks and draws every machine that implements this.
    public interface IMachineDynamicDraw
    {
        // Once per game tick, client only. Advance animation state here.
        void DynamicUpdate();

        // World-space circle that contains everything DynamicDraw can paint, used to skip off-screen machines.
        // Return false when there is nothing to draw right now.
        bool TryGetDynamicBounds(out Vector2 center, out float radius);

        // Called inside an already-begun SpriteBatch (world space, same transform the tiles use), after the tiles.
        void DynamicDraw(SpriteBatch spriteBatch);

        // Optional: drawn in the pass that runs BEFORE the tiles, so tiles cover it (beams, cables, anything that should
        // look like it passes behind the world). Same SpriteBatch contract as DynamicDraw.
        void DynamicDrawBehindTiles(SpriteBatch spriteBatch) { }
    }
}