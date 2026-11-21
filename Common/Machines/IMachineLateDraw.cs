using Microsoft.Xna.Framework.Graphics;

namespace Factorraria.Common.Machines
{
    // Parts of a machine drawn AFTER the smart cursor outline, so they sit in front of the highlight (MachineSmartOutlineSystem.PostDrawTiles calls
    // this right after it draws the outlines). The machine must also implement IMachineDynamicDraw: its TryGetDynamicBounds is the screen cull.
    public interface IMachineLateDraw
    {
        void DrawAfterOutline(SpriteBatch spriteBatch);
    }
}
