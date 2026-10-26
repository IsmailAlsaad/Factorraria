using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;

namespace Factorraria.Common.Systems
{
    // One thing that can push a hanging wire: a player or an NPC that is on screen and moving. Built once per tick by PoleWireDrawSystem.
    public struct PoleDisturber
    {
        public Rectangle Box;       // the hitbox, already grown by PoleWireDrawSystem.DisturbRadius
        public Vector2 Velocity;    // pixels per tick
    }

    // Client only (power poles patch P9). The visual of ONE hanging wire: a Verlet rope pinned to the two swayed connection points. It is
    // never saved and never sent; the link data (PoleLinkSystem) stays the only authority, and span checks, power and cutting never read
    // this. PoleWireDrawSystem creates a rope when its link comes on screen and throws it away when the link leaves the screen, so an
    // off-screen wire costs nothing (no stepping, no memory).
    public class PoleRope
    {
        public readonly Vector2[] Pos;
        public readonly Vector2[] Old;
        public readonly int Count;
        public readonly float Length;           // the rope's own length: the resting chord plus the slack (PoleLinkSystem.SlackExtra)
        public readonly float SegmentLength;
        readonly float phase;                   // so two wires do not gust in lockstep

        public PoleRope(int count, float length)
        {
            Count = Math.Max(4, count);
            Length = length;
            SegmentLength = length / (Count - 1);
            Pos = new Vector2[Count];
            Old = new Vector2[Count];
            phase = length * 0.37f;
        }

        public static int NodeCount(float length, float spacing, int maxNodes)
        {
            int count = (int)Math.Ceiling(length / Math.Max(2f, spacing)) + 1;
            return Math.Clamp(count, 4, Math.Max(4, maxNodes));
        }

        // A parabola from 'from' to 'to' whose arc length is 'length' (arc ~ chord + 8 sag^2 / (3 chord), so sag = sqrt(3 chord (length - chord) / 8)).
        // The same curve the rope starts from and the placement hologram shows.
        public static void FillRestCurve(Vector2[] destination, int count, Vector2 from, Vector2 to, float length)
        {
            float chord = Vector2.Distance(from, to);
            float extra = Math.Max(0f, length - chord);
            float sag = (float)Math.Sqrt(0.375f * chord * extra);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector2 p = Vector2.Lerp(from, to, t);
                p.Y += 4f * sag * t * (1f - t);
                destination[i] = p;
            }
        }

        public void Rest(Vector2 from, Vector2 to)
        {
            FillRestCurve(Pos, Count, from, to, Length);
            Array.Copy(Pos, Old, Count);
        }

        public void Pin(Vector2 from, Vector2 to)
        {
            Pos[0] = from;
            Old[0] = from;
            Pos[Count - 1] = to;
            Old[Count - 1] = to;
        }

        // One game tick. Gravity, wind (steady push plus a gust ripple along the wire), the pushing of players and NPCs, then the length
        // constraints (several sweeps, alternating direction, so a long wire does not stretch).
        public void Step(PoleWireDrawSystem s, Vector2 from, Vector2 to, List<PoleDisturber> disturbers)
        {
            Pin(from, to);

            int last = Count - 1;
            float windAccel = Main.windSpeedCurrent * s.WindForce;
            float time = Main.GlobalTimeWrappedHourly * s.GustSpeed;

            for (int i = 1; i < last; i++)
            {
                Vector2 velocity = (Pos[i] - Old[i]) * s.Damping;
                float gust = 1f + s.GustAmount * (float)Math.Sin(time + i * 0.35f + phase);
                // A wind along a (mostly horizontal) wire barely moves it sideways, so the wind also makes it flutter up and down in a ripple.
                float flutter = Math.Abs(windAccel) * s.FlutterAmount * (float)Math.Sin(time * 1.3f + i * 0.5f + phase);
                Old[i] = Pos[i];
                Pos[i] += velocity + new Vector2(windAccel * gust, s.Gravity + flutter);
            }

            if (disturbers != null && disturbers.Count > 0)
                ApplyDisturbers(s, disturbers);

            int iterations = Math.Max(1, s.ConstraintIterations);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int k = 0; k < last; k++)
                    {
                        int i = pass == 0 ? k : last - 1 - k;
                        Vector2 delta = Pos[i + 1] - Pos[i];
                        float distance = delta.Length();
                        if (distance < 0.0001f)
                            continue;

                        float weightA = i == 0 ? 0f : 1f;
                        float weightB = i + 1 == last ? 0f : 1f;
                        float weight = weightA + weightB;
                        if (weight <= 0f)
                            continue;

                        Vector2 correction = delta * ((distance - SegmentLength) / distance);
                        Pos[i] += correction * (weightA / weight);
                        Pos[i + 1] -= correction * (weightB / weight);
                    }
                }
            }

            for (int i = 1; i < last; i++)
            {
                if (float.IsNaN(Pos[i].X) || float.IsNaN(Pos[i].Y) || float.IsInfinity(Pos[i].X) || float.IsInfinity(Pos[i].Y))
                {
                    Rest(from, to);
                    return;
                }
            }
        }

        // A node inside a mover's (grown) hitbox has its velocity pulled toward the mover's velocity, so walking or flying through the wire
        // drags it along and lets it swing back.
        void ApplyDisturbers(PoleWireDrawSystem s, List<PoleDisturber> disturbers)
        {
            int last = Count - 1;
            float maxSpeed = Math.Max(0.5f, s.MaxNodeSpeed);

            for (int i = 1; i < last; i++)
            {
                var point = new Point((int)Pos[i].X, (int)Pos[i].Y);
                for (int d = 0; d < disturbers.Count; d++)
                {
                    PoleDisturber disturber = disturbers[d];
                    if (!disturber.Box.Contains(point))
                        continue;

                    Vector2 velocity = Vector2.Lerp(Pos[i] - Old[i], disturber.Velocity, s.DisturbStrength);
                    float speed = velocity.Length();
                    if (speed > maxSpeed)
                        velocity *= maxSpeed / speed;

                    Old[i] = Pos[i] - velocity;
                }
            }
        }

        public static float DistanceToPolyline(Vector2[] points, int count, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < count - 1; i++)
            {
                Vector2 a = points[i];
                Vector2 edge = points[i + 1] - a;
                float lengthSquared = edge.LengthSquared();
                float t = lengthSquared < 0.0001f ? 0f : MathHelper.Clamp(Vector2.Dot(p - a, edge) / lengthSquared, 0f, 1f);
                float distance = Vector2.Distance(p, a + edge * t);
                if (distance < best)
                    best = distance;
            }

            return best;
        }
    }
}