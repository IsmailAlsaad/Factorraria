using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    // Client only (power poles patch P9b). The sparks and sounds of the hanging wires; the rest of the class is in PoleWireDrawSystem.cs.
    // Nothing here is saved or sent: every client watches its own replica of the links and the synced isOn of the two poles.
    //
    // A wire SPARKS (a sweep of electric dust along it from end A to end B, plus one zap sound) when BOTH of its poles are on and it was not
    // already so: the two poles go from "not both on" to "both on" (one on and one off does not count), or the link was just placed while
    // both are already on. Nothing sparks for a link that is off screen (no rope), and nothing sparks during the first StartupQuietTicks
    // after entering a world, because the links and the poles' state all arrive then and would look like a thousand new links.
    public partial class PoleWireDrawSystem
    {
        // Tunables (plain instance fields, so hot reload can change them).
        public bool Sparks = true;
        public int StartupQuietTicks = 180;        // after entering a world, link and power changes are absorbed silently for this long
        public int SparkTicks = 36;                // how long the sweep along the wire takes
        public int SparksPerTick = 2;              // dust at the sweep's front
        public int StraySparksPerTick = 1;         // dust at a random point behind the front
        public int SparkDustType = 226;            // DustID.Electric
        public float SparkScale = 1f;
        public int MaxPulses = 24;                 // sweeps running at once
        public int MaxZapSoundsPerTick = 2;        // a whole grid powering up does not play a hundred zaps at once

        // Sounds (vanilla ones for now; swap them here, or later for your own SoundStyle("Factorraria/Assets/Sounds/...")).
        public SoundStyle FirstPointSound = SoundID.Item64 with { Volume = 0.7f, Pitch = 0.2f };                           // right click on the first pole
        public SoundStyle ConnectSound = SoundID.Dig with { Volume = 0.7f, Pitch = 0.1f };                            // the second pole: the link is requested
        public SoundStyle ZapSound = SoundID.Item93 with { Volume = 0.45f, PitchVariance = 0.2f };     // a wire starts to conduct

        class Pulse
        {
            public PoleLink Link;
            public int Age;
        }

        readonly List<Pulse> pulses = new List<Pulse>();
        int quietTicks;

        public override void OnWorldLoad()
        {
            quietTicks = 0;
            pulses.Clear();
        }

        // The local player's own actions (called by PoleLinkInputSystem).
        public void PlayFirstPointSound(Vector2 position)
        {
            if (!Main.dedServ)
                SoundEngine.PlaySound(FirstPointSound, position);
        }

        public void PlayConnectSound(Vector2 position)
        {
            if (!Main.dedServ)
                SoundEngine.PlaySound(ConnectSound, position);
        }

        // Once per tick (PostUpdateEverything, before the rope step). Tracks "both poles on" for every link, starts sweeps, advances them.
        void UpdateSparks()
        {
            if (quietTicks < StartupQuietTicks)
                quietTicks++;

            PoleLinkSystem links = PoleLinkSystem.Instance;
            if (links.Links.Count == 0)
            {
                pulses.Clear();
                return;
            }

            EnsureCache(links);
            bool quiet = quietTicks < StartupQuietTicks;
            bool visibleKnown = false;
            Rectangle visible = default;
            int zaps = 0;

            foreach (CachedLink entry in cache)
            {
                PoleLink link = entry.Link;
                bool both = TryGetDrawablePole(link.A, link.SlotA, out PowerPoleTileEntityBase poleA) && TryGetDrawablePole(link.B, link.SlotB, out PowerPoleTileEntityBase poleB)
                    && poleA.isOn && poleB.isOn;

                bool started = both && !entry.BothOn;
                entry.BothOn = both;

                if (!started || quiet || !Sparks || pulses.Count >= MaxPulses)
                    continue;

                if (!visibleKnown)
                {
                    GetVisibleRect(out visible);
                    visibleKnown = true;
                }

                if (!IsVisible(entry, visible))
                    continue;

                // A link that was just placed has no rope yet this tick (the first draw or step creates it), so make it here.
                PoleRope rope = GetRope(entry, entry.RestA, entry.RestB);
                pulses.Add(new Pulse { Link = link });

                if (zaps < MaxZapSoundsPerTick)
                {
                    zaps++;
                    SoundEngine.PlaySound(ZapSound, rope.Pos[rope.Count / 2]);
                }
            }

            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                Pulse pulse = pulses[i];
                pulse.Age++;

                // Over, or the link was cut / left the screen (no rope): the sweep just ends.
                if (pulse.Age > SparkTicks || !byLink.TryGetValue(pulse.Link, out CachedLink entry) || entry.Rope == null)
                {
                    pulses.RemoveAt(i);
                    continue;
                }

                EmitSparks(entry.Rope, pulse.Age / (float)Math.Max(1, SparkTicks));
            }
        }

        void EmitSparks(PoleRope rope, float progress)
        {
            float front = MathHelper.Clamp(progress, 0f, 1f) * (rope.Count - 1);

            for (int s = 0; s < SparksPerTick; s++)
                SpawnSpark(PointAlong(rope, front));

            for (int s = 0; s < StraySparksPerTick; s++)
                SpawnSpark(PointAlong(rope, Main.rand.NextFloat() * front));
        }

        // 'index' is a fractional node index.
        static Vector2 PointAlong(PoleRope rope, float index)
        {
            int i0 = Math.Clamp((int)index, 0, rope.Count - 1);
            int i1 = Math.Min(i0 + 1, rope.Count - 1);
            return Vector2.Lerp(rope.Pos[i0], rope.Pos[i1], index - i0);
        }

        void SpawnSpark(Vector2 position)
        {
            Dust dust = Dust.NewDustPerfect(position + Main.rand.NextVector2Circular(2f, 2f), SparkDustType, Main.rand.NextVector2Circular(1.2f, 1.2f), 0, default, SparkScale * (0.7f + 0.6f * Main.rand.NextFloat()));
            dust.noGravity = true;
        }
    }
}