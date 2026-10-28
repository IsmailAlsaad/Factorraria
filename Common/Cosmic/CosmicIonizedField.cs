using System;
using System.Collections.Generic;

namespace Factorraria.Common.Cosmic
{
    // One ionized cloud block. BaseShare is the part of the absorbed base energy this block carries (A1).
    public struct CosmicIonizedBlock
    {
        public CosmicColor Color;
        public float Multiplier;
        public float BaseShare;
        public long ExpireTick;
    }

    // One merged cluster that discharged. Energy follows A1 (a double, see CosmicClusterMath.Energy).
    public struct CosmicDischarge
    {
        public long Tick;
        public double Energy;
        public double BaseSum;
        public int Blocks;
        public float CenterX;   // tile units
        public float CenterY;
    }

    public enum CosmicHitResult : byte
    {
        Ionized = 0,      // a new cluster was ionized
        Added = 1,        // the block was already ionized: the particle's base energy was added to it (A4)
        Refractory = 2,   // the block is cooling down after a discharge: the particle is absorbed and nothing happens (A9)
        NotCloud = 3      // the cloud block was broken while the particle was flying: nothing happens
    }

    // The ionized cloud blocks and their expiry queue. Pure code (the world is asked through a Func), tested in tools/cosmic_tests.
    // Roadmap section 2 "Ionization and discharge": Hit() ionizes, PopDischarge() discharges a whole connected cluster once.
    public sealed class CosmicIonizedField
    {
        readonly Dictionary<long, CosmicIonizedBlock> blocks = new Dictionary<long, CosmicIonizedBlock>();
        readonly Dictionary<long, long> refractory = new Dictionary<long, long>();   // tile -> first tick it may be ionized again
        readonly CosmicEventHeap expiry = new CosmicEventHeap();                     // (ExpireTick, tile); stale entries are skipped when popped
        readonly List<(int x, int y)> scratch = new List<(int x, int y)>();
        readonly Func<int, int, bool> canIonize;
        readonly Func<int, int, bool> isIonizedCloud;

        // set only for the duration of one call, so the two delegates above need no allocation per call
        Func<int, int, bool> currentIsCloud;
        long currentNow;

        public CosmicIonizedField()
        {
            canIonize = CanIonize;
            isIonizedCloud = IsIonizedCloud;
        }

        public int Count => blocks.Count;

        public void Clear()
        {
            blocks.Clear();
            refractory.Clear();
            expiry.Clear();
        }

        public bool TryGet(int x, int y, out CosmicIonizedBlock block) => blocks.TryGetValue(CosmicClusterMath.Key(x, y), out block);

        // The cloud block at (x, y) is gone (mined or replaced): it stops being ionized and its share of the energy is lost.
        // Its expiry entry stays in the queue and is skipped when it comes up.
        public void Remove(int x, int y)
        {
            blocks.Remove(CosmicClusterMath.Key(x, y));
        }

        bool CooledDown(int x, int y)
        {
            if (refractory.Count == 0)
                return true;

            long key = CosmicClusterMath.Key(x, y);
            if (!refractory.TryGetValue(key, out long until))
                return true;

            if (until > currentNow)
                return false;

            refractory.Remove(key);
            return true;
        }

        bool CanIonize(int x, int y)
        {
            return currentIsCloud(x, y) && !blocks.ContainsKey(CosmicClusterMath.Key(x, y)) && CooledDown(x, y);
        }

        bool IsIonizedCloud(int x, int y)
        {
            return blocks.ContainsKey(CosmicClusterMath.Key(x, y)) && currentIsCloud(x, y);
        }

