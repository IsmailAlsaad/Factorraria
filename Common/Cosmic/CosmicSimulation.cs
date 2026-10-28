using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Common.Systems;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria;
using Terraria.DataStructures;

namespace Factorraria.Common.Cosmic
{
    // The cosmic radiation simulation (roadmap section 2). Patch C2: receiver index, sampling, ray tracing and the event queue.
    // Outcomes (a ray reaching a gemspark or a cloud block) arrive in Complete(): C3 (gemspark absorb and re-emit) is in OnAbsorbed(); C4 (cloud ionize and discharge) is in OnIonized() and OnDischarge(); C5 (strike and grid surge) is in Strike().
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

        // C4: the ionized cloud blocks, their expiry queue and the merged-cluster discharge rules (pure code in CosmicIonizedField).
        // Server and single-player only, never saved (an ionized block only lives about a second).
        public readonly CosmicIonizedField IonField = new CosmicIonizedField();
        readonly Func<int, int, bool> cloudTest;

        public CosmicSimulation()
        {
            cloudTest = IsCloudAt;
        }

        // Is there an unactuated cloud tile here right now?
        bool IsCloudAt(int x, int y) => probe.Probe(x, y, out _) == CosmicTileKind.Cloud;

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

        // C3: child rays created per color, absorptions that ended the chain at Red, absorptions whose gemspark was broken while the
        // particle was flying, and the angles of the last cascade (what /fcosmic stats prints).
        public readonly long[] Reemitted = new long[7];
        public long ChainEnds, Vanished;
        public bool HasLastCascade;
        public CosmicColor LastCascadeFrom;
        public float LastChildDegA, LastChildDegB;

        // C4: ionization and discharge. LogDischarges prints a line per discharge (/fcosmic log); it survives a counter reset.
        public long BlocksIonized, HitsAdded, HitsRefractory, CloudGone, Discharges, DischargedBlocks;
        public double BiggestEnergy;
        public readonly CosmicDischarge[] RecentDischarges = new CosmicDischarge[5];
        public int RecentDischargeCount;
        public bool LogDischarges;

        // C5: strikes. The sink gets every Strike and Spit event (C6 swaps in the real sinks); the counters are what /fcosmic stats shows.
        // C6: the router feeds the local screen (single-player, a host that plays) and/or the network (server); see Common/Net/CosmicEventSink.cs.
        public ICosmicEventSink Sink = new CosmicEventRouter();
        public long Strikes, Spits, SurgesClamped;
        public double BiggestSurge;
        public bool HasLastStrike;
        public string LastStrikeMachine;
        public int LastStrikeX, LastStrikeY;
        public long LastStrikeTick;
        public double LastStrikeEnergy;
        public float LastStrikePower, LastStrikeDistance;
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
            IonField.Clear();
            (Sink as CosmicEventRouter)?.Clear();
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
            Array.Clear(Reemitted, 0, Reemitted.Length);
            ChainEnds = Vanished = 0;
            HasLastCascade = false;
            BlocksIonized = HitsAdded = HitsRefractory = CloudGone = Discharges = DischargedBlocks = 0;
            BiggestEnergy = 0.0;
            RecentDischargeCount = 0;
            Strikes = Spits = SurgesClamped = 0;
            BiggestSurge = 0.0;
            HasLastStrike = false;
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
            IonField.Remove(x, y);
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

