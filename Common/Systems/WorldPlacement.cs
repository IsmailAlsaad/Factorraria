using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    // Tile and wall placement shared by the Robotic Arm worker and the Mech Suit builder arms (patch M3). Extracted from RoboticArmWorker
    // so the rules live in one place. Runs wherever the world is simulated (server or single-player); nothing here reads input or the screen.
    // Objects (anything with TileObjectData) still live in RoboticArmWorker; they move here when the builder places objects (patch M5).
    public static class WorldPlacement
    {
        // Copy of the rule in Player.PlaceThing_Walls: a wall goes where one of the four neighbours is a tile (any tile) or has a wall.
        // Replacing a different wall is left out (the target must have no wall). Re-verify after tModLoader updates.
        public static bool CanPlaceWall(int x, int y, Item item)
        {
            if (Framing.GetTileSafely(x, y).WallType != WallID.None)
                return false;

            if (!WallLoader.CanPlace(x, y, item.createWall))
                return false;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? -1 : i == 1 ? 1 : 0);
                int ny = y + (i == 2 ? -1 : i == 3 ? 1 : 0);
                Tile neighbour = Framing.GetTileSafely(nx, ny);

                if (neighbour.HasTile || neighbour.WallType > WallID.None)
                    return true;
            }

            return false;
        }

        // Copy of the vanilla rule for plain blocks (Player.PlaceThing_Tiles_BlockPlacementForAssortedThings, final else branch):
        // WorldGen.PlaceTile does not enforce it. Allowed when the target or one of its four neighbours has a wall, or a neighbour
        // is an active solid / beam / rope / tile 314. Re-verify this against the decompile after tModLoader updates.
        public static bool HasSupport(int x, int y)
        {
            if (Framing.GetTileSafely(x, y).WallType > WallID.None)
                return true;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? -1 : i == 1 ? 1 : 0);
                int ny = y + (i == 2 ? -1 : i == 3 ? 1 : 0);
                Tile neighbour = Framing.GetTileSafely(nx, ny);

                if (neighbour.WallType > WallID.None)
                    return true;

                if (neighbour.HasTile &&
                    (Main.tileSolid[neighbour.TileType]
                    || TileID.Sets.IsBeam[neighbour.TileType]
                    || Main.tileRope[neighbour.TileType]
                    || neighbour.TileType == TileID.MinecartTrack))
                {
                    return true;
                }
            }

            return false;
        }

        // Whether a plain block (an item whose tile has no TileObjectData) can go into this empty tile: in the world, no tile there,
        // the tile loader agrees and it has support.
        public static bool CanPlacePlain(int x, int y, Item item)
        {
            if (!WorldGen.InWorld(x, y, 5))
                return false;

            if (Framing.GetTileSafely(x, y).HasTile)
                return false;

            if (!TileLoader.CanPlace(x, y, item.createTile))
                return false;

            return HasSupport(x, y);
        }

        public static bool PlaceWall(int x, int y, Item item)
        {
            if (!WallLoader.CanPlace(x, y, item.createWall))
                return false;

            WorldGen.PlaceWall(x, y, item.createWall);
            if (Framing.GetTileSafely(x, y).WallType != item.createWall)
                return false;

            WallLoader.PlaceInWorld(x, y, item);
            return true;
        }

        public static bool PlacePlain(int x, int y, Item item)
        {
            // plr: -1 on purpose. The ghost's whoAmI is 0, which is the real local player in singleplayer.
            // PlaceTile refuses a tile a player or town NPC stands on (forced stays false), so that is "blocked, retry later".
            if (!WorldGen.PlaceTile(x, y, item.createTile, mute: false, forced: false, plr: -1, style: item.placeStyle))
                return false;

            TileLoader.PlaceInWorld(x, y, item);
            return true;
        }
    }
}