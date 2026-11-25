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
    // Two effects (P9c). A wire SWEEPS (electric dust travelling along it from end A to end B, plus one zap sound) only when the link is
    // just placed while both poles are on. A wire that already exists FLASHES (dust along its whole length in one tick, at a fixed density
    // per pixel of rope, plus one zap sound) whenever either of its poles goes from off to on (turning off is silent): a pole switched on, the grid
    // powering up, a machine joining the grid and tipping it back on. isOn is the only signal used: it is what the server's grid
    // decides and what the machine snapshot syncs, so a client sees exactly the changes the server made.
    // Nothing happens for a link that is off screen (no rope; its state is still tracked so it does not flash when it comes back), and
    // nothing happens during the first StartupQuietTicks after entering a world, because the links and the poles' state all arrive then.
    public partial class PoleWireDrawSystem
    {
        // Tunables (plain instance fields, so hot reload can change them).
        public bool Sparks = true;
        public int StartupQuietTicks = 180;        // after entering a world, link and power changes are absorbed silently for this long
        public float SweepPixelsPerTick = 10f;     // the sweep's constant speed: a short wire finishes sooner than a long one
        public int MinSweepTicks = 6;              // a very short wire still shows the sweep for at least this long
        public int SparksPerTick = 2;              // dust at the sweep's front
        public int StraySparksPerTick = 1;         // dust at a random point behind the front
        public int SparkDustType = 226;            // DustID.Electric
        public float SparkScale = 1f;
        public int MaxPulses = 24;                 // sweeps running at once
        public int MaxZapSoundsPerTick = 2;        // a whole grid powering up does not play a hundred zaps at once
        public float FlashPixelsPerSpark = 10f;    // P9c: the flash density, one spark per this many pixels of rope (so a longer wire gets proportionally more)
        public float FlashSparkScale = 1.1f;
        public float FlashSparkSpeed = 1.6f;       // random drift of a flash spark, pixels per tick
        public int FlashCooldownTicks = 20;        // a wire that just flashed ignores further pole changes for this long

        // Sounds (vanilla ones for now; swap them here, or later for your own SoundStyle("Factorraria/Assets/Sounds/...")).
        public SoundStyle FirstPointSound = SoundID.Item64 with { Volume = 0.7f, Pitch = 0.2f };                           // right click on the first pole
        public SoundStyle ConnectSound = SoundID.Dig with { Volume = 0.7f, Pitch = 0.1f };                            // the second pole: the link is requested
        public SoundStyle ZapSound = SoundID.Item93 with { Volume = 0.45f, PitchVariance = 0.2f };     // a wire starts to conduct

        class Pulse
        {
            public PoleLink Link;
            public int Age;
            public int Ticks;                      // how long this sweep lasts: rope length / SweepPixelsPerTick
        }

        readonly List<Pulse> pulses = new List<Pulse>();
        int quietTicks;

        public override void OnWorldLoad()
        {
            quietTicks = 0;
            pulses.Clear();
            birds.Clear();
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
                if (entry.FlashCooldown > 0)
                    entry.FlashCooldown--;

                // Poles not here yet (join order): nothing to compare, and the entry stays unprimed.
                if (!TryGetDrawablePole(link.A, link.SlotA, out PowerPoleTileEntityBase poleA) || !TryGetDrawablePole(link.B, link.SlotB, out PowerPoleTileEntityBase poleB))
                    continue;

                bool onA = poleA.isOn;
                bool onB = poleB.isOn;
                bool both = onA && onB;

                // What happened to this wire this tick. State is always updated (even off screen or while quiet), so only a real
                // change that happens in view can spark.
                bool sweep = false;
                bool flash = false;
                if (!entry.Primed)
                {
                    entry.Primed = true;
                    sweep = entry.JustPlaced && both;
                }
                else if ((onA && !entry.OnA) || (onB && !entry.OnB))   // only a pole turning ON; turning off is silent
                {
                    flash = entry.FlashCooldown <= 0;
                }

                entry.JustPlaced = false;
                entry.OnA = onA;
                entry.OnB = onB;
                entry.BothOn = both;

                if ((!sweep && !flash) || quiet || !Sparks)
                    continue;

                if (sweep && pulses.Count >= MaxPulses)
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
                if (sweep)
                {
                    pulses.Add(new Pulse { Link = link, Ticks = Math.Max(MinSweepTicks, (int)Math.Ceiling(rope.Length / Math.Max(0.1f, SweepPixelsPerTick))) });
                }
                else
                {
                    EmitFlash(rope);
                    entry.FlashCooldown = FlashCooldownTicks;
                    ScareBirds(link);   // P11
                }

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
                if (pulse.Age > pulse.Ticks || !byLink.TryGetValue(pulse.Link, out CachedLink entry) || entry.Rope == null)
                {
                    pulses.RemoveAt(i);
                    continue;
                }

                EmitSparks(entry.Rope, pulse.Age / (float)Math.Max(1, pulse.Ticks));
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

        // The whole wire at once: a fixed density of sparks per pixel of rope, one per slot at a random spot inside it so they do not line up.
        void EmitFlash(PoleRope rope)
        {
            int count = Math.Max(2, (int)Math.Ceiling(rope.Length / Math.Max(1f, FlashPixelsPerSpark)));
            float last = rope.Count - 1;

            for (int s = 0; s < count; s++)
            {
                float index = (s + Main.rand.NextFloat()) / count * last;
                Dust dust = Dust.NewDustPerfect(PointAlong(rope, index) + Main.rand.NextVector2Circular(2f, 2f), SparkDustType, Main.rand.NextVector2Circular(FlashSparkSpeed, FlashSparkSpeed), 0, default, FlashSparkScale * (0.7f + 0.6f * Main.rand.NextFloat()));
                dust.noGravity = true;
            }
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