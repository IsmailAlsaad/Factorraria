using Factorraria.Common.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria;

namespace Factorraria.Common.Cosmic
{
    // The cosmic radiation simulation (roadmap section 2). Patch C2: receiver index, sampling, ray tracing and the event queue.
    // Outcomes (a ray reaching a gemspark or a cloud block) are only COUNTED for now; C3 and C4 act on them in Complete().
    //
    // Authority: server and single-player only. CosmicRadiationSystem calls Update() from PostUpdateWorld on those two and never on
    // a multiplayer client. Nothing here is saved; everything is rebuilt after a world load. Nothing here touches client-only code.
    //
    // How it works (nothing is simulated per tick):
    //   1. A slow scan keeps CosmicReceiverIndex in step with the world (lit gemsparks and clouds in the rows a ray can reach);
    //      the place/kill hooks in CosmicReceivers.cs keep it exact in between.
    //   2. Every SampleStepTicks the sampler draws a Poisson number of new particles above each strip that has receivers
    //      (CosmicSampling.Build), at heat x altitude weighted positions, and queues one RayStart event for each.
    //   3. Each tick the queue releases what is due. A RayStart is traced in one pass (CosmicRayTracer) and, if it hits a receiver,
    //      queues an Absorb or Ionize event for the tick it would arrive (start + distance / ParticleSpeed).
    public class CosmicSimulation
    {
        // How far above the target tile /fcosmic spawn starts its ray.
        public const float DebugRayHeight = 30f;

        public readonly CosmicReceiverIndex Index = new CosmicReceiverIndex();
        public readonly CosmicEventHeap Heap = new CosmicEventHeap();

        readonly List<CosmicSamplingStrip> sampling = new List<CosmicSamplingStrip>();
        readonly CosmicTileProbe probe = new CosmicTileProbe();
        readonly List<(int x, int y, byte code)> found = new List<(int x, int y, byte code)>();
        readonly Random rng = new Random();

        // ------------------------------------------------------------ state
        public long Now { get; private set; }
        public string Fault { get; private set; }
        long nextSampleTick;

        // scan
        int scanStrip;
        public bool InitialScanDone { get; private set; }
        public int ScanPasses { get; private set; }

        // sampling cache (rebuilt when the index or any value it depends on changes)
        int cacheVersion = -1;
        float cacheRange, cacheSpread, cacheBand, cacheFloor, cacheSigma;
        int cacheWorld, cacheHeatKey;
        public float BandBottom { get; private set; }
        public int SamplingStrips => sampling.Count;
        public int Rigs { get; private set; }
        public float SamplingAreaTiles { get; private set; }
        public float DensitySum { get; private set; }

        // ------------------------------------------------------------ counters (what /fcosmic stats shows)
        public long RaysSampled, RaysTraced, DeadSolid, DeadRange, Dropped, DebugRays;
        public readonly long[] Absorbed = new long[7];
        public readonly long[] Ionized = new long[7];
        public int PeakQueue;
        public float RaysPerSecond;
        public double AvgMsPerTick, MaxMsPerTick;
        public readonly CosmicQueuedEvent[] Recent = new CosmicQueuedEvent[8];
        public int RecentCount;

        long rateWindowStart, raysAtWindowStart, windowTicks;
        double windowMs;

        public void Reset()
        {
            Index.MaxY = int.MaxValue;
            Index.Clear();
            Heap.Clear();
            sampling.Clear();
            Now = 0;
            nextSampleTick = 0;
            scanStrip = 0;
            InitialScanDone = false;
            ScanPasses = 0;
            cacheVersion = -1;
            Rigs = 0;
            SamplingAreaTiles = 0f;
            DensitySum = 0f;
            Fault = null;
            ResetCounters();
        }

        public void ResetCounters()
        {
            RaysSampled = RaysTraced = DeadSolid = DeadRange = Dropped = DebugRays = 0;
            Array.Clear(Absorbed, 0, Absorbed.Length);
            Array.Clear(Ionized, 0, Ionized.Length);
            PeakQueue = Heap.Count;
            RaysPerSecond = 0f;
            AvgMsPerTick = MaxMsPerTick = 0.0;
            RecentCount = 0;
            rateWindowStart = Now;
            raysAtWindowStart = 0;
            windowTicks = 0;
            windowMs = 0.0;
        }

