using System;
using System.Collections.Generic;

namespace Factorraria.Common.Networks.Flow
{
    // The per-tick solver. Pure: it reads offers, space and pump budgets and returns transfers; the game layer applies them.
    // Rules: one liquid type is pumped at a time (ActiveType); a type stays active until it can no longer move (no source
    // holds it, no sink takes it, or the pumps are dead), then the next type found in port order takes over.
    public sealed class FlowSolver
    {
        public int ActiveType = -1;

        const float Epsilon = 1e-5f;
        const int Rounds = 3;

        float[] srcRem = Array.Empty<float>();
        float[] dstRem = Array.Empty<float>();
        float[] routeRem = Array.Empty<float>();
        float[] moved = Array.Empty<float>();
        float[] want = Array.Empty<float>();
        float[] amount = Array.Empty<float>();
        float[] srcDem = Array.Empty<float>();
        float[] dstDem = Array.Empty<float>();
        float[] pumpDem = Array.Empty<float>();

        // sourceOffer/sourceType: what each source port can give right now. sinkSpace(sink, type): room for that liquid.
        // pumpBudget is indexed by node index (units per tick); it is consumed in place.
        public void Solve(IReadOnlyList<FlowRoute> routes, float[] sourceOffer, int[] sourceType, int sinkCount,
                          Func<int, int, float> sinkSpace, float[] pumpBudget, List<FlowTransfer> output)
        {
            output.Clear();
            if (routes.Count == 0) { ActiveType = -1; return; }

            if (!IsUsable(ActiveType, routes, sourceOffer, sourceType, sinkSpace, pumpBudget))
            {
                ActiveType = -1;
                for (int r = 0; r < routes.Count && ActiveType == -1; r++)
                {
                    int t = sourceType[routes[r].Src];
                    if (t != -1 && IsUsable(t, routes, sourceOffer, sourceType, sinkSpace, pumpBudget))
                        ActiveType = t;
                }
                if (ActiveType == -1) return;
            }

            int type = ActiveType;
            int sources = sourceOffer.Length;

            Ensure(ref srcRem, sources); Ensure(ref srcDem, sources);
            Ensure(ref dstRem, sinkCount); Ensure(ref dstDem, sinkCount);
            Ensure(ref routeRem, routes.Count); Ensure(ref moved, routes.Count); Ensure(ref want, routes.Count); Ensure(ref amount, routes.Count);
            Ensure(ref pumpDem, pumpBudget.Length);

            for (int s = 0; s < sources; s++) srcRem[s] = sourceType[s] == type ? Math.Max(0f, sourceOffer[s]) : 0f;
            for (int k = 0; k < sinkCount; k++) dstRem[k] = Math.Max(0f, sinkSpace(k, type));
            for (int r = 0; r < routes.Count; r++) { routeRem[r] = routes[r].RatePerTick; moved[r] = 0f; }

            for (int round = 0; round < Rounds; round++)
            {
                Array.Clear(srcDem, 0, sources);
                Array.Clear(dstDem, 0, sinkCount);
                Array.Clear(pumpDem, 0, pumpBudget.Length);

                bool any = false;
                for (int r = 0; r < routes.Count; r++)
                {
                    FlowRoute route = routes[r];
                    float w = Math.Min(Math.Min(srcRem[route.Src], dstRem[route.Dst]), routeRem[r]);
                    foreach (int p in route.Pumps) w = Math.Min(w, pumpBudget[p]);
                    if (w < Epsilon) { want[r] = 0f; continue; }

                    want[r] = w;
                    srcDem[route.Src] += w;
                    dstDem[route.Dst] += w;
                    foreach (int p in route.Pumps) pumpDem[p] += w;
                    any = true;
                }
                if (!any) break;

                // Proportional share: every resource (source, sink, pump) that is over-demanded scales all its routes down
                // by the same factor, so nothing depends on route order and nothing is over-allocated. All shares are computed
                // from the values at the START of the round (amount[]), then applied together.
                for (int r = 0; r < routes.Count; r++)
                {
                    amount[r] = 0f;
                    if (want[r] <= 0f) continue;
                    FlowRoute route = routes[r];

                    float scale = 1f;
                    if (srcDem[route.Src] > srcRem[route.Src]) scale = Math.Min(scale, srcRem[route.Src] / srcDem[route.Src]);
                    if (dstDem[route.Dst] > dstRem[route.Dst]) scale = Math.Min(scale, dstRem[route.Dst] / dstDem[route.Dst]);
                    foreach (int p in route.Pumps)
                        if (pumpDem[p] > pumpBudget[p]) scale = Math.Min(scale, pumpBudget[p] / pumpDem[p]);

                    amount[r] = want[r] * scale;
                }

                for (int r = 0; r < routes.Count; r++)
                {
                    if (amount[r] <= 0f) continue;
                    FlowRoute route = routes[r];
                    moved[r] += amount[r];
                    routeRem[r] -= amount[r];
                    srcRem[route.Src] = Math.Max(0f, srcRem[route.Src] - amount[r]);
                    dstRem[route.Dst] = Math.Max(0f, dstRem[route.Dst] - amount[r]);
                    foreach (int p in route.Pumps) pumpBudget[p] = Math.Max(0f, pumpBudget[p] - amount[r]);
                }
            }

            for (int r = 0; r < routes.Count; r++)
            {
                if (moved[r] <= Epsilon) continue;
                output.Add(new FlowTransfer { Route = r, Src = routes[r].Src, Dst = routes[r].Dst, Type = type, Amount = moved[r] });
            }
        }

        static bool IsUsable(int type, IReadOnlyList<FlowRoute> routes, float[] sourceOffer, int[] sourceType,
                             Func<int, int, float> sinkSpace, float[] pumpBudget)
        {
            if (type == -1) return false;
            foreach (FlowRoute route in routes)
            {
                if (sourceType[route.Src] != type || sourceOffer[route.Src] < Epsilon) continue;
                if (route.RatePerTick < Epsilon) continue;
                if (sinkSpace(route.Dst, type) < Epsilon) continue;

                bool pumpsAlive = true;
                foreach (int p in route.Pumps)
                    if (pumpBudget[p] < Epsilon) { pumpsAlive = false; break; }
                if (pumpsAlive) return true;
            }
            return false;
        }

        static void Ensure(ref float[] array, int size)
        {
            if (array.Length < size) array = new float[size];
        }
    }
}