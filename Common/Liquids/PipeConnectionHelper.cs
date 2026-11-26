using Factorraria.Common.Machines;
using Terraria;
using Terraria.DataStructures;

namespace Factorraria.Common.Liquids
{
    // Single source of truth for "does a pipe treat this neighbor as connectable" — used
    // both by the sprite mask (ReframeSelf, purely visual) and by the network topology
    // scan (FlowNetworkBuilder). One rule, so the pipe never LOOKS
    // connected to something it isn't actually hooked up to, or vice versa.
    public static class PipeConnectionHelper
    {
        // sourceTileType is the tile type of the pipe asking (a pipe of another material is never connectable).
        public static bool CanConnect(int neighborX, int neighborY, Direction directionFromPipe, int sourceTileType)
        {
            Tile neighborTile = Main.tile[neighborX, neighborY];

            if (PipeTierRegistry.IsPipeTile(neighborTile.TileType))
                return PipeTierRegistry.CanLink(sourceTileType, neighborTile.TileType);

            if (TileEntityHelper.TryGetEntityFromTile(neighborX, neighborY, out TileEntity entity, out _))
            {
                if (entity is MotorTileEntityBase motor)
                {
                    // Motors only accept pipes on their intake/discharge sides (front/back
                    // along Facing) — never perpendicular, and never a pipe of another material.
                    if (!PipeTierRegistry.CanLink(sourceTileType, neighborTile.TileType))
                        return false;

                    return directionFromPipe == motor.Facing || directionFromPipe == motor.Facing.Opposite();
                }

                if (entity is BaseMachine machine)
                {
                    // A machine with no liquid input or output tank has nothing for a pipe to take or give: no sprite connection
                    // (and FlowNetworkBuilder attaches nothing to it either, same rule).
                    return machine.HasLiquidPorts;
                }
            }

            return false;
        }

        // Which of a pipe's 4 sides count as active "mouths" — real connections (via CanConnect)
        // PLUS, for a straight run's dangling end, the axis-opposite side. This is what lets a
        // pipe drain from or dump into open world liquid/air on its free end, and it's the SAME
        // mask the sprite uses (PipeTileBase.ReframeSelf), so visuals and logic can't drift apart.
        // sourceTileType: leave at -1 to read the tile at (i, j); the placement hologram passes the pipe's own type, because nothing is placed there yet.
        public static void GetOpenSides(int i, int j, out bool up, out bool down, out bool left, out bool right, int sourceTileType = -1)
        {
            if (sourceTileType < 0)
                sourceTileType = Main.tile[i, j].TileType;

            bool realUp = CanConnect(i, j - 1, Direction.Up, sourceTileType);
            bool realDown = CanConnect(i, j + 1, Direction.Down, sourceTileType);
            bool realLeft = CanConnect(i - 1, j, Direction.Left, sourceTileType);
            bool realRight = CanConnect(i + 1, j, Direction.Right, sourceTileType);

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