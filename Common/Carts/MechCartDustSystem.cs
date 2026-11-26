using System;
using System.Collections.Generic;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Exhaust dust of the Mechanical Cart. Client visuals only: nothing here is saved or synced.
    /// Spawn(cart) is called from CartSystem.OnCartDriving (single-player / server-side tick) and from
    /// CartSystem.ClientInterpolate (multiplayer clients, where Cart.Driving never fires because the server owns the simulation).
    /// Vanilla dust has no lifetime and decays on its own, so every spawned dust is tracked here and gets a fixed lifetime and
    /// a linear shrink from its start scale to zero.
    /// </summary>
    public class MechCartDustSystem : ModSystem
    {
        private class DustLife
        {
            public Dust Dust;
            public int Age;
            public int Lifetime;
            public float StartScale;
        }

        private readonly List<DustLife> tracked = new List<DustLife>();
        private readonly Dictionary<int, Vector2> lastSpawn = new Dictionary<int, Vector2>(); // per cart id: where the dust spawned last tick

        /// <summary>Spawns one dust puff for the cart if the constant-rate timer says it is time. Safe to call every tick.</summary>
        public static void Spawn(Cart cart)
        {
            if (Main.dedServ || cart == null || !cart.Active)
            {
                return;
            }

            // ---- Tuning (plain locals so hot reload can change them) ----
            // Offset from the cart's rail point, in pixels. X is along the cart and mirrors with Facing (negative = behind it),
            // Y is up out of the cart floor (negative = below it). Both follow the cart's tilt on slopes.
            Vector2 offset = new Vector2(-24f, 20f);
            float spacing = 4f;         // px between dust along the path the spawn point travelled this tick
            float maxGap = 96f;         // a bigger jump than this (snap, teleport) is not filled
            // DELETE THIS LEFTOVER (old spawnInterval line): 1;      // ticks between puffs (constant rate)
            int lifetime = 30;          // ticks every puff lives (constant lifetime)
            float startScale = 1.2f;    // scale at spawn; it shrinks linearly to 0 over the lifetime
            float minSpeed = 0.1f;      // the cart counts as moving above this X speed
            // --------------------------------------------------------------

            MechCartDustSystem system = ModContent.GetInstance<MechCartDustSystem>();

            if (Math.Abs(cart.AlongSpeed) < minSpeed)
            {
                system.lastSpawn.Remove(cart.Id); // stopped: no streak from the old spot when it starts again
                return;
            }

            Vector2 spawn = cart.Position + CartGeometry.Right(cart) * (cart.Facing * offset.X) + CartGeometry.Up(cart) * offset.Y;

            Vector2 previous;
            bool hasPrevious = system.lastSpawn.TryGetValue(cart.Id, out previous);
            system.lastSpawn[cart.Id] = spawn; // updated even off screen, so coming back on screen does not draw a long streak

            if (!BaseMachine.IsOnScreen(spawn))
            {
                return;
            }

            float distance = hasPrevious ? Vector2.Distance(previous, spawn) : 0f;
            bool fill = hasPrevious && distance > 0f && distance <= maxGap;
            int count = fill ? Math.Max(1, (int)Math.Ceiling(distance / Math.Max(0.5f, spacing))) : 1;

            for (int i = 1; i <= count; i++)
            {
                Vector2 position = fill ? Vector2.Lerp(previous, spawn, i / (float)count) : spawn;

                Dust dust = Dust.NewDustPerfect(position, DustID.TheDestroyer, Vector2.Zero, 0, default, startScale);

                if (!dust.active)
                {
                    return; // dust limit reached
                }

                DustLife life = new DustLife { Dust = dust, Age = 0, Lifetime = Math.Max(1, lifetime), StartScale = startScale };
                dust.noGravity = true;
                dust.velocity = Vector2.Zero;
                dust.scale = startScale;
                dust.customData = life; // also the ownership token: if the slot is reused by another dust, customData no longer matches

                system.tracked.Add(life);
            }
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
            {
                return;
            }

            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                DustLife life = tracked[i];
                Dust dust = life.Dust;

                if (!dust.active || !ReferenceEquals(dust.customData, life))
                {
                    tracked.RemoveAt(i); // vanilla killed it, or the slot now belongs to another dust
                    continue;
                }

                life.Age++;

                if (life.Age >= life.Lifetime)
                {
                    dust.active = false;
                    tracked.RemoveAt(i);
                    continue;
                }

                dust.scale = life.StartScale * (1f - (float)life.Age / life.Lifetime);
            }
        }

        public override void OnWorldUnload()
        {
            tracked.Clear();
            lastSpawn.Clear();
        }
    }
}