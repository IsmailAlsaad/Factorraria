using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.TileEntities
{
    public class PriorityConveyorTE : ModTileEntity
    {
        public int FilterItemID = ItemID.None;
        public List<Point> ChildTiles = new();

        // Master lookup: Tile Coordinate -> PriorityConveyorTE
        public static Dictionary<Point, PriorityConveyorTE> NetworkMap = new();

        public override bool IsTileValidForEntity(int x, int y)
        {
            if (!WorldGen.InWorld(x, y)) return false;
            Tile tile = Main.tile[x, y];
            return tile.HasTile && IsPriorityConveyorTile(tile.TileType);
        }

        public static bool IsPriorityConveyorTile(int tileType)
        {
            return tileType == ModContent.TileType<Tiles.Conveyors.ClockwisePriorityConveyorTile>() ||
                   tileType == ModContent.TileType<Tiles.Conveyors.CounterClockwisePriorityConveyorTile>();
        }

        public static PriorityConveyorTE GetTEAt(int x, int y)
        {
            if (NetworkMap.TryGetValue(new Point(x, y), out var te) && te != null && te.IsTileValidForEntity(x, y))
                return te;
            return null;
        }

        public override void SaveData(TagCompound tag)
        {
            tag["FilterItemID"] = FilterItemID;
            var tileList = new List<TagCompound>();
            foreach (var pt in ChildTiles)
            {
                tileList.Add(new TagCompound { ["X"] = pt.X, ["Y"] = pt.Y });
            }
            tag["ChildTiles"] = tileList;
        }

        public override void LoadData(TagCompound tag)
        {
            FilterItemID = tag.GetInt("FilterItemID");
            ChildTiles.Clear();
            if (tag.ContainsKey("ChildTiles"))
            {
                var tileList = tag.GetList<TagCompound>("ChildTiles");
                foreach (var entry in tileList)
                {
                    ChildTiles.Add(new Point(entry.GetInt("X"), entry.GetInt("Y")));
                }
            }
        }

        // ============================================================
        // PHASE 6: State Sync - Network flood-fill (BFS) on placement
        // ============================================================
        public static void UpdateNetworkOnPlace(int startX, int startY)
        {
            List<Point> connectedCluster = RunBFS(startX, startY);
            PriorityConveyorTE existingTE = null;

            // Find an existing TE in the cluster, if any
            foreach (var pt in connectedCluster)
            {
                if (NetworkMap.TryGetValue(pt, out var te) && te != null)
                {
                    existingTE = te;
                    break;
                }
            }

            // Create a new TE if none exists in the cluster
            if (existingTE == null)
            {
                int id = ModContent.GetInstance<PriorityConveyorTE>().Place(startX, startY);
                if (id != -1 && ByID.TryGetValue(id, out TileEntity entity))
                {
                    existingTE = (PriorityConveyorTE)entity;
                }
            }

            if (existingTE == null) return;

            // Re-assign all connected tiles to this single TE
            existingTE.ChildTiles.Clear();
            foreach (var pt in connectedCluster)
            {
                existingTE.ChildTiles.Add(pt);
                NetworkMap[pt] = existingTE;
            }
        }

        // ============================================================
        // PHASE 6: State Sync - Network flood-fill (BFS) on break
        // ============================================================
        public static void UpdateNetworkOnBreak(int brokenX, int brokenY)
        {
            Point brokenPt = new Point(brokenX, brokenY);

            // Remove destroyed tile from mapping
            if (NetworkMap.TryGetValue(brokenPt, out var oldTE))
            {
                oldTE?.ChildTiles.Remove(brokenPt);
                NetworkMap.Remove(brokenPt);
            }

            // Check orthogonal neighbors and re-flood-fill separated clusters
            Point[] neighbors =
            {
                new(brokenX + 1, brokenY),
                new(brokenX - 1, brokenY),
                new(brokenX, brokenY + 1),
                new(brokenX, brokenY - 1)
            };

            HashSet<Point> processed = new();

            foreach (var n in neighbors)
            {
                if (processed.Contains(n) || !WorldGen.InWorld(n.X, n.Y)) continue;

                Tile tile = Main.tile[n.X, n.Y];
                if (tile.HasTile && IsPriorityConveyorTile(tile.TileType))
                {
                    List<Point> cluster = RunBFS(n.X, n.Y);
                    foreach (var pt in cluster) processed.Add(pt);

                    // Spawn a fresh TE for this isolated cluster
                    int id = ModContent.GetInstance<PriorityConveyorTE>().Place(n.X, n.Y);
                    if (id != -1 && ByID.TryGetValue(id, out TileEntity entity) && entity is PriorityConveyorTE newTE)
                    {
                        // Preserve filter from old network
                        if (oldTE != null)
                            newTE.FilterItemID = oldTE.FilterItemID;

                        newTE.ChildTiles = cluster;
                        foreach (var pt in cluster)
                        {
                            NetworkMap[pt] = newTE;
                        }
                    }
                }
            }
        }

        private static List<Point> RunBFS(int startX, int startY)
        {
            List<Point> result = new();
            Queue<Point> queue = new();
            HashSet<Point> visited = new();

            Point start = new Point(startX, startY);
            queue.Enqueue(start);
            visited.Add(start);

            Point[] directions =
            {
                new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
            };

            while (queue.Count > 0)
            {
                Point current = queue.Dequeue();
                result.Add(current);

                foreach (var dir in directions)
                {
                    Point neighbor = new Point(current.X + dir.X, current.Y + dir.Y);
                    if (!visited.Contains(neighbor) && WorldGen.InWorld(neighbor.X, neighbor.Y))
                    {
                        Tile t = Main.tile[neighbor.X, neighbor.Y];
                        if (t.HasTile && IsPriorityConveyorTile(t.TileType))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return result;
        }
    }
}