        // Starts the fast scan again from the first strip (the slow scan keeps running by itself; this is for /fcosmic rescan).
        public void RequestRescan()
        {
            scanStrip = 0;
            InitialScanDone = false;
        }

        public int StripsInWorld => (Main.maxTilesX + CosmicReceiverIndex.StripWidth - 1) / CosmicReceiverIndex.StripWidth;

        public int ScanPercent => InitialScanDone ? 100 : Math.Min(99, scanStrip * 100 / Math.Max(1, StripsInWorld));

        // ------------------------------------------------------------ tile hooks (server and single-player)
        public void OnReceiverPlaced(int x, int y, byte code)
        {
            if (code != CosmicTileCodes.None)
                Index.Add(x, y, code);
        }

        public void OnReceiverRemoved(int x, int y)
        {
            Index.Remove(x, y);
        }

        // ------------------------------------------------------------ the tick
        public void Update(CosmicRadiationSystem s)
        {
            if (Fault != null || !s.Heatmap.Ready || Main.maxTilesX <= 0 || Main.maxTilesY <= 0)
                return;

            long start = Stopwatch.GetTimestamp();

            try
            {
                Now++;

                float band = Math.Max(1f, (float)(Main.worldSurface * s.SpaceBottomFraction));
                BandBottom = band;
                Index.MaxY = Math.Min((int)(band + s.ParticleRange) + 2, Main.maxTilesY - 1);

                ScanStep(s);

                if (Now >= nextSampleTick)
                {
                    SampleStep(s, band);
                    nextSampleTick = Now + Math.Max(1, s.SampleStepTicks);
                }

                ProcessQueue(s);
            }
            catch (Exception e)
            {
                // Stop instead of throwing every tick. /fcosmic stats shows the message; leaving and re-entering the world resets it.
                Fault = e.GetType().Name + ": " + e.Message;
                FactorrariaNet.Log("[Cosmic] the simulation stopped after an error: " + e);
                return;
            }

            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            windowMs += ms;
            windowTicks++;
            if (ms > MaxMsPerTick)
                MaxMsPerTick = ms;

            if (Now - rateWindowStart >= 60)
            {
                RaysPerSecond = (RaysTraced - raysAtWindowStart) * 60f / (Now - rateWindowStart);
                raysAtWindowStart = RaysTraced;
                rateWindowStart = Now;
                AvgMsPerTick = windowMs / Math.Max(1, windowTicks);
                windowMs = 0.0;
                windowTicks = 0;
            }
        }

        // ------------------------------------------------------------ 1. the receiver scan
        void ScanStep(CosmicRadiationSystem s)
        {
            int total = StripsInWorld;
            int budget = InitialScanDone ? Math.Max(0, s.ResyncStripsPerTick) : Math.Max(1, s.ScanStripsPerTick);
            int maxY = Math.Min(Index.MaxY, Main.maxTilesY - 1);

            for (int i = 0; i < budget; i++)
            {
                ScanStrip(scanStrip, maxY);
                scanStrip++;

                if (scanStrip >= total)
                {
                    scanStrip = 0;
                    ScanPasses++;
                    if (!InitialScanDone)
                    {
                        InitialScanDone = true;
                        FactorrariaNet.Log("[Cosmic] receiver scan finished: " + Index.Total + " receivers (" + Index.CloudCount + " cloud blocks), " + Index.StripCount + " strips");
                    }
                }
            }
        }

        void ScanStrip(int strip, int maxY)
        {
            found.Clear();

            int x0 = strip * CosmicReceiverIndex.StripWidth;
            int x1 = Math.Min(x0 + CosmicReceiverIndex.StripWidth, Main.maxTilesX);

            for (int x = x0; x < x1; x++)
            {
                for (int y = 0; y <= maxY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasUnactuatedTile)
                        continue;

                    byte code = CosmicTileCodes.CodeOf(tile.TileType);
                    if (code != CosmicTileCodes.None)
                        found.Add((x, y, code));
                }
            }

            Index.ReplaceStrip(strip, found);
        }

