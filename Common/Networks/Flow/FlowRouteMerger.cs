using System;
using System.Collections.Generic;

namespace Factorraria.Common.Networks.Flow
{
    // Structural rule shared by the flood fill that groups tiles into networks and by anything else that needs "are these two
    // adjacent tiles connected at all", independent of pumps being on or off. Pipes link on every side; a motor only on its axis.
    public static class FlowLinks
    {
        public static bool OnAxis(in FlowNode node, FlowDir moveDir) => !node.IsMotor || FlowDirs.SameAxis(moveDir, node.Facing);

        // moveDir = direction from a to b. Symmetric: CanLink(a, b, d) == CanLink(b, a, Opposite(d)).
        public static bool CanLink(in FlowNode a, in FlowNode b, FlowDir moveDir) => OnAxis(a, moveDir) && OnAxis(b, moveDir);
    }

    // The route finder works on ATTACH POINTS (a tile + the side an outside thing touches it on). The game works on PORTS (one per
    // machine output tank, one per machine for all its input tanks, one per world liquid cell). A port can be touched in several
    // places (a 3x3 machine next to three pipe tiles; a pool next to two pipes), and one attach point can serve several ports
    // (a machine with two output tanks). This turns the attach-level routes into port-level routes:
    //   - every (source port, sink port) pair appears at most once, so a tank is never offered or filled twice;
    //   - when several attach pairs connect the same two ports, the best one wins: highest rate, then fewest climbs, then the
    //     first in the (already deterministic) input order.
    public static class FlowRouteMerger
    {
        public static List<FlowRoute> Merge(IReadOnlyList<FlowRoute> raw, int[][] srcPortsOfAttach, int[][] dstPortsOfAttach)
        {
            var best = new Dictionary<long, FlowRoute>();
            var order = new List<long>();

            foreach (FlowRoute r in raw)
            {
                if (r.Src < 0 || r.Src >= srcPortsOfAttach.Length || r.Dst < 0 || r.Dst >= dstPortsOfAttach.Length) continue;

                foreach (int ps in srcPortsOfAttach[r.Src])
                {
                    foreach (int pd in dstPortsOfAttach[r.Dst])
                    {
                        long key = ((long)ps << 32) | (uint)pd;
                        if (best.TryGetValue(key, out FlowRoute current))
                        {
                            bool better = r.RatePerMinute > current.RatePerMinute ||
                                          (r.RatePerMinute == current.RatePerMinute && r.Climbs < current.Climbs);
                            if (!better) continue;
                        }
                        else
                        {
                            order.Add(key);
                        }

                        best[key] = new FlowRoute { Src = ps, Dst = pd, Climbs = r.Climbs, Pumps = r.Pumps, RatePerMinute = r.RatePerMinute };
                    }
                }
            }

            var result = new List<FlowRoute>(order.Count);
            foreach (long key in order) result.Add(best[key]);
            result.Sort((a, b) => a.Src != b.Src ? a.Src.CompareTo(b.Src) : a.Dst.CompareTo(b.Dst));
            return result;
        }
    }
}
