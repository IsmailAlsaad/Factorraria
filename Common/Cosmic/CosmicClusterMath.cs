using System;
using System.Collections.Generic;

namespace Factorraria.Common.Cosmic
{
    // Pure code (no Terraria types), tested in tools/cosmic_tests. Roadmap section 2 "Ionization and discharge", assumptions A1 and A6.
    public static class CosmicClusterMath
    {
        // One number for a tile position, used as a dictionary key.
        public static long Key(int x, int y) => ((long)x << 32) | (uint)y;

        // Flood fill over the 4 neighbors (A6: up, down, left, right), breadth first, from (startX, startY). A tile joins when canJoin
        // says so; the start tile has to pass it too. The fill stops as soon as `limit` tiles are in the result (pass int.MaxValue for
        // "the whole connected group"). The start tile is the first entry. `result` is cleared first. Returns the number of tiles.
        public static int Fill(int startX, int startY, int limit, Func<int, int, bool> canJoin, List<(int x, int y)> result)
        {
            result.Clear();
            if (limit <= 0 || !canJoin(startX, startY))
                return 0;

            HashSet<long> seen = new HashSet<long> { Key(startX, startY) };
            result.Add((startX, startY));

            int head = 0;
            while (head < result.Count && result.Count < limit)
            {
                (int x, int y) = result[head++];

                for (int side = 0; side < 4 && result.Count < limit; side++)
                {
                    int nx = side == 0 ? x + 1 : side == 1 ? x - 1 : x;
                    int ny = side == 2 ? y + 1 : side == 3 ? y - 1 : y;

                    if (seen.Add(Key(nx, ny)) && canJoin(nx, ny))
                        result.Add((nx, ny));
                }
            }

            return result.Count;
        }

        // A1: Energy = (sum of the base energy absorbed into the cluster) x (product of the multipliers of every ionized block).
        // Base energy adds, multipliers multiply. A huge merged cluster really does multiply into astronomical numbers (roadmap risk
        // "merged-cluster explosion"), so this is a double and an overflow is clamped to double.MaxValue instead of becoming infinity.
        // The physics is never capped; only the number that reaches the power grid is clamped (C5).
        public static double Energy(double baseSum, double multiplierProduct)
        {
            double energy = baseSum * multiplierProduct;

            if (double.IsNaN(energy))
                return 0.0;

            return double.IsInfinity(energy) ? double.MaxValue : energy;
        }

        // The middle of a group of tiles, in tile units (the mean of the tile centers).
        public static void Center(List<(int x, int y)> tiles, out float centerX, out float centerY)
        {
            double sumX = 0.0;
            double sumY = 0.0;

            foreach ((int x, int y) in tiles)
            {
                sumX += x + 0.5;
                sumY += y + 0.5;
            }

            int count = Math.Max(1, tiles.Count);
            centerX = (float)(sumX / count);
            centerY = (float)(sumY / count);
        }
    }
}