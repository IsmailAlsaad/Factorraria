using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Systems
{
    // Tile and wall placement shared by the Robotic Arm worker and the Mech Suit builder arms (patch M3). Extracted from RoboticArmWorker
    // so the rules live in one place. Runs wherever the world is simulated (server or single-player); nothing here reads input or the screen.
    // Objects (anything with TileObjectData) moved here in patch M5 (TryGetObjectPlacement, TryGetObjectAt, PlaceObject, BroadcastObjectExtras).
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

        // ---------------------------------------------------------------- objects (patch M5)
        // Moved here from RoboticArmWorker so the Mech Suit builder arms place objects by the same rules. Everything an object needs on the
        // way is the route a player's item takes (Player.PlaceThing_Tiles): TileObject.CanPlace + Place, SquareTileFrame, the post-placement
        // hook (this is what creates a chest's Main.chest entry and, for machines, the TileEntity: BaseMachine.Hook_AfterPlacement for the
        // ModTile machines), then TileLoader.PlaceInWorld (TilePlacedGlobalTile -> MachinePlacedConsumer creates the entity of a vanilla-tile
        // machine). Server or single-player only. Nothing here reads input or the screen.

        // The shared check: vanilla's support / anchor / overlap-with-tiles test (TileObject.CanPlace says where the top-left corner lands, so
        // the footprint comes from it) plus: every footprint tile in the world, empty, and free of players / NPCs. onlyCheck must stay false
        // (true draws vanilla's placement hologram at the target). The cursor tile (x, y) is the tile a player's cursor would be on.
        public static bool TryGetObjectPlacement(int x, int y, Item item, int direction, bool onlyCheck, out TileObject data, out Rectangle footprint)
        {
            footprint = default;
            if (!TileObject.CanPlace(x, y, item.createTile, item.placeStyle, direction, out data, onlyCheck))
                return false;

            TileObjectData objectData = TileObjectData.GetTileData(data.type, data.style, data.alternate)
                ?? TileObjectData.GetTileData(item.createTile, item.placeStyle);
            footprint = objectData == null
                ? new Rectangle(x, y, 1, 1)
                : new Rectangle(data.xCoord, data.yCoord, objectData.Width, objectData.Height);

            for (int tx = footprint.Left; tx < footprint.Right; tx++)
            {
                for (int ty = footprint.Top; ty < footprint.Bottom; ty++)
                {
                    if (!WorldGen.InWorld(tx, ty, 5) || Framing.GetTileSafely(tx, ty).HasTile)
                        return false;

                    if (!Collision.EmptyTile(tx, ty, true))
                        return false;
                }
            }

            return true;
        }

        // The builder knows where the object's TOP-LEFT CORNER must land, not the cursor tile. The cursor is the corner plus the Origin of
        // the placement variant (the base data or one of its alternates: chairs facing left / right, torches on a wall / floor ...), and
        // which variant vanilla picks depends on the direction and on what supports the object. So this tries every variant's origin with the
        // preferred direction first and then the opposite one, and accepts the first one that lands exactly on the corner. direction and
        // cursor tell the caller what was used. Re-verify TileObjectData.AlternatesCount / Origin against the API source after tModLoader updates.
        public static bool TryGetObjectAt(Point corner, Item item, int preferredDirection, out int direction, out Point cursor, out TileObject data, out Rectangle footprint)
        {
            direction = preferredDirection >= 0 ? 1 : -1;
            cursor = default;
            data = default;
            footprint = default;

            TileObjectData baseData = TileObjectData.GetTileData(item.createTile, item.placeStyle);
            if (baseData == null)
                return false;

            int variants = baseData.AlternatesCount + 1;
            int first = direction;
            for (int d = 0; d < 2; d++)
            {
                int trialDirection = d == 0 ? first : -first;
                for (int k = 0; k < variants; k++)
                {
                    TileObjectData variant = TileObjectData.GetTileData(item.createTile, item.placeStyle, k) ?? baseData;
                    Point trial = new Point(corner.X + variant.Origin.X, corner.Y + variant.Origin.Y);
                    if (!TryGetObjectPlacement(trial.X, trial.Y, item, trialDirection, false, out data, out footprint))
                        continue;

                    if (footprint.X != corner.X || footprint.Y != corner.Y)
                        continue; // that variant lands somewhere else

                    direction = trialDirection;
                    cursor = trial;
                    return true;
                }
            }

            data = default;
            footprint = default;
            return false;
        }

        // Places the object that TryGetObjectPlacement / TryGetObjectAt approved. Returns false (and leaves nothing behind) when it was refused
        // at the last moment. The caller uses up the item, broadcasts the tile square and then calls BroadcastObjectExtras.
        public static bool PlaceObject(int x, int y, Item item, int direction, TileObject data, Rectangle footprint)
        {
            if (!TileObject.Place(data))
                return false;

            // Place can report success without changing anything; only a footprint that now holds the tile counts.
            bool any = false;
            for (int tx = footprint.Left; tx < footprint.Right && !any; tx++)
                for (int ty = footprint.Top; ty < footprint.Bottom && !any; ty++)
                {
                    Tile tile = Framing.GetTileSafely(tx, ty);
                    any = tile.HasTile && tile.TileType == item.createTile;
                }

            if (!any)
                return false;

            WorldGen.SquareTileFrame(x, y);
            TileObjectData.CallPostPlacementPlayerHook(x, y, item.createTile, item.placeStyle, direction, data.alternate, data);

            // A chest or dresser is created by that hook. If the world has no free chest slot left it made nothing: undo the tile
            // instead of leaving a container that holds nothing (nothing was broadcast yet, so no client has seen it).
            if (ChestUpdateMode(item.createTile) >= 0)
            {
                Point16 corner = TileObjectData.TopLeft(x, y);
                if (Chest.FindChest(corner.X, corner.Y) < 0)
                {
                    WorldGen.KillTile(x, y, noItem: true);
                    return false;
                }
            }

            TileLoader.PlaceInWorld(x, y, item);
            return true;
        }

        // The first number of the ChestUpdates packet (message 34) for a tile that owns a Main.chest entry, or -1 for any other tile.
        // Same values vanilla's Chest.AfterPlacement_Hook uses for the request a client sends after placing one.
        public static int ChestUpdateMode(int type)
        {
            if (type == TileID.Containers) return 0;
            if (type == TileID.Dressers) return 2;
            if (type == TileID.Containers2) return 4;
            if (TileID.Sets.BasicChest[type]) return 100;
            if (TileID.Sets.BasicDresser[type]) return 102;
            return -1;
        }

        // Dedicated server only, AFTER the tile square of the placement. What a player's placement sends through its own packets but the
        // server-side hooks only create locally:
        //  - a chest or dresser: the ChestUpdates message with the index of the new (empty) Main.chest entry;
        //  - every tile entity that is not a BaseMachine at the object's cells (item frame, weapon rack, training dummy ...): the vanilla
        //    tile-entity sharing message (86), the same one the server sends after a client's entity request (87). Machines are shared by
        //    MachinePlacement.ServerPlace already, so they are skipped here and never sent twice.
        // x, y is any tile of the placed object.
        public static void BroadcastObjectExtras(Item item, int x, int y)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            Point16 corner = TileObjectData.TopLeft(x, y);
            int mode = ChestUpdateMode(item.createTile);
            if (mode >= 0)
            {
                int index = Chest.FindChest(corner.X, corner.Y);
                if (index >= 0)
                    NetMessage.SendData(MessageID.ChestUpdates, -1, -1, null, mode, corner.X, corner.Y, item.placeStyle, index, mode >= 100 ? item.createTile : 0);
            }

            TileObjectData data = TileObjectData.GetTileData(item.createTile, item.placeStyle);
            int width = data == null ? 1 : data.Width;
            int height = data == null ? 1 : data.Height;
            List<int> shared = new List<int>();
            for (int tx = corner.X; tx < corner.X + width; tx++)
            {
                for (int ty = corner.Y; ty < corner.Y + height; ty++)
                {
                    if (!TileEntity.ByPosition.TryGetValue(new Point16(tx, ty), out TileEntity entity) || entity is BaseMachine || shared.Contains(entity.ID))
                        continue;

                    shared.Add(entity.ID);
                    NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, entity.ID, entity.Position.X, entity.Position.Y);
                }
            }
        }
    }
}