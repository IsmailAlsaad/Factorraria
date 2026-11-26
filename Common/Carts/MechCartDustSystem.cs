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
            Vector2 offset = new Vector2(-24f, 6f);
            int spawnInterval = 3;      // ticks between puffs (constant rate)
            int lifetime = 30;          // ticks every puff lives (constant lifetime)
            float startScale = 1.2f;    // scale at spawn; it shrinks linearly to 0 over the lifetime
            float minSpeed = 0.1f;      // the cart counts as moving above this X speed
            // --------------------------------------------------------------

            if (Math.Abs(cart.AlongSpeed) < minSpeed)
            {
                return;
            }

            if (Main.GameUpdateCount % (uint)Math.Max(1, spawnInterval) != 0)
            {
                return;
            }

            Vector2 spawn = cart.Position + CartGeometry.Right(cart) * (cart.Facing * offset.X) + CartGeometry.Up(cart) * offset.Y;

            if (!BaseMachine.IsOnScreen(spawn))
            {
                return;
            }

            Dust dust = Dust.NewDustPerfect(spawn, DustID.TheDestroyer, Vector2.Zero, 0, default, startScale);

            if (!dust.active)
            {
                return; // dust limit reached
            }

            DustLife life = new DustLife { Dust = dust, Age = 0, Lifetime = Math.Max(1, lifetime), StartScale = startScale };
            dust.noGravity = true;
            dust.velocity = Vector2.Zero;
            dust.scale = startScale;
            dust.customData = life; // also the ownership token: if the slot is reused by another dust, customData no longer matches

            ModContent.GetInstance<MechCartDustSystem>().tracked.Add(life);
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
        }
    }
}