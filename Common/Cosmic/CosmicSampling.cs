using System;
using System.Collections.Generic;

namespace Factorraria.Common.Cosmic
{
    // One strip (8 tiles wide) of the sky that the server draws new particles in: the rows a particle can start at and still
    // reach a receiver, and how many particles it expects there.
    public struct CosmicSamplingStrip
    {
        public int Strip;       // strip index; x covers [Strip * 8, Strip * 8 + 8)
        public float Y0;        // start rows [Y0, Y1)
        public float Y1;
        public float Density;   // heat weight (sum over all colors) x integral of altitude over [Y0, Y1) x strip width.
                                // Expected particles per step = RateDensity x (step ticks / 60) x Density.
    }

    // Pure maths for the sampler (roadmap section 2, "Statistical equivalence"). No Terraria types; tested in tools/cosmic_tests.
    public static class CosmicSampling
    {
        // Altitude is 1 at the top of the world (y = 0) and falls linearly to 0 at bandBottom. This is its integral over [y0, y1].
        public static float AltitudeIntegral(float y0, float y1, float bandBottom)
        {
            if (bandBottom <= 0f)
                return 0f;

            y0 = Math.Clamp(y0, 0f, bandBottom);
            y1 = Math.Clamp(y1, 0f, bandBottom);
            if (y1 <= y0)
                return 0f;

            return (y1 - y0) - (y1 * y1 - y0 * y0) / (2f * bandBottom);
        }

        // Draws a start row in [y0, y1] with a density proportional to altitude (more particles higher up). u is uniform in [0, 1).
        // Inverse of the cumulative integral: y = B - sqrt((B - y0)^2 - 2 B T u), T = AltitudeIntegral(y0, y1).
        public static float SampleY(float y0, float y1, float bandBottom, double u)
        {
            float total = AltitudeIntegral(y0, y1, bandBottom);
            if (total <= 0f)
                return (y0 + y1) * 0.5f;

            double b = bandBottom;
            double disc = (b - y0) * (b - y0) - 2.0 * b * total * u;
            if (disc < 0.0)
                disc = 0.0;

            float y = (float)(b - Math.Sqrt(disc));
            return Math.Clamp(y, y0, y1);
        }

        // Poisson draw. Knuth's method for small means, a rounded normal for large ones (a mean of 30 or more).
        public static int Poisson(double lambda, Random rng)
        {
            if (lambda <= 0.0)
                return 0;

            if (lambda < 30.0)
            {
                double limit = Math.Exp(-lambda);
                double p = 1.0;
                int k = 0;
                do
                {
                    k++;
                    p *= rng.NextDouble();
                } while (p > limit);
                return k - 1;
            }

            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            double gauss = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            int n = (int)Math.Round(lambda + Math.Sqrt(lambda) * gauss);
            return n < 0 ? 0 : n;
        }

        // Builds the sampling strips. A particle that starts at (sx, sy), tilted at most spreadDeg from straight down, flies at most
        // `range` tiles, so it can only reach a receiver at (rx, ry) if
        //     ry - range <= sy <= ry + 1     (it starts above the receiver and within range)
        //     |sx - rx|  <= range * tan(spread) + 1   (it drifts sideways at most that far)
        // Every strip that holds receivers therefore needs particles from rows [minY - range, maxY + 1] (cut to the band), over its own
        // x range plus `reach` strips either side. Overlapping areas are merged by strip, so a particle start can never be counted
        // twice, and the union is a superset of every start that can matter (the tests check this against brute force).
        public static void Build(CosmicReceiverIndex index, float range, float spreadDeg, float bandBottom, int worldTilesX,
            Func<float, float> weightAtX, List<CosmicSamplingStrip> result)
        {
            result.Clear();
            if (bandBottom <= 0f || range <= 0f || worldTilesX <= 0)
                return;

            float tan = MathF.Tan(Math.Clamp(spreadDeg, 0f, 80f) * (MathF.PI / 180f));
            int reach = (int)MathF.Ceiling((range * tan + 1f) / CosmicReceiverIndex.StripWidth);
            int stripsInWorld = (worldTilesX + CosmicReceiverIndex.StripWidth - 1) / CosmicReceiverIndex.StripWidth;

            Dictionary<int, (float lo, float hi)> areas = new Dictionary<int, (float lo, float hi)>();

            foreach (int s in index.StripIds)
            {
                if (!index.TryGetYRange(s, out int minY, out int maxY))
                    continue;

                float lo = Math.Max(0f, minY - range);
                float hi = Math.Min(bandBottom, maxY + 1f);
                if (lo >= hi)
                    continue;   // too deep: nothing can reach it from the band

                for (int d = -reach; d <= reach; d++)
                {
                    int k = s + d;
                    if (k < 0 || k >= stripsInWorld)
                        continue;

                    if (areas.TryGetValue(k, out (float lo, float hi) have))
                        areas[k] = (Math.Min(have.lo, lo), Math.Max(have.hi, hi));
                    else
                        areas[k] = (lo, hi);
                }
            }

            foreach (KeyValuePair<int, (float lo, float hi)> pair in areas)
            {
                float centerX = (pair.Key + 0.5f) * CosmicReceiverIndex.StripWidth;
                float density = weightAtX(centerX) * AltitudeIntegral(pair.Value.lo, pair.Value.hi, bandBottom) * CosmicReceiverIndex.StripWidth;
                if (density <= 0f)
                    continue;

                result.Add(new CosmicSamplingStrip { Strip = pair.Key, Y0 = pair.Value.lo, Y1 = pair.Value.hi, Density = density });
            }

            result.Sort((a, b) => a.Strip.CompareTo(b.Strip));
        }

        // C7 (client ambient simulation): the rows a local particle may start at so that it can pass through the view rows
        // [viewTop, viewBottom] during its flight: from `range` tiles above the view down to the view's bottom, cut to the particle band.
        // False when nothing that starts in the band can reach the view (the player is too deep or too high).
        public static bool TryAmbientStartRows(float viewTop, float viewBottom, float range, float bandBottom, out float y0, out float y1)
        {
            y0 = Math.Max(0f, viewTop - range);
            y1 = Math.Min(bandBottom, viewBottom);
            return bandBottom > 0f && y1 > y0;
        }

        // C7: expected ambient particles per TICK in a window widthTiles wide, with the same density as the real sky. The server expects
        // rateDensity x (stepTicks / 60) x weight x AltitudeIntegral x stripWidth per sampling strip, so per tick it is the same without
        // stepTicks. boost multiplies it (1 = the same as the real sky).
        public static double AmbientExpectedPerTick(float rateDensity, float boost, float weightSum, float y0, float y1, float bandBottom, float widthTiles)
        {
            if (rateDensity <= 0f || boost <= 0f || widthTiles <= 0f || weightSum <= 0f)
                return 0.0;

            return rateDensity * (double)boost / 60.0 * weightSum * AltitudeIntegral(y0, y1, bandBottom) * widthTiles;
        }

        // A "rig" for the stats: a run of neighboring sampling strips (the strips are sorted by Strip).
        public static int CountRigs(List<CosmicSamplingStrip> strips)
        {
            int rigs = 0;
            for (int i = 0; i < strips.Count; i++)
            {
                if (i == 0 || strips[i].Strip != strips[i - 1].Strip + 1)
                    rigs++;
            }
            return rigs;
        }
    }
}
