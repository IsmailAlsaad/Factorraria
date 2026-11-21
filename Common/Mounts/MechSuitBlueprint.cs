using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.DataStructures;

namespace Factorraria.Common.Mounts
{
    // Shared by the client hologram (MechSuitHologramSystem) and the server build job (MechSuitBuildSystem), patch M3.
    // Holds the tile / wall -> placing item lookup (built once from ContentSamples) and the scan that turns a region of the world into a
    // list of blocks. Nothing here touches input, textures or the network, so it runs on a dedicated server too.
    // Rules: a tile with no placeable item (grass) is skipped. A multi-tile object counts as ONE item and is only copied when its whole
    // footprint is inside the selection. Half blocks are kept, slopes and paint are not (yet).
    public class MechSuitBlueprint : ModSystem
    {
        public class Block
        {
            public int X, Y;            // offset from the selection's top-left corner
            public bool IsWall;
            public ushort Type;
            public int FrameX, FrameY;
            public bool Half;
            public bool Plain;          // no TileObjectData: only the tile type is compared at the destination
            public int Width = 16, Height = 16, DrawYOffset;
            public int Item;
            public int Group;           // index of the block that stands for the whole object (itself for single blocks)
            public int Style = -1;      // objects: TileObjectData.GetTileStyle of the source (the "same object" test uses it); -1 for plain blocks and walls
            public bool Head = true;    // the one block that stands for a placeable unit: a plain block, a wall, or the first block of an object (patch M5)
            public int ObjX, ObjY, ObjW, ObjH; // objects only: the whole footprint (offset from the selection, absolute after Shift); ObjW 0 = not an object
        }

        Dictionary<(int, int), int> tileItems;
        Dictionary<int, int> tileItemsAnyStyle;
        Dictionary<int, int> wallItems;

        public override void Unload()
        {
            tileItems = null;
            tileItemsAnyStyle = null;
            wallItems = null;
        }

        void EnsureItemMaps()
        {
            if (tileItems != null)
                return;

            tileItems = new Dictionary<(int, int), int>();
            tileItemsAnyStyle = new Dictionary<int, int>();
            wallItems = new Dictionary<int, int>();

            foreach (Item sample in ContentSamples.ItemsByType.Values)
            {
                if (sample == null || sample.IsAir)
                    continue;

                if (sample.createTile >= 0)
                {
                    tileItems.TryAdd((sample.createTile, sample.placeStyle), sample.type);
                    tileItemsAnyStyle.TryAdd(sample.createTile, sample.type);
                }

                if (sample.createWall >= 0)
                    wallItems.TryAdd(sample.createWall, sample.type);
            }
        }

        // The item that places this tile (type + style), or false for tiles nothing places (grass, trees, ...).
        public bool TryGetTileItem(int type, int style, bool plain, out int item)
        {
            EnsureItemMaps();
            if (tileItems.TryGetValue((type, style), out item))
                return true;

            // Plain blocks have no style to read; take any item that places the type.
            return plain && tileItemsAnyStyle.TryGetValue(type, out item);
        }

        // ---- shared rules (patch M5): the server job, the cursor hologram and the placed-copy holograms all decide with these ----

        public enum UnitState : byte { Free, Built, Blocked }

        // The cells a block's unit covers: the whole object, or the block's own tile.
        public static Rectangle Footprint(Block b)
        {
            return b.ObjW > 0 ? new Rectangle(b.ObjX, b.ObjY, b.ObjW, b.ObjH) : new Rectangle(b.X, b.Y, 1, 1);
        }

        // Does this destination cell already hold what the block wants? A wall: the same wall. A tile: the same tile type, and for an object
        // also the same style (a different chair is not "built"). Frames are NOT compared, so a chair that faces the other way counts as built.
        public static bool Matches(Block b, Tile t)
        {
            if (b.IsWall)
                return t.WallType == b.Type;

            if (!t.HasTile || t.TileType != b.Type)
                return false;

            return b.ObjW <= 0 || TileObjectData.GetTileStyle(t) == b.Style;
        }

        // Does this destination cell hold something on the block's layer (tile or wall)?
        public static bool Occupied(Block b, Tile t)
        {
            return b.IsWall ? t.WallType != 0 : t.HasTile;
        }

        // The state of a unit at its ABSOLUTE coordinates (the server job, the placed copies): built = every cell matches, blocked = a cell
        // holds something else (or is outside the world, or only part of an object is there), free = every cell is empty.
        public static UnitState StateOf(Block b)
        {
            Rectangle f = Footprint(b);
            int matched = 0, other = 0, total = 0;
            for (int x = f.Left; x < f.Right; x++)
            {
                for (int y = f.Top; y < f.Bottom; y++)
                {
                    total++;
                    if (!WorldGen.InWorld(x, y, 1))
                        return UnitState.Blocked;

                    Tile t = Main.tile[x, y];
                    if (Matches(b, t))
                        matched++;
                    else if (Occupied(b, t))
                        other++;
                }
            }

            if (matched == total)
                return UnitState.Built;

            return other > 0 || matched > 0 ? UnitState.Blocked : UnitState.Free;
        }

