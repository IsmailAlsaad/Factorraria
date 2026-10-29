using System;

namespace Factorraria.Common.Cosmic
{
    public enum CosmicQueuedKind : byte
    {
        RayStart = 0,   // a ray that begins flying at Tick; the server traces it then (X, Y, DirX, DirY, Color)
        Absorb = 1,     // a ray reaches a gemspark of its own color at Tick (TileX, TileY, Color)
        Ionize = 2,     // a ray reaches a cloud block at Tick (TileX, TileY, Color)
        Expire = 3      // C4: an ionized cloud block's time is up at Tick (TileX, TileY); only used by the queue inside CosmicIonizedField
    }

    // One entry of the server's event queue. Plain data so the queue is cheap and allocation free.
    public struct CosmicQueuedEvent
    {
        public long Tick;
        public CosmicQueuedKind Kind;
        public CosmicColor Color;
        public float X;
        public float Y;
        public float DirX;
        public float DirY;
        public float SpeedMul;   // re-emitted rays only: random factor on ParticleSpeed (0 = never set, counts as 1)
        public float LifeMul;    // re-emitted rays only: random factor on the lifespan (0 = never set, counts as 1)
        public int TileX;
        public int TileY;
        internal long Seq;   // set by the heap: events with the same Tick come out in the order they were pushed
    }

    // Binary min-heap ordered by (Tick, push order). Pure code, tested in tools/cosmic_tests.
    public sealed class CosmicEventHeap
    {
        CosmicQueuedEvent[] items = new CosmicQueuedEvent[256];
        int count;
        long nextSeq;

        public int Count => count;

        public void Clear()
        {
            count = 0;
            nextSeq = 0;
        }

        public void Push(in CosmicQueuedEvent e)
        {
            if (count == items.Length)
                Array.Resize(ref items, items.Length * 2);

            CosmicQueuedEvent copy = e;
            copy.Seq = nextSeq++;

            int i = count++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (!Less(copy, items[parent]))
                    break;
                items[i] = items[parent];
                i = parent;
            }
            items[i] = copy;
        }

        public bool TryPeek(out CosmicQueuedEvent e)
        {
            if (count == 0)
            {
                e = default;
                return false;
            }

            e = items[0];
            return true;
        }

        // Removes and returns the earliest event. Check Count (or TryPeek) first.
        public CosmicQueuedEvent Pop()
        {
            CosmicQueuedEvent top = items[0];
            CosmicQueuedEvent last = items[--count];
            items[count] = default;

            if (count > 0)
            {
                int i = 0;
                while (true)
                {
                    int child = 2 * i + 1;
                    if (child >= count)
                        break;
                    if (child + 1 < count && Less(items[child + 1], items[child]))
                        child++;
                    if (!Less(items[child], last))
                        break;
                    items[i] = items[child];
                    i = child;
                }
                items[i] = last;
            }

            return top;
        }

        static bool Less(in CosmicQueuedEvent a, in CosmicQueuedEvent b)
        {
            return a.Tick != b.Tick ? a.Tick < b.Tick : a.Seq < b.Seq;
        }
    }
}
