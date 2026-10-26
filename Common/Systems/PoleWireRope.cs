using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Factorraria.Common.Systems
{
    // A nearby moving actor that can push a visible rope. This is visual state only: actor positions and velocities are never changed.
    readonly struct PoleWireActorPush
    {
        public readonly Vector2 Center;
        public readonly Vector2 Velocity;

        public PoleWireActorPush(Vector2 center, Vector2 velocity)
        {
            Center = center;
            Velocity = velocity;
        }
    }

    // Client-only Verlet rope for one power pole link. The two endpoints are pinned to the authoritative connection points; only the
    // nodes between them move. A rope is created only while its link is visible and is discarded by PoleWireDrawSystem when it leaves
    // the screen, so the next visit starts cleanly from the rest curve instead of replaying missed time.
    sealed class PoleWireRope
    {
        public Vector2[] Nodes { get; private set; }

        Vector2[] previous;
        float[] segmentLengths;

        public int NodeCount => Nodes.Length;

        public PoleWireRope(Vector2 from, Vector2 to, int nodeCount, float sag, float slack)
        {
            Nodes = new Vector2[Math.Max(2, nodeCount)];
            previous = new Vector2[Nodes.Length];
            segmentLengths = new float[Nodes.Length - 1];
            Reset(from, to, sag, slack);
        }

        public void Reset(Vector2 from, Vector2 to, float sag, float slack)
        {
            for (int i = 0; i < Nodes.Length; i++)
            {
                Vector2 point = RestPosition(from, to, sag, i, Nodes.Length);
                Nodes[i] = point;
                previous[i] = point;
            }

            for (int i = 0; i < segmentLengths.Length; i++)
                segmentLengths[i] = Vector2.Distance(Nodes[i], Nodes[i + 1]) * (1f + Math.Max(0f, slack));
        }

        // Follow the swaying crossboards without adding endpoint velocity to the simulation.
        public void PinEndpoints(Vector2 from, Vector2 to)
        {
            Nodes[0] = from;
            previous[0] = from;
            Nodes[Nodes.Length - 1] = to;
            previous[previous.Length - 1] = to;
        }

        public void Step(Vector2 from, Vector2 to, float sag, float deltaTime, float damping, float gravity, float wind,
            float maxOffset, int constraintIterations, IReadOnlyList<PoleWireActorPush> actors, float actorRadius, float actorPush)
        {
            PinEndpoints(from, to);
            float deltaSquared = deltaTime * deltaTime;
            float radiusSquared = actorRadius * actorRadius;

            for (int i = 1; i < Nodes.Length - 1; i++)
            {
                Vector2 position = Nodes[i];
                Vector2 velocity = (position - previous[i]) * damping;
                previous[i] = position;

                float t = i / (float)(Nodes.Length - 1);
                float middle = (float)Math.Sin(Math.PI * t);
                Vector2 acceleration = new Vector2(wind * middle, gravity);
                Nodes[i] = position + velocity + acceleration * deltaSquared;
            }

            // Actor motion is applied as a local displacement, falling off to zero at the radius. Constraints below restore the rope's length.
            if (actorPush > 0f && actorRadius > 0f)
            {
                foreach (PoleWireActorPush actor in actors)
                {
                    if (actor.Velocity == Vector2.Zero)
                        continue;

                    for (int i = 1; i < Nodes.Length - 1; i++)
                    {
                        Vector2 offset = Nodes[i] - actor.Center;
                        float distanceSquared = offset.LengthSquared();
                        if (distanceSquared >= radiusSquared)
                            continue;

                        float falloff = 1f - (float)Math.Sqrt(distanceSquared) / actorRadius;
                        Nodes[i] += actor.Velocity * (actorPush * falloff);
                    }
                }
            }

            // Alternating the constraint direction avoids always stretching the rope towards the same endpoint.
            for (int iteration = 0; iteration < Math.Max(1, constraintIterations); iteration++)
            {
                PinEndpoints(from, to);
                if ((iteration & 1) == 0)
                {
                    for (int i = 0; i < segmentLengths.Length; i++)
                        SolveSegment(i);
                }
                else
                {
                    for (int i = segmentLengths.Length - 1; i >= 0; i--)
                        SolveSegment(i);
                }
            }

            // A hard visual envelope makes the shared culling test conservative even during strong wind or actor pushes.
            for (int i = 1; i < Nodes.Length - 1; i++)
            {
                Vector2 rest = RestPosition(from, to, sag, i, Nodes.Length);
                Vector2 offset = Nodes[i] - rest;
                float length = offset.Length();
                if (length <= maxOffset)
                    continue;

                Vector2 correction = offset * (1f - maxOffset / length);
                Nodes[i] += correction;
                previous[i] += correction;
            }

            PinEndpoints(from, to);
        }

        void SolveSegment(int index)
        {
            int next = index + 1;
            Vector2 edge = Nodes[next] - Nodes[index];
            float distance = edge.Length();
            if (distance < 0.0001f)
                return;

            float correction = (distance - segmentLengths[index]) / distance;
            bool pinA = index == 0;
            bool pinB = next == Nodes.Length - 1;
            if (pinA)
                Nodes[next] -= edge * correction;
            else if (pinB)
                Nodes[index] += edge * correction;
            else
            {
                Vector2 movement = edge * (correction * 0.5f);
                Nodes[index] += movement;
                Nodes[next] -= movement;
            }
        }

        public static Vector2 RestPosition(Vector2 from, Vector2 to, float sag, int index, int nodeCount)
        {
            float t = index / (float)(nodeCount - 1);
            return Vector2.Lerp(from, to, t) + new Vector2(0f, sag * 4f * t * (1f - t));
        }
    }
}