        // Moves a scanned list to its destination (X, Y and the object footprint).
        public static void Shift(List<Block> blocks, Point offset)
        {
            foreach (Block b in blocks)
            {
                b.X += offset.X;
                b.Y += offset.Y;
                if (b.ObjW > 0)
                {
                    b.ObjX += offset.X;
                    b.ObjY += offset.Y;
                }
            }
        }

        // Every (x, y, isWall) cell the heads of this list cover. A newer copy wins where it overlaps an older one: the older units that
        // touch any of these cells are dropped whole (server job and placed holograms use the same two calls).
        public static HashSet<(int, int, bool)> CellsOf(List<Block> blocks)
        {
            HashSet<(int, int, bool)> cells = new HashSet<(int, int, bool)>();
            foreach (Block b in blocks)
            {
                if (!b.Head)
                    continue;

                Rectangle f = Footprint(b);
                for (int x = f.Left; x < f.Right; x++)
                    for (int y = f.Top; y < f.Bottom; y++)
                        cells.Add((x, y, b.IsWall));
            }

            return cells;
        }

        public static bool Overlaps(HashSet<(int, int, bool)> cells, Block b)
        {
            Rectangle f = Footprint(b);
            for (int x = f.Left; x < f.Right; x++)
                for (int y = f.Top; y < f.Bottom; y++)
                    if (cells.Contains((x, y, b.IsWall)))
                        return true;

            return false;
        }

        // Reads the region from the world (whatever side this runs on) into blocks. Clears the list first.
        public void Scan(Rectangle selection, List<Block> blocks)
        {
            EnsureItemMaps();
            blocks.Clear();
            HashSet<Point> doneObjects = new();

            for (int x = selection.Left; x < selection.Right; x++)
            {
                for (int y = selection.Top; y < selection.Bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    Tile tile = Main.tile[x, y];
                    int rx = x - selection.X;
                    int ry = y - selection.Y;

                    if (tile.WallType > 0 && wallItems.TryGetValue(tile.WallType, out int wallItem))
                    {
                        blocks.Add(new Block
                        {
                            X = rx, Y = ry, IsWall = true, Type = tile.WallType,
                            FrameX = tile.WallFrameX, FrameY = tile.WallFrameY,
                            Item = wallItem, Group = blocks.Count
                        });
                    }

                    if (!tile.HasTile)
                        continue;

                    TileObjectData data = TileObjectData.GetTileData(tile);
                    if (data == null)
                    {
                        if (!TryGetTileItem(tile.TileType, 0, true, out int plainItem))
                            continue;

                        blocks.Add(new Block
                        {
                            X = rx, Y = ry, Type = tile.TileType, FrameX = tile.TileFrameX, FrameY = tile.TileFrameY,
                            Half = tile.IsHalfBlock, Plain = true, Item = plainItem, Group = blocks.Count
                        });
                        continue;
                    }

                    // Multi-tile object: handle it once, from any of its cells, and only if all of it is inside the selection.
                    Point16 corner = TileObjectData.TopLeft(x, y);
                    if (!doneObjects.Add(new Point(corner.X, corner.Y)))
                        continue;

                    Rectangle footprint = new Rectangle(corner.X, corner.Y, data.Width, data.Height);
                    if (!selection.Contains(footprint.Left, footprint.Top) || !selection.Contains(footprint.Right - 1, footprint.Bottom - 1))
                        continue;

                    int objectStyle = TileObjectData.GetTileStyle(tile);
                    if (!TryGetTileItem(tile.TileType, objectStyle, false, out int objectItem))
                        continue;

                    int group = -1;
                    for (int fx = footprint.Left; fx < footprint.Right; fx++)
                    {
                        for (int fy = footprint.Top; fy < footprint.Bottom; fy++)
                        {
                            Tile cell = Main.tile[fx, fy];
                            if (!cell.HasTile || cell.TileType != tile.TileType)
                                continue;

                            if (group < 0)
                                group = blocks.Count;

                            // Source rectangle height of this row, the way vanilla's tile drawing finds it.
                            int rest = cell.TileFrameY % data.CoordinateFullHeight;
                            int row = 0;
                            while (row < data.CoordinateHeights.Length - 1 && rest > 0)
                            {
                                rest -= data.CoordinateHeights[row] + data.CoordinatePadding;
                                row++;
                            }

                            blocks.Add(new Block
                            {
                                X = fx - selection.X, Y = fy - selection.Y, Type = cell.TileType,
                                FrameX = cell.TileFrameX, FrameY = cell.TileFrameY,
                                Width = data.CoordinateWidth, Height = data.CoordinateHeights[row], DrawYOffset = data.DrawYOffset,
                                Item = objectItem, Group = group, Style = objectStyle, Head = group == blocks.Count,
                                ObjX = footprint.X - selection.X, ObjY = footprint.Y - selection.Y, ObjW = footprint.Width, ObjH = footprint.Height
                            });
                        }
                    }
                }
            }
        }
    }
}