using System;
using System.Collections.Generic;

namespace Factorraria.Common.Networks.Flow
{
    // Builds the route table: for every source port, which sink ports can it reach, how much does the liquid climb on
    // the way, and which ON pumps does it pass. Rebuilt when the topology changes or a motor switches on/off; the
    // per-tick solver only reads the result.
    public static class FlowRouteFinder
    {
        // A motor lets liquid through along its axis. ON: only in its Facing direction (a pump is one-way).
        // OFF: both ways along the axis (it behaves like a plain pipe segment).
        static bool MotorPasses(in FlowNode motor, FlowDir moveDir) =>
            motor.MotorOn ? moveDir == motor.Facing : FlowDirs.SameAxis(moveDir, motor.Facing);

        static bool MovePasses(in FlowNode from, in FlowNode to, FlowDir moveDir)
        {
            if (from.IsMotor && !MotorPasses(from, moveDir)) return false;
            if (to.IsMotor && !MotorPasses(to, moveDir)) return false;
            return true;
        }

        public static List<FlowRoute> Build(FlowNode[] nodes, FlowPort[] sources, FlowPort[] sinks, float decayPerClimb, float cap)
        {
            var routes = new List<FlowRoute>();
            if (nodes.Length == 0 || sources.Length == 0 || sinks.Length == 0) return routes;

            int n = nodes.Length;
            var lookup = new Dictionary<long, int>(n);
            for (int i = 0; i < n; i++)
                lookup[Key(nodes[i].X, nodes[i].Y)] = i;

            var nb = new int[n * 4];
            for (int i = 0; i < n; i++)
                for (int d = 0; d < 4; d++)
                    nb[i * 4 + d] = lookup.TryGetValue(Key(nodes[i].X + FlowDirs.Dx[d], nodes[i].Y + FlowDirs.Dy[d]), out int j) ? j : -1;

            var sinksAt = new Dictionary<int, List<int>>();
            for (int k = 0; k < sinks.Length; k++)
            {
                if (!sinksAt.TryGetValue(sinks[k].Node, out var list)) sinksAt[sinks[k].Node] = list = new List<int>();
                list.Add(k);
            }

            int stateCount = n * 2;   // state = node * 2 + (hasPassedAnOnPump ? 1 : 0)
            var dist = new int[stateCount];
            var parent = new int[stateCount];
            var queue = new PriorityQueue<int, int>();

            for (int s = 0; s < sources.Length; s++)
            {
                FlowPort sp = sources[s];
                if (sp.Node < 0 || sp.Node >= n) continue;
                FlowNode startNode = nodes[sp.Node];

                // The liquid enters the start node moving away from the attached thing.
                if (startNode.IsMotor && !MotorPasses(startNode, FlowDirs.Opposite(sp.Mouth))) continue;

                Array.Fill(dist, int.MaxValue);
                Array.Fill(parent, -1);
                queue.Clear();

                int start = sp.Node * 2 + (startNode.IsMotor && startNode.MotorOn ? 1 : 0);
                dist[start] = 0;
                queue.Enqueue(start, 0);

                while (queue.TryDequeue(out int state, out int d))
                {
                    if (d != dist[state]) continue;   // stale entry
                    int u = state >> 1;
                    int flag = state & 1;

                    for (int dir = 0; dir < 4; dir++)
                    {
                        int v = nb[u * 4 + dir];
                        if (v < 0) continue;
                        if (!MovePasses(nodes[u], nodes[v], (FlowDir)dir)) continue;

                        int nd = d + (nodes[v].Y < nodes[u].Y ? 1 : 0);   // rising = smaller Y
                        int nflag = flag | (nodes[v].IsMotor && nodes[v].MotorOn ? 1 : 0);
                        int ns = v * 2 + nflag;
                        if (nd >= dist[ns]) continue;

                        dist[ns] = nd;
                        parent[ns] = state;
                        queue.Enqueue(ns, nd);
                    }
                }

                foreach (var pair in sinksAt)
                {
                    int t = pair.Key;
                    int goal = t * 2 + 1;
                    if (dist[goal] == int.MaxValue) continue;

                    foreach (int k in pair.Value)
                    {
                        FlowPort kp = sinks[k];
                        if (nodes[t].IsMotor && !MotorPasses(nodes[t], kp.Mouth)) continue;   // exits through the mouth
                        if (sp.Owner != -1 && sp.Owner == kp.Owner) continue;

                        var pumps = new List<int>();
                        for (int st = goal; st != -1; st = parent[st])
                        {
                            int node = st >> 1;
                            if (nodes[node].IsMotor && nodes[node].MotorOn && !pumps.Contains(node)) pumps.Add(node);
                        }
                        pumps.Sort();

                        float minStrength = float.MaxValue, sumStrength = 0f;
                        foreach (int p in pumps)
                        {
                            minStrength = Math.Min(minStrength, nodes[p].Strength);
                            sumStrength += nodes[p].Strength;
                        }

                        // Same rule as before the overhaul: a lone pump loses decayPerClimb per climbed tile; pumps in series add
                        // their strength to the climb budget, but the pipe still only carries the weakest one's rate.
                        float rate = Math.Min(minStrength, sumStrength - dist[goal] * decayPerClimb);
                        rate = Math.Min(rate, cap);
                        if (rate <= 0f) continue;

                        routes.Add(new FlowRoute { Src = s, Dst = k, Climbs = dist[goal], Pumps = pumps.ToArray(), RatePerMinute = rate });
                    }
                }
            }

            // Deterministic order: sources first, then sinks.
            routes.Sort((a, b) => a.Src != b.Src ? a.Src.CompareTo(b.Src) : a.Dst.CompareTo(b.Dst));
            return routes;
        }

        static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}