        // ------------------------------------------------------------ 2. the sampler
        static float TotalWeight(CosmicHeatmap heat, int x, int worldTiles, float floor, float sigma)
        {
            float sum = 0f;
            for (int c = 0; c < CosmicColors.Count; c++)
                sum += heat.Weight((CosmicColor)c, x, worldTiles, floor, sigma);
            return sum;
        }

        static int HeatKey(CosmicHeatmap heat)
        {
            int key = 0;
            for (int i = 0; i < 7; i++)
                key = key * 8 + heat.Order[i];
            return key;
        }

        void RebuildSamplingIfNeeded(CosmicRadiationSystem s, float band)
        {
            int heatKey = HeatKey(s.Heatmap);
            if (cacheVersion == Index.Version && cacheRange == s.ParticleRange && cacheSpread == s.SpawnSpreadDeg && cacheBand == band
                && cacheFloor == s.HeatFloor && cacheSigma == s.HeatSigmaFraction && cacheWorld == Main.maxTilesX && cacheHeatKey == heatKey)
                return;

            CosmicHeatmap heat = s.Heatmap;
            float floor = s.HeatFloor;
            float sigma = s.HeatSigmaFraction;
            int world = Main.maxTilesX;

            CosmicSampling.Build(Index, s.ParticleRange, s.SpawnSpreadDeg, band, world, x => TotalWeight(heat, (int)x, world, floor, sigma), sampling);

            cacheVersion = Index.Version;
            cacheRange = s.ParticleRange;
            cacheSpread = s.SpawnSpreadDeg;
            cacheBand = band;
            cacheFloor = floor;
            cacheSigma = sigma;
            cacheWorld = world;
            cacheHeatKey = heatKey;

            Rigs = CosmicSampling.CountRigs(sampling);

            float area = 0f;
            float density = 0f;
            foreach (CosmicSamplingStrip strip in sampling)
            {
                area += (strip.Y1 - strip.Y0) * CosmicReceiverIndex.StripWidth;
                density += strip.Density;
            }
            SamplingAreaTiles = area;
            DensitySum = density;
        }

        void SampleStep(CosmicRadiationSystem s, float band)
        {
            RebuildSamplingIfNeeded(s, band);
            if (sampling.Count == 0)
                return;

            int step = Math.Max(1, s.SampleStepTicks);
            double perDensity = s.RateDensity * step / 60.0;
            int world = Main.maxTilesX;

            foreach (CosmicSamplingStrip strip in sampling)
            {
                int count = CosmicSampling.Poisson(perDensity * strip.Density, rng);

                for (int k = 0; k < count; k++)
                {
                    if (Heap.Count >= s.MaxQueuedEvents)
                    {
                        Dropped += count - k;
                        break;
                    }

                    float x = (strip.Strip + (float)rng.NextDouble()) * CosmicReceiverIndex.StripWidth;
                    if (x >= world)
                        continue;

                    float y = CosmicSampling.SampleY(strip.Y0, strip.Y1, band, rng.NextDouble());
                    float tilt = ((float)rng.NextDouble() * 2f - 1f) * s.SpawnSpreadDeg;
                    CosmicRayTracer.DirectionFromTilt(tilt, out float dirX, out float dirY);

                    Heap.Push(new CosmicQueuedEvent
                    {
                        Tick = Now + rng.Next(step),
                        Kind = CosmicQueuedKind.RayStart,
                        Color = s.Heatmap.Pick((int)x, world, s.HeatFloor, s.HeatSigmaFraction, rng),
                        X = x,
                        Y = y,
                        DirX = dirX,
                        DirY = dirY
                    });
                    RaysSampled++;
                }
            }
        }

        // ------------------------------------------------------------ 3. the event queue
        void ProcessQueue(CosmicRadiationSystem s)
        {
            int rayBudget = Math.Max(1, s.MaxRaysPerTick);

            while (Heap.TryPeek(out CosmicQueuedEvent next) && next.Tick <= Now)
            {
                if (next.Kind == CosmicQueuedKind.RayStart)
                {
                    if (rayBudget <= 0)
                        break;   // the rest wait for the next tick

                    rayBudget--;
                    Heap.Pop();
                    ResolveRay(s, next, out _);
                }
                else
                {
                    Heap.Pop();
                    Complete(next);
                }
            }

            if (Heap.Count > PeakQueue)
                PeakQueue = Heap.Count;
        }

