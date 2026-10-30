using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.SuperWeapons
{
    // The missile's impact explosion (super weapons patch S4.4). CLIENT ONLY and cosmetic: SuperWeaponNet.PlayLocal (Impact) calls Start on
    // every client (and in single-player); the server never runs it. Each explosion is a small timeline stepped once per tick:
    //   tick 0        the core fireball (hundreds of fire and smoke particles) and a screen flash
    //   ticks 0..40   an expanding shockwave ring of smoke
    //   ticks 0..30   a rising fire column that ends in a smoke cap
    //   ticks 4..32   secondary blasts around the impact, each with its own bang
    //   up to the end lingering smoke, and a light that fades out
    // Everything is placeholder dust and sounds. The tunables are instance fields so hot reload can change them.
    public class SuperWeaponExplosionFxSystem : ModSystem
    {
        /// <summary>Total life of one explosion, in ticks.</summary>
        public int DurationTicks = 210;

        /// <summary>How far the shockwave ring grows per tick, in pixels.</summary>
        public float ShockwaveSpeedPx = 32f;

        /// <summary>How many ticks the shockwave ring keeps growing.</summary>
        public int ShockwaveTicks = 42;

        /// <summary>Ticks the white screen flash takes to fade.</summary>
        public int FlashTicks = 16;

        /// <summary>Peak opacity of the flash right next to the explosion (0 to 1).</summary>
        public float FlashMaxAlpha = 0.8f;

        /// <summary>The flash is gone for a player this far (in pixels) from the explosion.</summary>
        public float FlashRangePx = 3200f;

        class Blast
        {
            public Vector2 Position;
            public int Age;
        }

        readonly List<Blast> blasts = new List<Blast>();

        public override void OnWorldLoad() => blasts.Clear();

        public override void OnWorldUnload() => blasts.Clear();

        /// <summary>Client / single-player: begins one explosion at a world position.</summary>
        public void Start(Vector2 position)
        {
            if (Main.dedServ)
                return;

            blasts.Add(new Blast { Position = position });
            CoreBurst(position);
        }

        void CoreBurst(Vector2 at)
        {
            for (int i = 0; i < 220; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(4f, 20f);
                Dust fire = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(50f, 50f), DustID.Torch, velocity, 0, default, Main.rand.NextFloat(2.4f, 4.4f));
                fire.noGravity = true;
            }

            for (int i = 0; i < 120; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(2f, 12f);
                Dust smoke = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(70f, 70f), DustID.Smoke, velocity, 100, default, Main.rand.NextFloat(2.2f, 3.6f));
                smoke.noGravity = true;
            }
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || blasts.Count == 0)
                return;

            for (int i = blasts.Count - 1; i >= 0; i--)
            {
                Blast blast = blasts[i];
                Step(blast);
                blast.Age++;
                if (blast.Age >= DurationTicks)
                    blasts.RemoveAt(i);
            }
        }

        void Step(Blast blast)
        {
            int age = blast.Age;
            Vector2 at = blast.Position;

            // Light: bright orange that fades over the whole life.
            float fade = 1f - age / (float)Math.Max(1, DurationTicks);
            Lighting.AddLight(at, 3f * fade, 1.8f * fade, 0.7f * fade);

            // Shockwave: a ring of smoke pushed outwards, denser as it grows.
            if (age < ShockwaveTicks)
            {
                float radius = 40f;// + age * ShockwaveSpeedPx;
                int count = (int)MathHelper.Clamp(14f + radius / 16f, 14f, 80f);
                for (int k = 0; k < count; k++)
                {
                    Vector2 direction = Main.rand.NextVector2Circular(1f,1f);
                    Dust ring = Dust.NewDustPerfect(at + direction * radius, DustID.Smoke, direction * Main.rand.NextFloat(15f,30f), 120, default, 2.2f);
                    ring.noGravity = true;
                }
            }

            //// Fire column rising from the impact, ending in a wide smoke cap.
            //if (age < 30)
            //{
            //    float height = age * 11f;
            //    for (int k = 0; k < 7; k++)
            //    {
            //        Vector2 where = at + new Vector2(Main.rand.NextFloat(-40f, 40f) * (1f - age / 45f), -height + Main.rand.NextFloat(-20f, 20f));
            //        Dust column = Dust.NewDustPerfect(where, DustID.Torch, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-6f, -2f)), 0, default, Main.rand.NextFloat(2f, 3.4f));
            //        column.noGravity = true;
            //    }
            //}

            //if (age == 30)
            //{
            //    Vector2 top = at + new Vector2(0f, -330f);
            //    for (int k = 0; k < 70; k++)
            //    {
            //        Vector2 direction = Main.rand.NextVector2Circular(1f, 0.45f);
            //        Dust cap = Dust.NewDustPerfect(top + direction * 40f, DustID.Smoke, direction * Main.rand.NextFloat(3f, 9f), 100, default, Main.rand.NextFloat(2.6f, 4f));
            //        cap.noGravity = true;
            //    }
            //}

            // Secondary blasts around the impact, each with a bang.
            if (age >= 4 && age <= 32 && age % 4 == 0)
            {
                Vector2 spot = at + Main.rand.NextVector2Circular(220f, 160f);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = Main.rand.NextFloat(-0.6f, 0.2f) }, spot);
                for (int k = 0; k < 60; k++)
                {
                    Dust fire = Dust.NewDustPerfect(spot + Main.rand.NextVector2Circular(20f, 20f), DustID.Torch, Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(3f, 11f), 0, default, Main.rand.NextFloat(2f, 3.4f));
                    fire.noGravity = true;
                }

                for (int k = 0; k < 20; k++)
                {
                    Dust smoke = Dust.NewDustPerfect(spot, DustID.Smoke, Main.rand.NextVector2Circular(5f, 5f), 100, default, 2.4f);
                    smoke.noGravity = true;
                }
            }

            // Lingering smoke drifting up until the end.
            //if (age < DurationTicks - 15)
            //{
            //    for (int k = 0; k < 3; k++)
            //    {
            //        Dust smoke = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(160f, 100f) + new Vector2(0f, -age * 2f), DustID.Smoke, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.5f, -0.5f)), 120, default, Main.rand.NextFloat(2f, 3.2f));
            //        smoke.noGravity = true;
            //    }
            //}
        }

        // The flash: a white screen cover that fades fast, weaker the farther the local player is.
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (blasts.Count == 0)
                return;

            int inventory = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventory == -1)
                return;

            layers.Insert(inventory, new LegacyGameInterfaceLayer("Factorraria: Explosion Flash", delegate
            {
                DrawFlash();
                return true;
            }, InterfaceScaleType.UI));
        }

        void DrawFlash()
        {
            if (Main.LocalPlayer == null || !Main.LocalPlayer.active)
                return;

            float alpha = 0f;
            foreach (Blast blast in blasts)
            {
                if (blast.Age >= FlashTicks)
                    continue;

                float time = 1f - blast.Age / (float)Math.Max(1, FlashTicks);
                float near = MathHelper.Clamp(1f - Vector2.Distance(Main.LocalPlayer.Center, blast.Position) / Math.Max(1f, FlashRangePx), 0f, 1f);
                alpha = Math.Max(alpha, time * time * near * FlashMaxAlpha);
            }

            if (alpha <= 0.01f)
                return;

            // TextureAssets.MagicPixel is not 1x1, so the source rectangle is.
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Rectangle(0, 0, 1, 1), new Color(255, 245, 220) * alpha);
        }
    }
}