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
    }
}