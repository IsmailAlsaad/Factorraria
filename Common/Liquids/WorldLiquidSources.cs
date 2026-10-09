using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Liquids
{
    /// <summary>
    /// "Is this world liquid cell part of a bottomless source?" Same rule as the pipe network (LiquidNetwork.ComputeIsInfiniteSource):
    /// flood-fill (4 directions) through connected cells holding the same liquid; if the body reaches InfiniteThreshold cells it is
    /// treated as infinite. Used by terrarium carts so a big lake is not drained; the pipe code keeps its own copy.
    /// Results are cached per cell and re-checked every RecheckInterval ticks, like the pipes do.
    /// </summary>
    public class WorldLiquidSources : ModSystem
    {
        public const int InfiniteThreshold = 100;
        private const int RecheckInterval = 60;     // ticks, about 1 second
        private const int MaxCachedCells = 512;

        private static readonly Dictionary<Point, (bool IsInfinite, long LastCheckedTick)> cache = new Dictionary<Point, (bool, long)>();
        private static readonly HashSet<Point> visited = new HashSet<Point>();
        private static readonly Queue<Point> queue = new Queue<Point>();
        private static readonly Point[] offsets = { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) };

        public static bool IsInfiniteSource(Point start)
        {
            long tick = (long)Main.GameUpdateCount;

            if (cache.TryGetValue(start, out var cached) && tick - cached.LastCheckedTick < RecheckInterval)
            {
                return cached.IsInfinite;
            }

            if (cache.Count >= MaxCachedCells)
            {
                cache.Clear();
            }

            bool result = ComputeIsInfinite(start);
            cache[start] = (result, tick);
            return result;
        }

        /// <summary>
        /// The flood-fill itself, with no caching. Callers that keep their own cache (the pipe network does, per network)
        /// use this; everything else should call IsInfiniteSource.
        /// </summary>
        public static bool ComputeIsInfinite(Point start)
        {
            visited.Clear();
            queue.Clear();

            byte liquidType = (byte)Main.tile[start.X, start.Y].LiquidType;
            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                if (visited.Count >= InfiniteThreshold)
                {
                    return true;
                }

                Point current = queue.Dequeue();
                foreach (Point offset in offsets)
                {
                    Point neighbor = new Point(current.X + offset.X, current.Y + offset.Y);
                    if (!WorldGen.InWorld(neighbor.X, neighbor.Y) || visited.Contains(neighbor))
                    {
                        continue;
                    }

                    Tile t = Main.tile[neighbor.X, neighbor.Y];
                    if (t.LiquidAmount <= 0 || t.LiquidType != liquidType)
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return false;
        }

        public override void OnWorldUnload()
        {
            cache.Clear();
        }
    }
}