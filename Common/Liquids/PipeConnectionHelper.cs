using Factorraria.Common.Machines;
using Terraria;
using Terraria.DataStructures;

namespace Factorraria.Common.Liquids
{
    // Single source of truth for "does a pipe treat this neighbor as connectable" — used
    // both by the sprite mask (ReframeSelf, purely visual) and by the network topology
    // scan (LiquidNetworkSystem.RecordAttachmentIfAny). One rule, so the pipe never LOOKS
    // connected to something it isn't actually hooked up to, or vice versa.
    public static class PipeConnectionHelper
    {
        public static bool CanConnect(int neighborX, int neighborY, Direction directionFromPipe)
        {
            Tile neighborTile = Main.tile[neighborX, neighborY];

            if (PipeTierRegistry.IsPipeTile(neighborTile.TileType))
                return true;

            if (TileEntityHelper.TryGetEntityFromTile(neighborX, neighborY, out TileEntity entity, out _))
            {
                if (entity is MotorTileEntityBase motor)
                {
                    // Motors only accept pipes on their intake/discharge sides (front/back
                    // along Facing) — never perpendicular.
                    return directionFromPipe == motor.Facing || directionFromPipe == motor.Facing.Opposite();
                }

                if (entity is BaseMachine)
                {
                    return true;
                }
            }

            return false;
        }

        // Which of a pipe's 4 sides count as active "mouths" — real connections (via CanConnect)
        // PLUS, for a straight run's dangling end, the axis-opposite side. This is what lets a
        // pipe drain from or dump into open world liquid/air on its free end, and it's the SAME
        // mask the sprite uses (PipeTileBase.ReframeSelf), so visuals and logic can't drift apart.
        public static void GetOpenSides(int i, int j, out bool up, out bool down, out bool left, out bool right)
        {
            bool realUp = CanConnect(i, j - 1, Direction.Up);
            bool realDown = CanConnect(i, j + 1, Direction.Down);
            bool realLeft = CanConnect(i - 1, j, Direction.Left);
            bool realRight = CanConnect(i + 1, j, Direction.Right);

            int connectionCount = (realUp ? 1 : 0) + (realDown ? 1 : 0) + (realLeft ? 1 : 0) + (realRight ? 1 : 0);

            up = realUp; down = realDown; left = realLeft; right = realRight;

            if (connectionCount == 0)
            {
                // Fully isolated pipe — defaults to an open horizontal run (existing convention).
                left = true;
                right = true;
            }
            else if (connectionCount == 1)
            {
                if (realLeft) right = true;
                else if (realRight) left = true;
                else if (realUp) down = true;
                else if (realDown) up = true;
            }
            // 2+: corner/T/cross — real connections only, no auto-opening.
        }
    }
}