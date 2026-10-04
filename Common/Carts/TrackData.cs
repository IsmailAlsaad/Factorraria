using System;
using System.Reflection;
using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Carts
{
    /// <summary>What happens to a cart whose leading wheel reaches the end of a track piece.</summary>
    public enum TrackEnd
    {
        None,
        Stop,   // vanilla "regular" end piece: cart halts
        Bounce, // vanilla bumper: cart reverses
        Ramp,   // launch ramp: cart is thrown off at 45 degrees
        Open    // open end: cart falls off
    }

    /// <summary>Result of looking at the track under one x position.</summary>
    public struct TrackSample
    {
        public bool HasSurface;
        public float SurfaceY;   // world pixels
        public TrackEnd End;     // only set when there is no surface but an end-of-track slice
        public int BoostDirection; // -1 booster pushes left, +1 pushes right, 0 none

        public bool IsEmpty
        {
            get { return !HasSurface && End == TrackEnd.None; }
        }
    }

    /// <summary>
    /// Read-only view of vanilla minecart track geometry.
    ///
    /// Vanilla stores the shape of every track tile as a "frame id" in Tile.TileFrameX (TileFrameY holds the
    /// alternate frame of a junction, which we ignore for now). Terraria.Minecart keeps private tables that say,
    /// for each frame id, how high the rail is in each of 8 vertical slices of the tile (2 px wide each).
    /// We read those tables by reflection so we always match the installed game instead of copying numbers.
    ///
    /// Slice value meaning (after vanilla's own post-processing):
    ///   >= 0 : rail surface is that many pixels below the top of the tile
    ///   -1   : regular end piece   -2 : bouncy bumper   -3 : launch ramp   -4 : open end
    /// </summary>
    public static class TrackData
    {
        public const float MaxSnap = 20f; // how far (px) a wheel may be from the rail it is following

        private const int SliceWidth = 2;
        private const int SliceCount = 8;
        private const int FrameCount = 36;

        private static int[][] tileHeight;
        private static int[] trackType;  // 0 normal, 1 pressure plate, 2 booster
        private static bool[] boostLeft;
        private static bool failed;

        /// <summary>Set (once) if the vanilla tables could not be read. Carts are disabled in that case.</summary>
        public static string FailureReason { get; private set; }

        public static bool EnsureLoaded()
        {
            if (tileHeight != null)
            {
                return true;
            }

            if (failed)
            {
                return false;
            }

            try
            {
                int[][] heights = (int[][])GetVanillaField("_tileHeight");
                if (heights == null)
                {
                    return false; // vanilla has not initialised its tables yet, try again later
                }

                int[] types = (int[])GetVanillaField("_trackType");
                bool[] boosts = (bool[])GetVanillaField("_boostLeft");

                if (types == null || boosts == null || heights.Length != FrameCount || types.Length != FrameCount || boosts.Length != FrameCount)
                {
                    throw new InvalidOperationException("Minecart track tables have an unexpected size (vanilla layout changed).");
                }

                for (int i = 0; i < heights.Length; i++)
                {
                    if (heights[i] == null || heights[i].Length != SliceCount)
                    {
                        throw new InvalidOperationException("Minecart track tables have an unexpected slice count (vanilla layout changed).");
                    }
                }

                tileHeight = heights;
                trackType = types;
                boostLeft = boosts;
                return true;
            }
            catch (Exception e)
            {
                failed = true;
                FailureReason = e.Message;
                return false;
            }
        }

        public static void Reset()
        {
            tileHeight = null;
            trackType = null;
            boostLeft = null;
            failed = false;
            FailureReason = null;
        }

        private static object GetVanillaField(string name)
        {
            FieldInfo field = typeof(Minecart).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
            {
                throw new MissingFieldException(typeof(Minecart).FullName, name);
            }

            return field.GetValue(null);
        }

        /// <summary>
        /// Looks at the track tiles around (worldX, referenceY). Returns true if the position is on a rail surface
        /// or inside an end-of-track slice. referenceY is where we expect the rail to be; it picks the right tile
        /// when tracks are stacked or the rail climbs into the next row.
        /// </summary>
        public static bool TrySample(float worldX, float referenceY, out TrackSample sample)
        {
            sample = default(TrackSample);

            if (worldX < 0f || !EnsureLoaded())
            {
                return false;
            }

            int tileX = (int)(worldX / 16f);
            int slice = ((int)worldX % 16) / SliceWidth;
            int baseRow = (int)Math.Floor(referenceY / 16f);

            float bestDistance = float.MaxValue;
            TrackEnd end = TrackEnd.None;

            for (int k = 0; k < 3; k++)
            {
                // own row first, then the row above, then the row below
                int row = baseRow + (k == 0 ? 0 : (k == 1 ? -1 : 1));

                if (!WorldGen.InWorld(tileX, row, 1))
                {
                    continue;
                }

                Tile tile = Framing.GetTileSafely(tileX, row);
                if (!tile.HasUnactuatedTile || tile.TileType != TileID.MinecartTrack)
                {
                    continue;
                }

                int frame = tile.TileFrameX;
                if (frame < 0 || frame >= FrameCount)
                {
                    continue; // not framed yet
                }

                int height = tileHeight[frame][slice];

                if (height >= 0)
                {
                    float surfaceY = row * 16 + height;
                    float distance = Math.Abs(surfaceY - referenceY);

                    if (distance <= MaxSnap && distance < bestDistance)
                    {
                        bestDistance = distance;
                        sample.HasSurface = true;
                        sample.SurfaceY = surfaceY;
                        sample.BoostDirection = trackType[frame] == 2 ? (boostLeft[frame] ? -1 : 1) : 0;
                    }
                }
                else if (end == TrackEnd.None)
                {
                    switch (height)
                    {
                        case -1: end = TrackEnd.Stop; break;
                        case -2: end = TrackEnd.Bounce; break;
                        case -3: end = TrackEnd.Ramp; break;
                        case -4: end = TrackEnd.Open; break;
                    }
                }
            }

            if (!sample.HasSurface)
            {
                sample.End = end;
            }

            return !sample.IsEmpty;
        }
    }
}