        // Traces one ray and, when it hits a receiver, queues the outcome for the tick it arrives. Returns the trace result.
        CosmicRayResult ResolveRay(CosmicRadiationSystem s, in CosmicQueuedEvent ev, out bool queued)
        {
            queued = false;
            RaysTraced++;

            CosmicRayResult r = CosmicRayTracer.Trace(probe, ev.X, ev.Y, ev.DirX, ev.DirY, ev.Color, s.ParticleRange, s.CloudAbsorbChance, rng);

            if (r.End == CosmicRayEnd.Solid)
            {
                DeadSolid++;
                return r;
            }

            if (r.End == CosmicRayEnd.Range)
            {
                DeadRange++;
                return r;
            }

            if (Heap.Count >= s.MaxQueuedEvents)
            {
                Dropped++;
                return r;
            }

            long travelTicks = (long)Math.Ceiling(r.Distance / Math.Max(0.01f, s.ParticleSpeed));

            Heap.Push(new CosmicQueuedEvent
            {
                Tick = ev.Tick + travelTicks,
                Kind = r.End == CosmicRayEnd.Absorb ? CosmicQueuedKind.Absorb : CosmicQueuedKind.Ionize,
                Color = ev.Color,
                TileX = r.TileX,
                TileY = r.TileY
            });
            queued = true;
            return r;
        }

        // A ray has arrived. C2 only counts it. C3 (gemspark: absorb and re-emit) and C4 (cloud: ionize) act here.
        void Complete(in CosmicQueuedEvent ev)
        {
            if (ev.Kind == CosmicQueuedKind.Absorb)
                Absorbed[(int)ev.Color]++;
            else if (ev.Kind == CosmicQueuedKind.Ionize)
                Ionized[(int)ev.Color]++;

            Recent[RecentCount % Recent.Length] = ev;
            RecentCount++;
        }

        // ------------------------------------------------------------ debug
        // /fcosmic spawn: one ray of this color, straight down, starting DebugRayHeight tiles above the target tile (or at the top of the world).
        public string FireDebugRay(CosmicRadiationSystem s, CosmicColor color, int tx, int ty)
        {
            if (Fault != null)
                return "the simulation stopped after an error: " + Fault;

            if (!s.Heatmap.Ready || Now == 0)
                return "the simulation has not started yet (it starts on the first world tick)";

            float sx = tx + 0.5f;
            float sy = Math.Max(0.5f, ty + 0.5f - DebugRayHeight);

            CosmicQueuedEvent ev = new CosmicQueuedEvent
            {
                Tick = Now,
                Kind = CosmicQueuedKind.RayStart,
                Color = color,
                X = sx,
                Y = sy,
                DirX = 0f,
                DirY = 1f
            };

            DebugRays++;
            CosmicRayResult r = ResolveRay(s, ev, out bool queued);

            string head = color + " ray from (" + (int)sx + ", " + (int)sy + ") straight down: ";
            long ticks = (long)Math.Ceiling(r.Distance / Math.Max(0.01f, s.ParticleSpeed));
            string tail = " after " + r.Distance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " tiles";

            switch (r.End)
            {
                case CosmicRayEnd.Absorb:
                    return head + "absorbed by the " + color + " gemspark at (" + r.TileX + ", " + r.TileY + ")" + tail + ", arrives in " + ticks + " ticks" + (queued ? "" : " (the queue is full, the outcome was dropped)");
                case CosmicRayEnd.Ionize:
                    return head + "hit a cloud block at (" + r.TileX + ", " + r.TileY + ")" + tail + ", arrives in " + ticks + " ticks" + (queued ? "" : " (the queue is full, the outcome was dropped)");
                case CosmicRayEnd.Solid:
                    return head + "stopped by a solid block at (" + r.TileX + ", " + r.TileY + ")" + tail;
                default:
                    return head + "flew the full range of " + s.ParticleRange.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " tiles without hitting a receiver";
            }
        }
    }
}
