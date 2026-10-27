using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.BeeWheel
{
    // Flood fill (BFS) through passable tiles starting at the ring around the machine's footprint, up to `radius` steps of path
    // length. Every solid tile that touches a reached passable tile is "exposed" and counted once (a surface-area calculator);
    // the exposed Hive tiles are the result. One instance per Bee Wheel, buffers are reused so a scan allocates nothing.
    public sealed class BeeWheelScanner
    {
        public int HiveCount;                 // exposed Hive tiles
        public int ExposedSolidCount;         // exposed solid tiles of any kind
        public readonly List<Point> ExposedHives = new List<Point>();   // tile coordinates, only filled when collectPositions is true

        int[] visited = Array.Empty<int>();   // stamp == current scan: flooded (or part of the footprint)
        int[] solidSeen = Array.Empty<int>(); // stamp == current scan: solid already recorded
        int[] queue = Array.Empty<int>();
        short[] queueDist = Array.Empty<short>();
        int stamp;

        int minX, minY, sideX, sideY;

        public void Scan(int cornerX, int cornerY, int width, int height, int radius, bool collectPositions)
        {
            HiveCount = 0;
            ExposedSolidCount = 0;
            ExposedHives.Clear();

            int margin = radius + 1;   // one extra ring so solids next to the farthest flooded tile are inside the box
            minX = cornerX - margin;
            minY = cornerY - margin;
            sideX = width + margin * 2;
            sideY = height + margin * 2;

            int cells = sideX * sideY;
            if (visited.Length != cells)
            {
                visited = new int[cells];
                solidSeen = new int[cells];
                queue = new int[cells];
                queueDist = new short[cells];
                stamp = 0;
            }

            stamp++;

            // The machine's own tiles are never entered (they are not solid, so the flood would otherwise run through them).
            for (int x = cornerX; x < cornerX + width; x++)
            {
                for (int y = cornerY; y < cornerY + height; y++)
                    visited[Index(x, y)] = stamp;
            }

            int head = 0;
            int tail = 0;

            // Seeds: the ring of tiles around the footprint (only the passable ones start the flood).
            for (int x = cornerX - 1; x <= cornerX + width; x++)
            {
                for (int y = cornerY - 1; y <= cornerY + height; y++)
                {
                    bool insideFootprint = x >= cornerX && x < cornerX + width && y >= cornerY && y < cornerY + height;
                    if (insideFootprint || !InBox(x, y) || !WorldGen.InWorld(x, y, 1))
                        continue;

                    int idx = Index(x, y);
                    if (visited[idx] == stamp || !IsPassable(x, y))
                        continue;

                    visited[idx] = stamp;
                    queue[tail] = idx;
                    queueDist[tail] = 1;
                    tail++;
                }
            }

            while (head < tail)
            {
                int idx = queue[head];
                int dist = queueDist[head];
                head++;

                int cx = idx % sideX + minX;
                int cy = idx / sideX + minY;

                for (int n = 0; n < 4; n++)
                {
                    int nx = n == 0 ? cx - 1 : n == 1 ? cx + 1 : cx;
                    int ny = n == 2 ? cy - 1 : n == 3 ? cy + 1 : cy;

                    if (!InBox(nx, ny) || !WorldGen.InWorld(nx, ny, 1))
                        continue;

                    int nidx = Index(nx, ny);
                    if (visited[nidx] == stamp)
                        continue;

                    if (IsPassable(nx, ny))
                    {
                        if (dist < radius)
                        {
                            visited[nidx] = stamp;
                            queue[tail] = nidx;
                            queueDist[tail] = (short)(dist + 1);
                            tail++;
                        }
                    }
                    else if (solidSeen[nidx] != stamp)
                    {
                        solidSeen[nidx] = stamp;
                        ExposedSolidCount++;

                        if (Main.tile[nx, ny].TileType == TileID.Hive)
                        {
                            HiveCount++;
                            if (collectPositions)
                                ExposedHives.Add(new Point(nx, ny));
                        }
                    }
                }
            }
        }

        bool InBox(int x, int y) => x >= minX && y >= minY && x < minX + sideX && y < minY + sideY;

        int Index(int x, int y) => (y - minY) * sideX + (x - minX);

        // Air, actuated (inactive) tiles, non-solid tiles (torches, furniture, wires) and platforms let the flood through. Liquids do too.
        static bool IsPassable(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || tile.IsActuated)
                return true;

            int type = tile.TileType;
            return !Main.tileSolid[type] || Main.tileSolidTop[type];
        }
    }
}