                // C6: a network sink sends what the tick produced (one packet of each kind per nearby player).
                Sink?.EndTick();
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
                    Complete(s, next);
                }
            }

            // C4: ionized blocks whose time is up discharge now, each merged cluster once.
            while (IonField.PopDischarge(Now, s.RefractoryTicks, cloudTest, out CosmicDischarge discharge))
                OnDischarge(s, discharge);

            if (Heap.Count > PeakQueue)
                PeakQueue = Heap.Count;
        }

        // Traces one ray and, when it hits a receiver, queues the outcome for the tick it arrives. Returns the trace result.
        CosmicRayResult ResolveRay(CosmicRadiationSystem s, in CosmicQueuedEvent ev, out bool queued)
        {
            queued = false;
            RaysTraced++;

            CosmicRayResult r = CosmicRayTracer.Trace(probe, ev.X, ev.Y, ev.DirX, ev.DirY, ev.Color, s.ParticleRange, s.CloudAbsorbChance, rng);

            // C6: every traced ray is drawn from start to end, whatever its outcome (a Range end has Distance 0, it flies the full range).
            Sink?.Ray(new CosmicRayEvent
            {
                Tick = Now,
                Color = ev.Color,
                StartX = ev.X,
                StartY = ev.Y,
                DirX = ev.DirX,
                DirY = ev.DirY,
                Length = r.End == CosmicRayEnd.Range ? s.ParticleRange : r.Distance,
                End = r.End == CosmicRayEnd.Absorb ? CosmicRayEndKind.Absorb : r.End == CosmicRayEnd.Ionize ? CosmicRayEndKind.Ionize : CosmicRayEndKind.None
            });

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
                TileY = r.TileY,
                DirX = ev.DirX,   // C3 rotates this one for the child rays
                DirY = ev.DirY
            });
            queued = true;
            return r;
        }

        // A ray has arrived. C3 (gemspark: absorb and re-emit) acts in OnAbsorbed; C4 (cloud: ionize) acts in OnIonized.
        void Complete(CosmicRadiationSystem s, in CosmicQueuedEvent ev)
        {
            if (ev.Kind == CosmicQueuedKind.Absorb)
                OnAbsorbed(s, ev);
            else if (ev.Kind == CosmicQueuedKind.Ionize)
            {
                Ionized[(int)ev.Color]++;
                OnIonized(s, ev);
            }

            Recent[RecentCount % Recent.Length] = ev;
            RecentCount++;
        }

        // C3. A ray reached a gemspark of its own color. The gemspark has to still be there (the player may have broken it while the
        // particle was flying). It absorbs the particle and, unless the color is Red, re-emits two rays of the next color from the
        // middle of the tile at the arrival tick: the incoming direction rotated by +a and by -b degrees, a and b random between
        // ReemitAngleMinDeg and ReemitAngleMaxDeg. The children are pushed into the heap as RayStart events, so a cascade runs through
        // the queue and never recurses (the chain ends at Red, so one White ray makes at most 127 rays). The caps are MaxRaysPerTick
        // (the rest wait a tick, see ProcessQueue) and MaxQueuedEvents (children beyond it are dropped and counted in Dropped).
        // Children are never sampled: they are only created here.
        void OnAbsorbed(CosmicRadiationSystem s, in CosmicQueuedEvent ev)
        {
            CosmicTileKind kind = probe.Probe(ev.TileX, ev.TileY, out CosmicColor tileColor);
            if (kind != CosmicTileKind.Gemspark || tileColor != ev.Color)
            {
                Vanished++;
                return;
            }

            Absorbed[(int)ev.Color]++;
            Sink?.Absorb(new CosmicAbsorbEvent { Tick = Now, Color = ev.Color, TileX = ev.TileX, TileY = ev.TileY });

            if (!CosmicColors.HasNext(ev.Color))
            {
                ChainEnds++;
                return;
            }

            CosmicColor next = CosmicColors.Next(ev.Color);
            float low = Math.Max(0f, Math.Min(s.ReemitAngleMinDeg, s.ReemitAngleMaxDeg));
            float high = Math.Max(low, Math.Max(s.ReemitAngleMinDeg, s.ReemitAngleMaxDeg));
            float degA = low + (float)rng.NextDouble() * (high - low);
            float degB = low + (float)rng.NextDouble() * (high - low);

            for (int child = 0; child < 2; child++)
            {
                if (Heap.Count >= s.MaxQueuedEvents)
                {
                    Dropped++;
                    continue;
                }

                CosmicRayTracer.Rotate(ev.DirX, ev.DirY, child == 0 ? degA : -degB, out float dirX, out float dirY);

                Heap.Push(new CosmicQueuedEvent
                {
                    Tick = ev.Tick,
                    Kind = CosmicQueuedKind.RayStart,
                    Color = next,
                    X = ev.TileX + 0.5f,
                    Y = ev.TileY + 0.5f,
                    DirX = dirX,
                    DirY = dirY
                });
                Reemitted[(int)next]++;
            }

            HasLastCascade = true;
            LastCascadeFrom = ev.Color;
            LastChildDegA = degA;
            LastChildDegB = degB;
        }

        // C4. A ray reached a cloud block (the absorb roll already succeeded when it was traced). CosmicIonizedField has the rules:
        // a block that is already ionized takes the particle's base energy (A4); otherwise a flood fill ionizes N connected cloud
        // blocks that are not ionized yet (N random in the color's range) and they share the base energy; a cloud broken while the
        // particle flew does nothing. The ionized blocks live IonizedLifetimeTicks, then the whole connected cluster discharges once.
        void OnIonized(CosmicRadiationSystem s, in CosmicQueuedEvent ev)
        {
            int c = (int)ev.Color;
            CosmicHitResult result = IonField.Hit(Now, ev.TileX, ev.TileY, ev.Color, s.BaseEnergy[c], s.Multiplier[c], s.ClusterMin[c], s.ClusterMax[c],
                s.IonizedLifetimeTicks, cloudTest, rng, out int made);

            if (result == CosmicHitResult.Ionized)
            {
                BlocksIonized += made;
                Sink?.Ionize(new CosmicIonizeEvent { Tick = Now, Color = ev.Color, LifeTicks = s.IonizedLifetimeTicks, Tiles = IonField.LastTiles });
            }
            else if (result == CosmicHitResult.Added)
                HitsAdded++;
            else if (result == CosmicHitResult.Refractory)
                HitsRefractory++;
            else
                CloudGone++;
        }

        // C4. A merged cluster discharged: it is counted and logged, then (C5) the thunder looks for a machine to strike.
        // The energy is a double and is never capped here (roadmap risk "merged-cluster explosion"); Strike() clamps what it sends to the grid.
        void OnDischarge(CosmicRadiationSystem s, in CosmicDischarge d)
        {
            Discharges++;
            DischargedBlocks += d.Blocks;

            if (d.Energy > BiggestEnergy)
                BiggestEnergy = d.Energy;

            RecentDischarges[RecentDischargeCount % RecentDischarges.Length] = d;
            RecentDischargeCount++;

            // C6: IonField.LastTiles still holds the blocks of this cluster (nothing has touched the field since PopDischarge).
            Sink?.Discharge(new CosmicDischargeEvent { Tick = Now, CenterX = d.CenterX, CenterY = d.CenterY, Energy = d.Energy, Tiles = IonField.LastTiles });

            StrikeResult strike = Strike(s, d.Energy, d.CenterX, d.CenterY);

            if (!LogDischarges)
                return;

            string text = "[Cosmic] discharge: energy " + EnergyText(d.Energy) + ", " + d.Blocks + " blocks, base energy " + EnergyText(d.BaseSum) + ", center ("
                + (int)d.CenterX + ", " + (int)d.CenterY + "), tick " + d.Tick + StrikeLogText(s, strike);

            if (Main.dedServ)
                FactorrariaNet.Log(text);
            else
                Main.NewText(text, 255, 225, 90);
        }

        // ------------------------------------------------------------ C5: strike and grid surge
        // The outcome of one strike attempt (what the log line and /fcosmic strike print).
        public struct StrikeResult
        {
            public bool Struck;          // false = no machine in range, the thunder spat into the sky
            public BaseMachine Target;
            public float Distance;       // tiles from the thunder to the machine's center
            public float Power;          // surge power added to the machine (after clamping)
            public bool Clamped;
        }

        // C5. A discharge of `energy` happened at (centerX, centerY), in tile units. Roadmap section 2 "Strike" and assumptions A7 and A8:
        // - the nearest electric machine (consumer or producer, poles included, they are consumers) within StrikeRadiusTiles of the center is struck:
        //   a surge of energy x SurgePowerPerEnergy (clamped to MaxSurgePower) is added to that machine for SurgeDurationTicks, and PowerNetwork.Tick
        //   adds it to the supply of the machine's grid; a Strike event goes to the sink;
        // - no machine in range: a Spit event with a random direction goes to the sink and nothing else happens.
        // Authority: server and single-player only (the simulation never runs on a client). Nothing here is saved.
        public StrikeResult Strike(CosmicRadiationSystem s, double energy, float centerX, float centerY)
        {
            StrikeResult result = default;
            BaseMachine target = FindStrikeTarget(centerX, centerY, s.StrikeRadiusTiles, out float distance);

            if (target == null)
            {
                Spits++;

                double angle = rng.NextDouble() * Math.PI * 2.0;
                Sink?.Spit(new CosmicSpitEvent { Tick = Now, FromX = centerX, FromY = centerY, DirX = (float)Math.Cos(angle), DirY = (float)Math.Sin(angle), Energy = energy });
                return result;
            }

            float power = CosmicStrikeMath.SurgePower(energy, s.SurgePowerPerEnergy, s.MaxSurgePower, out bool clamped);
            target.AddSurge(power, s.SurgeDurationTicks, s.MaxSurgePower);

            Strikes++;
            if (clamped)
                SurgesClamped++;
            if (power > BiggestSurge)
                BiggestSurge = power;

            HasLastStrike = true;
            LastStrikeMachine = target.GetType().Name;
            LastStrikeX = target.Position.X;
            LastStrikeY = target.Position.Y;
            LastStrikeTick = Now;
            LastStrikeEnergy = energy;
            LastStrikePower = power;
            LastStrikeDistance = distance;

            Sink?.Strike(new CosmicStrikeEvent
            {
                Tick = Now,
                FromX = centerX,
                FromY = centerY,
                ToX = target.MachineCenter.X / 16f,
                ToY = target.MachineCenter.Y / 16f,
                Energy = energy,
                SurgePower = power
            });

            result.Struck = true;
            result.Target = target;
            result.Distance = distance;
            result.Power = power;
            result.Clamped = clamped;
            return result;
        }

        // The nearest electric machine whose center is within `radius` tiles of the point, or null. Walks PowerGridSystem.AllMachines, which holds
        // every IElectricConsumer and IElectricProducer entity, poles included (roadmap V6). Cheap: one squared distance per machine per discharge.
        BaseMachine FindStrikeTarget(float centerX, float centerY, float radius, out float distance)
        {
            distance = 0f;
            HashSet<TileEntity> machines = PowerGridSystem.AllMachines;

            if (machines.Count == 0 || radius <= 0f)
                return null;

            BaseMachine best = null;
            float limitSq = radius * radius;

            foreach (TileEntity entity in machines)
            {
                if (entity is not BaseMachine machine || !(machine is IElectricConsumer || machine is IElectricProducer) || !machine.InitializeGeometry())
                    continue;

                if (CosmicStrikeMath.IsCloser(centerX, centerY, machine.MachineCenter.X / 16f, machine.MachineCenter.Y / 16f, limitSq, out float distanceSq))
                {
                    best = machine;
                    limitSq = distanceSq;
                    distance = (float)Math.Sqrt(distanceSq);
                }
            }

            return best;
        }

        // How many machines have a surge running right now (what /fcosmic stats shows).
        public int ActiveSurges()
        {
            int count = 0;

            foreach (TileEntity entity in PowerGridSystem.AllMachines)
            {
                if (entity is BaseMachine machine && machine.SurgeTicksLeft > 0)
                    count++;
            }

            return count;
        }

        // The end of a discharge log line: what the thunder did.
        public static string StrikeLogText(CosmicRadiationSystem s, in StrikeResult r)
        {
            if (!r.Struck)
                return " -> no machine within " + s.StrikeRadiusTiles.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " tiles: spit";

            return " -> struck " + r.Target.GetType().Name + " at (" + r.Target.Position.X + ", " + r.Target.Position.Y + "), "
                + r.Distance.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " tiles away, surge " + EnergyText(r.Power) + " for " + s.SurgeDurationTicks + " ticks"
                + (r.Clamped ? " (clamped)" : "");
        }

        // Energy numbers can be tiny or astronomically large, so big ones are written in scientific notation.
        public static string EnergyText(double energy)
        {
            return energy < 1e6
                ? energy.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                : energy.ToString("0.###e+0", System.Globalization.CultureInfo.InvariantCulture);
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
                    return head + "blocked by a lead brick (or the world edge) at (" + r.TileX + ", " + r.TileY + ")" + tail;
                default:
                    return head + "flew the full range of " + s.ParticleRange.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " tiles without hitting a receiver";
            }
        }
    }
}
