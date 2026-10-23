using System;
using System.Collections.Generic;
using Factorraria.Common.Carts;
using Factorraria.Content.Items.Materials;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace Factorraria.Common.WorldGeneration
{
    /// <summary>
    /// Abandoned minecarts on the vanilla tracks, as environmental storytelling and a quiet hint that carts can be placed.
    /// Runs once while a NEW world is generated (host / single-player only, so it is authoritative by nature); the carts are then
    /// saved by CartSystem like any player-placed cart and synced to clients by the normal cart join sync.
    /// Per track tile: SpawnChancePerTile. A hit is a solo carriage (plain Minecart) or, LinkedChance of the time, a Steampunk motor
    /// cart (always out of fuel) with a carriage linked behind it. Every carriage then rolls its cargo: chest cart / terrarium cart / empty.
    /// All numbers are instance fields on purpose (hot reload can tweak them).
    /// </summary>
    public partial class CartWorldGen : ModSystem
    {
        public bool Enabled = true;
        public double SpawnChancePerTile = 0.05;   // per vanilla track tile
        public double LinkedChance = 0.30;          // of the spawns: motor cart + carriage (the rest are a lone carriage)
        public double ChestChance = 0.60;           // per carriage: chest cart
        public double TerrariumChance = 0.10;       // per carriage: terrarium cart (the rest are empty)
        public double GoldChestShare = 0.5;         // of the chest carts: gold chest (the rest wooden)
        public int MinSpacingTiles = 10;            // no two spawns closer than this (straight-line tiles)
        public int MaxCartsPerTrack = 3;            // per connected stretch of track
        public int FlatMarginPx = 20;               // extra flat rail required beyond the cart footprint
        public int HeadroomRowsMargin = 0;          // extra empty tile rows required above the cart

        // Chest cart loot: the biome tables live in CartWorldGen.Biomes.cs. These feed the ore slots of the underground and snow tables.
        public int OreSlots = 3;
        public int OreMin = 7, OreMax = 20;

        private int statTracks, statRolled, statNoShape, statNoSpace, statCap, statSolo, statLinked, statChest, statTerrarium;

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            // Vanilla lays its tracks inside the "Micro Biomes" pass. Run after it, as late as possible but before the final cleanup.
            int micro = tasks.FindIndex(t => t.Name.Equals("Micro Biomes"));
            if (micro == -1)
            {
                Mod.Logger.Warn("[CartGen] 'Micro Biomes' pass not found, no carts will be generated");
                return;
            }

            int insertAt = tasks.FindIndex(micro + 1, t => t.Name.Equals("Final Cleanup"));
            if (insertAt == -1)
                insertAt = micro + 1;

            tasks.Insert(insertAt, new PassLegacy("Factorraria Abandoned Minecarts", GenerateCarts, 5));
        }

        private void GenerateCarts(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Abandoning minecarts";

            if (!Enabled)
                return;

            if (!TrackData.EnsureLoaded())
            {
                Mod.Logger.Warn("[CartGen] track tables not available (" + TrackData.FailureReason + "), skipping");
                return;
            }

            statTracks = statRolled = statNoShape = statNoSpace = statCap = statSolo = statLinked = statChest = statTerrarium = 0;

            // ---- 1. Every vanilla track tile ----
            List<Point> tracks = new List<Point>();
            for (int x = 1; x < Main.maxTilesX - 1; x++)
            {
                for (int y = 1; y < Main.maxTilesY - 1; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (tile.HasUnactuatedTile && tile.TileType == TileID.MinecartTrack)
                        tracks.Add(new Point(x, y));
                }
            }

            statTracks = tracks.Count;
            if (tracks.Count == 0)
            {
                Mod.Logger.Info("[CartGen] no track tiles in this world");
                return;
            }

            // ---- 2. Group them into connected stretches (8-neighbour) so one long track cannot fill up with carts ----
            Dictionary<int, int> component = new Dictionary<int, int>(tracks.Count);
            for (int i = 0; i < tracks.Count; i++)
                component[Key(tracks[i])] = -1;

            int componentCount = 0;
            Queue<Point> queue = new Queue<Point>();
            for (int i = 0; i < tracks.Count; i++)
            {
                if (component[Key(tracks[i])] != -1)
                    continue;

                component[Key(tracks[i])] = componentCount;
                queue.Enqueue(tracks[i]);

                while (queue.Count > 0)
                {
                    Point p = queue.Dequeue();
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            Point n = new Point(p.X + dx, p.Y + dy);
                            int key = Key(n);
                            int label;
                            if (component.TryGetValue(key, out label) && label == -1)
                            {
                                component[key] = componentCount;
                                queue.Enqueue(n);
                            }
                        }
                    }
                }

                componentCount++;
            }

            int[] perTrack = new int[componentCount];

            // ---- 3. Shuffle (so the world is not biased towards its left edge), then roll each tile ----
            for (int i = tracks.Count - 1; i > 0; i--)
            {
                int j = WorldGen.genRand.Next(i + 1);
                Point swap = tracks[i];
                tracks[i] = tracks[j];
                tracks[j] = swap;
            }

            List<Point> spawned = new List<Point>();

            for (int i = 0; i < tracks.Count; i++)
            {
                if (WorldGen.genRand.NextDouble() >= SpawnChancePerTile)
                    continue;

                statRolled++;
                Point tile = tracks[i];
                int trackId = component[Key(tile)];

                if (perTrack[trackId] >= MaxCartsPerTrack)
                {
                    statCap++;
                    continue;
                }

                if (TooClose(tile, spawned))
                {
                    statNoSpace++;
                    continue;
                }

                bool linked = WorldGen.genRand.NextDouble() < LinkedChance;
                int facing = WorldGen.genRand.NextBool() ? 1 : -1;
                Vector2 railPoint;

                if (!TryGetRailPoint(tile, linked, facing, out railPoint))
                {
                    if (!linked || !TryGetRailPoint(tile, true, -facing, out railPoint))
                    {
                        statNoShape++;
                        continue;
                    }

                    facing = -facing;
                }

                if (linked)
                    SpawnLinked(tile, railPoint, facing);
                else
                    SpawnSolo(tile, railPoint, facing);

                perTrack[trackId]++;
                spawned.Add(tile);
            }

            Mod.Logger.Info("[CartGen] tracks=" + statTracks + " stretches=" + componentCount + " rolled=" + statRolled
                + " solo=" + statSolo + " linked=" + statLinked + " chestCarts=" + statChest + " terrariumCarts=" + statTerrarium
                + " rejected(shape=" + statNoShape + " spacing=" + statNoSpace + " perTrackCap=" + statCap + ")");
        }

        private int Key(Point p)
        {
            return p.Y * Main.maxTilesX + p.X;
        }

        private bool TooClose(Point tile, List<Point> spawned)
        {
            long limit = (long)MinSpacingTiles * MinSpacingTiles;
            for (int i = 0; i < spawned.Count; i++)
            {
                long dx = spawned[i].X - tile.X;
                long dy = spawned[i].Y - tile.Y;
                if (dx * dx + dy * dy < limit)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The rail point for a cart standing on this track tile, if the whole footprint is straight, flat, free of boosters /
        /// pressure plates / track ends and has room above. For a linked pair the motor stands at the point facing 'facing' and the
        /// carriage sits one ChainSpacing behind it, so the footprint extends further to the back.
        /// </summary>
        private bool TryGetRailPoint(Point tile, bool linked, int facing, out Vector2 railPoint)
        {
            railPoint = Vector2.Zero;

            float x = tile.X * 16f + 8f;
            TrackSample centre;
            if (!TrackData.TrySample(x, tile.Y * 16f + 8f, out centre) || !centre.HasSurface)
                return false;

            float y = centre.SurfaceY;
            float half = CartPhysics.HitboxWidth / 2f + FlatMarginPx;
            float front = half;
            float back = half + (linked ? CartPhysics.ChainSpacing : 0f);

            float xMin = facing > 0 ? x - back : x - front;
            float xMax = facing > 0 ? x + front : x + back;

            if (xMin < 32f || xMax > (Main.maxTilesX - 2) * 16f)
                return false;

            for (float px = xMin; px <= xMax; px += 4f)
            {
                TrackSample sample;
                if (!TrackData.TrySample(px, y, out sample) || !sample.HasSurface || Math.Abs(sample.SurfaceY - y) > 0.5f || sample.BoostDirection != 0)
                    return false;

                Point plate;
                if (TrackData.TryGetPressurePlate(px, y, out plate))
                    return false;
            }

            // Headroom: no solid tile where the cart body goes.
            int topRow = (int)((y - CartPhysics.HitboxHeight) / 16f) - HeadroomRowsMargin;
            int bottomRow = (int)((y - 1f) / 16f);
            for (int tx = (int)(xMin / 16f); tx <= (int)(xMax / 16f); tx++)
            {
                for (int ty = topRow; ty <= bottomRow; ty++)
                {
                    if (!WorldGen.InWorld(tx, ty, 1))
                        return false;

                    Tile t = Main.tile[tx, ty];
                    if (t.HasUnactuatedTile && t.TileType != TileID.MinecartTrack && Main.tileSolid[t.TileType])
                        return false;
                }
            }

            railPoint = new Vector2(x, y);
            return true;
        }

        // SpawnSolo, SpawnLinked, RollCargo, chest and terrarium contents: see CartWorldGen.Biomes.cs
    }
}