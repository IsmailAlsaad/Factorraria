using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Projectiles.Weapons.IceBoomerang
{
    /// <summary>
    /// The thrown Ice Boomerang. It flies straight at the cursor and never comes back by itself: it stops at the cursor, on a
    /// tile or on the first enemy it touches, spins down, and turns into an IceCrystalProjectile right there.
    /// MULTIPLAYER: the owner's client decides everything (stop point, crystal spawn); other machines only run the visuals.
    /// ai[0]: 0 = outbound, 1 = settling (synced through netUpdate when it flips)
    /// ai[1]: throw range in pixels (set at spawn by the item, synced with the spawn packet)
    /// ai[2]: distance flown so far (every machine adds it the same way)
    /// localAI[0]: settle timer, localAI[1]: spin direction (visual only)
    /// </summary>
    public class IceBoomerangProjectile : ModProjectile
    {
        // Vanilla art until the art file below exists.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBoomerang;

        static Asset<Texture2D> customTexture;

        bool Settling => Projectile.ai[0] == 1f;

        public override void Load()
        {
            if (Main.dedServ)
                return;
        }

        public override void Unload()
        {
            customTexture = null;
        }

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600; // safety net only; the throw range ends it long before
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        // Only the outbound flight hurts; a settling boomerang is just a visual.
        public override bool? CanDamage()
        {
            return Settling ? false : (bool?)null;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            bool isOwner = Projectile.owner == Main.myPlayer;
            Lighting.AddLight(Projectile.Center, 0.2f, 0.45f, 0.65f);

            if (Projectile.localAI[1] == 0f)
                Projectile.localAI[1] = Projectile.velocity.X >= 0f ? 1f : -1f;
            float spinDirection = Projectile.localAI[1];

            if (!Settling)
            {
                Projectile.rotation += 0.45f * spinDirection;
                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? DustID.IceTorch : DustID.Ice, Projectile.velocity * -0.1f + Main.rand.NextVector2Circular(1f, 1f), 100, default, 1.1f);
                    dust.noGravity = true;
                }

                float speed = Projectile.velocity.Length();
                float flown = Projectile.ai[2];
                Projectile.ai[2] += speed;

                if (isOwner && speed > 0.01f && flown + speed >= Projectile.ai[1])
                {
                    // Reached the cursor this tick: stop exactly on it.
                    Projectile.Center += Projectile.velocity / speed * Math.Max(0f, Projectile.ai[1] - flown);
                    BeginSettle();
                }
            }
            else
            {
                Projectile.velocity = Vector2.Zero;
                Projectile.localAI[0]++;
                float t = MathHelper.Clamp(Projectile.localAI[0] / IceBoomerangTuning.SettleTicks, 0f, 1f);
                float spin = 0.45f * (1f - t);// * (1f - t);
                Projectile.rotation += spin * spinDirection;

                IceBoomerangFx.Gather(Projectile.Center, 34f * (1f - t) + 8f);

                if (Projectile.localAI[0] >= IceBoomerangTuning.SettleTicks)
                {
                    if (isOwner)
                        SpawnCrystal();
                    Projectile.Kill(); // every machine ends its own copy on the same timeline
                }
            }
        }

        void BeginSettle()
        {
            Projectile.ai[0] = 1f;
            Projectile.velocity = Vector2.Zero;
            Projectile.localAI[0] = 0f;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;
        }

        void SpawnCrystal()
        {
            IceCrystalProjectile.EvictForNewCrystal(Projectile.owner);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<IceCrystalProjectile>(), 0, 0f, Projectile.owner);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Projectile.owner == Main.myPlayer && !Settling)
            {
                SpawnCrystal();
                Projectile.Kill();
            }
                //BeginSettle();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner != Main.myPlayer || Settling)
                return;

            // The crystal forms where the boomerang touched the enemy (nearest point of its hitbox), not at the aim point.
            Rectangle box = target.Hitbox;
            Vector2 center = Projectile.Center;
            Projectile.Center = new Vector2(MathHelper.Clamp(center.X, box.Left, box.Right), MathHelper.Clamp(center.Y, box.Top, box.Bottom));
            IceBoomerangFx.Burst(Projectile.Center, 8, 3f, 1.1f);

            //BeginSettle();

            SpawnCrystal();
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = (customTexture != null && customTexture.IsLoaded) ? customTexture.Value : TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            float settleT = Settling ? MathHelper.Clamp(Projectile.localAI[0] / IceBoomerangTuning.SettleTicks, 0f, 1f) : 0f;
            float scale = MathHelper.Lerp(1f, 0.65f, settleT);
            Color glow = new Color(110, 200, 255, 0);

            int length = ProjectileID.Sets.TrailCacheLength[Projectile.type];
            for (int i = length - 1; i >= 1; i--)
            {
                Vector2 old = Projectile.oldPos[i];
                if (old == Vector2.Zero)
                    continue;
                float fade = 1f - i / (float)length;
                Main.EntitySpriteDraw(texture, old + Projectile.Size * 0.5f - Main.screenPosition, null, glow * (0.55f * fade),
                    Projectile.oldRot[i], origin, scale * (0.6f + 0.4f * fade), SpriteEffects.None, 0);
            }

            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, drawPosition, null, glow * 0.5f, Projectile.rotation, origin, scale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, drawPosition, null, Color.Lerp(lightColor, Color.White, 0.6f), Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}