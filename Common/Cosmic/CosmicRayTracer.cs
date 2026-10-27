using System;

namespace Factorraria.Common.Cosmic
{
    // What a ray sees in one tile. Pure code (no Terraria types) so it can be tested in tools/cosmic_tests.
    public enum CosmicTileKind : byte
    {
        Empty = 0,      // every tile that is not a gemspark, a cloud or a Lead Brick (terrain, platforms, actuated tiles): the ray flies on
        Gemspark = 1,   // a lit gemspark block; gemsparkColor says which color it absorbs
        Cloud = 2,      // Cloud, Rain Cloud or Snow Cloud
        Solid = 3       // a blocker: a Lead Brick tile, and everything outside the world; the ray dies here
    }

    // The ray tracer asks the world through this. The Terraria implementation is CosmicTileProbe (Main.tile); the tests use a grid.
    public interface ITileProbe
    {
        // Must return Solid for tiles outside the world so rays die at the edge.
        CosmicTileKind Probe(int x, int y, out CosmicColor gemsparkColor);
    }

    public enum CosmicRayEnd : byte
    {
        Range = 0,      // flew the whole range without hitting anything that matters
        Solid = 1,      // stopped by a Lead Brick (or the world edge)
        Absorb = 2,     // absorbed by a gemspark of its own color
        Ionize = 3      // absorbed by a cloud block (the roll succeeded)
    }

    public struct CosmicRayResult
    {
        public CosmicRayEnd End;
        public int TileX;
        public int TileY;
        public float Distance;   // tiles flown until the ray entered that tile (0 = it started inside it)
    }

    // Roadmap section 2 "Ray resolution": a ray is resolved in one pass with a tile walk (Amanatides-Woo DDA), nothing is
    // simulated per tick. Per tile on the way:
    //   empty                       continue
    //   gemspark of the ray's color absorbed (Absorb)
    //   gemspark of another color   continue (A5)
    //   cloud                       roll cloudAbsorbChance, success = Ionize, otherwise continue (A4)
    //   Lead Brick (or world edge)  the ray dies (Solid); every other tile is transparent
    //   range exhausted             the ray dies (Range)
    public static class CosmicRayTracer
    {
        // Direction for a tilt away from straight down, in degrees (positive = towards +X). Y grows downward in Terraria.
        public static void DirectionFromTilt(float tiltDeg, out float dirX, out float dirY)
        {
            float radians = tiltDeg * (MathF.PI / 180f);
            dirX = MathF.Sin(radians);
            dirY = MathF.Cos(radians);
        }

        // Start position in tile units (tile (i, j) covers [i, i+1) x [j, j+1)). The direction does not need to be normalized.
        // rng is only used for cloud rolls (it may be null when cloudAbsorbChance is 0 or 1).
        public static CosmicRayResult Trace(ITileProbe probe, float startX, float startY, float dirX, float dirY,
            CosmicColor color, float range, float cloudAbsorbChance, Random rng)
        {
            CosmicRayResult result = default;
            result.End = CosmicRayEnd.Range;

            double dx = dirX;
            double dy = dirY;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9 || range <= 0f)
                return result;

            dx /= length;
            dy /= length;

            int tx = (int)Math.Floor((double)startX);
            int ty = (int)Math.Floor((double)startY);

            int stepX = dx > 0 ? 1 : -1;
            int stepY = dy > 0 ? 1 : -1;

            double tDeltaX = dx == 0 ? double.PositiveInfinity : 1.0 / Math.Abs(dx);
            double tDeltaY = dy == 0 ? double.PositiveInfinity : 1.0 / Math.Abs(dy);
            double tMaxX = dx == 0 ? double.PositiveInfinity : (dx > 0 ? tx + 1 - (double)startX : (double)startX - tx) / Math.Abs(dx);
            double tMaxY = dy == 0 ? double.PositiveInfinity : (dy > 0 ? ty + 1 - (double)startY : (double)startY - ty) / Math.Abs(dy);

            double t = 0;
            int guard = (int)(range * 2f) + 8;   // a ray crosses at most about range * 2 tiles

            for (int i = 0; i < guard; i++)
            {
                CosmicTileKind kind = probe.Probe(tx, ty, out CosmicColor gemColor);

                if (kind == CosmicTileKind.Solid)
                    return Done(CosmicRayEnd.Solid, tx, ty, t);

                if (kind == CosmicTileKind.Gemspark)
                {
                    if (gemColor == color)
                        return Done(CosmicRayEnd.Absorb, tx, ty, t);
                }
                else if (kind == CosmicTileKind.Cloud)
                {
                    if (cloudAbsorbChance >= 1f || (cloudAbsorbChance > 0f && rng.NextDouble() < cloudAbsorbChance))
                        return Done(CosmicRayEnd.Ionize, tx, ty, t);
                }

                if (tMaxX < tMaxY)
                {
                    t = tMaxX;
                    tMaxX += tDeltaX;
                    tx += stepX;
                }
                else
                {
                    t = tMaxY;
                    tMaxY += tDeltaY;
                    ty += stepY;
                }

                if (t > range)
                    return result;
            }

            return result;
        }

        static CosmicRayResult Done(CosmicRayEnd end, int x, int y, double distance)
        {
            CosmicRayResult result;
            result.End = end;
            result.TileX = x;
            result.TileY = y;
            result.Distance = (float)distance;
            return result;
        }
    }
}
