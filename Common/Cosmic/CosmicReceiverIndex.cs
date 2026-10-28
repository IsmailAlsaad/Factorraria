using System.Collections.Generic;

namespace Factorraria.Common.Cosmic
{
    // Every tile that can take a particle (lit gemspark blocks and cloud blocks) in the rows a ray can reach, grouped into vertical
    // strips 8 tiles wide. The sampler only creates particles above strips that have receivers. Pure code (no Terraria types), tested
    // in tools/cosmic_tests. A receiver is stored as a code: 0 to 7 = the gemspark of that CosmicColor (a color without a gemspark, Grey, never has one), 15 = a cloud block.
    // Server and single-player only; never saved (rebuilt by scanning the world).
    public sealed class CosmicReceiverIndex
    {
        public const int StripWidth = 8;
        // Not a color index, so adding colors never collides with it (it was 7 before N2 made Grey color 7).
        public const byte CloudCode = 15;

        public sealed class Strip
        {
            public readonly Dictionary<long, byte> Tiles = new Dictionary<long, byte>();
            public int MinY = int.MaxValue;
            public int MaxY = -1;
            public bool Dirty;   // MinY / MaxY are stale after a removal; TryGetYRange recomputes them
        }

        readonly Dictionary<int, Strip> strips = new Dictionary<int, Strip>();
        readonly int[] counts = new int[16];

        // Rows deeper than this are not indexed (a ray cannot reach them). The system sets it from the band and the range.
        public int MaxY = int.MaxValue;

        // Bumped on every change, so the sampler knows when to rebuild its sampling areas.
        public int Version { get; private set; }

        public int Total { get; private set; }
        public int StripCount => strips.Count;
        public int CloudCount => counts[CloudCode];
        public int GemsparkCount(int color) => counts[color];

        public static int StripOf(int x) => x / StripWidth;

        static long Key(int x, int y) => ((long)x << 32) | (uint)y;

        public void Clear()
        {
            strips.Clear();
            for (int i = 0; i < counts.Length; i++)
                counts[i] = 0;
            Total = 0;
            Version++;
        }

        public bool Contains(int x, int y, out byte code)
        {
            code = 0;
            return x >= 0 && strips.TryGetValue(StripOf(x), out Strip strip) && strip.Tiles.TryGetValue(Key(x, y), out code);
        }

        // Idempotent: adding what is already there changes nothing and returns false.
        public bool Add(int x, int y, byte code)
        {
            if (x < 0 || y < 0 || y > MaxY || code > CloudCode)
                return false;

            int s = StripOf(x);
            if (!strips.TryGetValue(s, out Strip strip))
            {
                strip = new Strip();
                strips[s] = strip;
            }

            long key = Key(x, y);
            if (strip.Tiles.TryGetValue(key, out byte old))
            {
                if (old == code)
                    return false;

                counts[old]--;
                counts[code]++;
                strip.Tiles[key] = code;
                Version++;
                return true;
            }

            strip.Tiles[key] = code;
            counts[code]++;
            Total++;

            if (!strip.Dirty)
            {
                if (y < strip.MinY) strip.MinY = y;
                if (y > strip.MaxY) strip.MaxY = y;
            }

            Version++;
            return true;
        }

        // Idempotent: removing what is not there returns false.
        public bool Remove(int x, int y)
        {
            if (x < 0 || !strips.TryGetValue(StripOf(x), out Strip strip))
                return false;

            if (!strip.Tiles.Remove(Key(x, y), out byte old))
                return false;

            counts[old]--;
            Total--;

            if (strip.Tiles.Count == 0)
                strips.Remove(StripOf(x));
            else
                strip.Dirty = true;

            Version++;
            return true;
        }

        // Replaces the whole content of one strip with what a scan found (adds new tiles, drops tiles that are gone).
        // Changes nothing, and does not bump Version, when the strip already matches.
        public void ReplaceStrip(int s, List<(int x, int y, byte code)> found)
        {
            strips.TryGetValue(s, out Strip old);

            int oldCount = old == null ? 0 : old.Tiles.Count;
            if (oldCount == found.Count)
            {
                bool same = true;
                for (int i = 0; i < found.Count && same; i++)
                    same = old.Tiles.TryGetValue(Key(found[i].x, found[i].y), out byte c) && c == found[i].code;

                if (same)
                    return;
            }

            if (old != null)
            {
                foreach (byte c in old.Tiles.Values)
                {
                    counts[c]--;
                    Total--;
                }
                strips.Remove(s);
            }

            if (found.Count > 0)
            {
                Strip fresh = new Strip();
                for (int i = 0; i < found.Count; i++)
                {
                    (int x, int y, byte code) f = found[i];
                    if (f.code > CloudCode || f.y < 0 || f.y > MaxY)
                        continue;

                    if (fresh.Tiles.TryAdd(Key(f.x, f.y), f.code))
                    {
                        counts[f.code]++;
                        Total++;
                        if (f.y < fresh.MinY) fresh.MinY = f.y;
                        if (f.y > fresh.MaxY) fresh.MaxY = f.y;
                    }
                }

                if (fresh.Tiles.Count > 0)
                    strips[s] = fresh;
            }

            Version++;
        }

        // The strips that currently hold at least one receiver (read only; do not add or remove while enumerating).
        public IEnumerable<int> StripIds => strips.Keys;

        // Highest and lowest row with a receiver in this strip.
        public bool TryGetYRange(int s, out int minY, out int maxY)
        {
            minY = 0;
            maxY = 0;

            if (!strips.TryGetValue(s, out Strip strip) || strip.Tiles.Count == 0)
                return false;

            if (strip.Dirty)
            {
                int lo = int.MaxValue;
                int hi = -1;
                foreach (long key in strip.Tiles.Keys)
                {
                    int y = (int)(uint)(key & 0xFFFFFFFFL);
                    if (y < lo) lo = y;
                    if (y > hi) hi = y;
                }
                strip.MinY = lo;
                strip.MaxY = hi;
                strip.Dirty = false;
            }

            minY = strip.MinY;
            maxY = strip.MaxY;
            return true;
        }
    }
}
