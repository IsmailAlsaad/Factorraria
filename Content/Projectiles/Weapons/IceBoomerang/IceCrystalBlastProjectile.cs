using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Projectiles.Weapons.IceBoomerang
{
    /// <summary>
    /// The explosion at a crystal the boomerang bounced off. Spawned by the boomerang's owner (so its damage goes through the
    /// normal owner-client hit path), lives a few ticks, hits every enemy in a circle once and chills them.
    /// ai[0]: bounce level (sets how big the burst looks and how high the sound pitch is). localAI[0]: age (visual only).
    /// </summary>
    public class IceCrystalBlastProjectile : ModProjectile
    {
        // Never drawn as a sprite (all visuals are dust), so any existing vanilla texture will do.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalShard;

        public override void SetDefaults()
        {
            int size = (int)(IceBoomerangTuning.BlastRadius * 2f);
            Projectile.width = size;
            Projectile.height = size;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = IceBoomerangTuning.BlastLifeTicks;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // each enemy once
            Projectile.alpha = 255;
        }

        // A circle, not the square hitbox.
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float radius = IceBoomerangTuning.BlastRadius;
            Vector2 center = Projectile.Center;
            Vector2 closest = new Vector2(MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right), MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(center, closest) <= radius * radius;
        }

        public override void AI()
        {
            float age = Projectile.localAI[0]++;
            int level = (int)Projectile.ai[0];
            float fade = 1f - age / IceBoomerangTuning.BlastLifeTicks;
            Lighting.AddLight(Projectile.Center, 0.7f * fade, 1f * fade, 1.3f * fade);

            if (age != 0f)
                return;

            IceBoomerangFx.PlayBlast(Projectile.Center, level);
            IceBoomerangFx.Burst(Projectile.Center, 18 + 4 * level, 6f + level, 1.5f);
            IceBoomerangFx.Ring(Projectile.Center, 24, 10f, 6f);
            IceBoomerangFx.Ring(Projectile.Center, 16, 40f, 3f);

            // Make the crystal under the blast flash (every machine does this for its own copy).
            int crystalType = ModContent.ProjectileType<IceCrystalProjectile>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == crystalType && Vector2.DistanceSquared(p.Center, Projectile.Center) < 24f * 24f)
                    p.localAI[1] = IceBoomerangTuning.BlastFlashTicks;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            IceBoomerangFx.Burst(target.Center, 5, 3f, 1f);
            if (Projectile.owner == Main.myPlayer)
                IceBoomerangChill.Apply(target); // runs on the owner's client; vanilla AddBuff tells the server
        }

        public override bool PreDraw(ref Color lightColor)
        {
            return false;
        }
    }
}