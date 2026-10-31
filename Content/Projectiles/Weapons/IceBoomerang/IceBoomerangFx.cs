using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Factorraria.Content.Projectiles.Weapons.IceBoomerang
{
    /// <summary>All dust and sound for the Ice Boomerang (vanilla dust and sounds only), so a later juice pass edits one file.</summary>
    public static class IceBoomerangFx
    {
        public static void Burst(Vector2 center, int count, float speed, float scale)
        {
            if (Main.dedServ)
                return;
            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(speed, speed);
                int dustType = Main.rand.NextBool(3) ? DustID.IceTorch : DustID.Ice;
                Dust dust = Dust.NewDustPerfect(center, dustType, velocity, 100, default, scale * Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = true;
            }
        }

        public static void Ring(Vector2 center, int count, float radius, float outwardSpeed)
        {
            if (Main.dedServ)
                return;
            for (int i = 0; i < count; i++)
            {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 direction = angle.ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(center + direction * radius, DustID.IceTorch, direction * outwardSpeed, 100, default, 1.3f);
                dust.noGravity = true;
            }
        }

        // Frost gathering toward a point (used while the boomerang settles).
        public static void Gather(Vector2 center, float radius)
        {
            if (Main.dedServ)
                return;
            Vector2 offset = Main.rand.NextVector2CircularEdge(radius, radius);
            Dust dust = Dust.NewDustPerfect(center + offset, DustID.Ice, -offset * 0.12f, 100, default, 1.1f);
            dust.noGravity = true;
        }

        public static void PlayFormed(Vector2 position)
        {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.8f, PitchVariance = 0.2f }, position);
        }

        // The pitch rises with every crystal the boomerang has bounced off.
        public static void PlayBlast(Vector2 position, int level)
        {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.85f, Pitch = MathHelper.Min(0.9f, -0.3f + 0.15f * level) }, position);
        }

        public static void PlayCatch(Vector2 position)
        {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.6f, Pitch = 0.5f }, position);
        }

        public static void PlayShatter(Vector2 position)
        {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, PitchVariance = 0.25f }, position);
        }
    }
}