        // A ray of `color` reached the cloud block at (x, y) at tick `now`.
        // - the block is already ionized: its BaseShare grows by baseEnergy (A4), the expiry time does not change;
        // - otherwise a flood fill ionizes n connected cloud blocks that are not ionized and not cooling down (n random between
        //   minBlocks and maxBlocks, at least 1). They share baseEnergy equally and expire at now + lifetimeTicks.
        public CosmicHitResult Hit(long now, int x, int y, CosmicColor color, float baseEnergy, float multiplier, int minBlocks, int maxBlocks,
            int lifetimeTicks, Func<int, int, bool> isCloud, Random rng, out int made)
        {
            made = 0;

            if (!isCloud(x, y))
                return CosmicHitResult.NotCloud;

            long key = CosmicClusterMath.Key(x, y);
            if (blocks.TryGetValue(key, out CosmicIonizedBlock existing))
            {
                existing.BaseShare += baseEnergy;
                blocks[key] = existing;
                return CosmicHitResult.Added;
            }

            currentNow = now;
            if (!CooledDown(x, y))
                return CosmicHitResult.Refractory;

            int low = Math.Max(1, Math.Min(minBlocks, maxBlocks));
            int high = Math.Max(low, Math.Max(minBlocks, maxBlocks));
            int wanted = low == high ? low : rng.Next(low, high + 1);

            currentIsCloud = isCloud;
            int count = CosmicClusterMath.Fill(x, y, wanted, canIonize, scratch);
            currentIsCloud = null;

            if (count == 0)
                return CosmicHitResult.NotCloud;

            float share = baseEnergy / count;
            long expireTick = now + Math.Max(1, lifetimeTicks);

            foreach ((int bx, int by) in scratch)
            {
                blocks[CosmicClusterMath.Key(bx, by)] = new CosmicIonizedBlock { Color = color, Multiplier = multiplier, BaseShare = share, ExpireTick = expireTick };
                expiry.Push(new CosmicQueuedEvent { Tick = expireTick, Kind = CosmicQueuedKind.Expire, TileX = bx, TileY = by });
            }

            made = count;
            return CosmicHitResult.Ionized;
        }

        // Looks at the expiry queue for blocks whose time is up at tick `now`. The first one that is still ionized discharges its WHOLE
        // connected group of ionized blocks, once: energy by A1, center = mean tile, every block of the group de-ionizes (A9), and with
        // refractoryTicks > 0 those cloud blocks cannot be ionized again for that long. Returns false when nothing is due; call it in a
        // loop until it does. Blocks whose cloud was broken are dropped quietly.
        public bool PopDischarge(long now, int refractoryTicks, Func<int, int, bool> isCloud, out CosmicDischarge discharge)
        {
            discharge = default;

            while (expiry.TryPeek(out CosmicQueuedEvent next) && next.Tick <= now)
            {
                expiry.Pop();

                long key = CosmicClusterMath.Key(next.TileX, next.TileY);
                if (!blocks.TryGetValue(key, out CosmicIonizedBlock seed) || seed.ExpireTick != next.Tick)
                    continue;   // stale: its cluster already discharged, or the block was ionized again later with a new time

                if (!isCloud(next.TileX, next.TileY))
                {
                    blocks.Remove(key);
                    continue;
                }

                currentIsCloud = isCloud;
                int count = CosmicClusterMath.Fill(next.TileX, next.TileY, int.MaxValue, isIonizedCloud, scratch);
                currentIsCloud = null;

                double baseSum = 0.0;
                double product = 1.0;

                foreach ((int bx, int by) in scratch)
                {
                    long blockKey = CosmicClusterMath.Key(bx, by);
                    CosmicIonizedBlock block = blocks[blockKey];
                    baseSum += block.BaseShare;
                    product *= block.Multiplier;
                    blocks.Remove(blockKey);

                    if (refractoryTicks > 0)
                        refractory[blockKey] = now + refractoryTicks;
                }

                CosmicClusterMath.Center(scratch, out float centerX, out float centerY);

                discharge = new CosmicDischarge
                {
                    Tick = now,
                    Energy = CosmicClusterMath.Energy(baseSum, product),
                    BaseSum = baseSum,
                    Blocks = count,
                    CenterX = centerX,
                    CenterY = centerY
                };
                return true;
            }

            return false;
        }
    }
}