using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Projectiles.Weapons.IceBoomerang
{
    /// <summary>
    /// The thrown Ice Boomerang. It flies straight at the cursor (braking gradually at the end) and never comes back by itself:
    /// it stops at the cursor or on a tile (it pierces enemies on the way out), spins down, and turns into an IceCrystalProjectile
    /// right there. If it touches a crystal first, it ricochets through all of the owner's crystals and then returns.
    /// MULTIPLAYER: the owner's client decides everything (stop point, crystal spawn); other machines only run the visuals.
    /// ai[0]: phase (0 outbound, 1 settling, 2 chain, 3 returning, 4 spent; synced through netUpdate when it flips)
    /// ai[1]: outbound = throw range in pixels (set at spawn by the item); chain = identity of the crystal it flies to
    /// ai[2]: outbound = distance flown so far (every machine adds it the same way); chain/returning = bounce level
    /// localAI[0]: settle timer, localAI[1]: spin direction, localAI[2]: ticks since it left the outbound phase (visual/safety only)
    /// CHAIN: the owner's client picks each next crystal (nearest not yet bounced off), spawns the blast projectile, scales the
    /// damage and flips ai[1] with netUpdate; every machine flies toward the crystal named in ai[1] on its own.
    /// </summary>
    public class IceBoomerangProjectile : ModProjectile
    {
        // Vanilla art until the art file below exists.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBoomerang;

        static Asset<Texture2D> customTexture;

        enum Phase { Outbound = 0, Settling = 1, Chain = 2, Returning = 3, Spent = 4 }

        Phase CurrentPhase => (Phase)(int)Projectile.ai[0];
        bool Settling => CurrentPhase == Phase.Settling;
        bool Spent => CurrentPhase == Phase.Spent; // the crystal already formed; every machine removes its own copy next tick

        static int CrystalType => ModContent.ProjectileType<IceCrystalProjectile>();

        List<int> visited;     // owner only: identities of the crystals already bounced off in this chain
        Vector2 sweepStart;    // center at the start of this tick, for the swept hit test

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

        // Settling is just a visual; every other phase hurts.
        public override bool? CanDamage()
        {
            return Settling || Spent ? false : (bool?)null;
        }

        // Swept hit test: a fast chain boomerang must not tunnel through an enemy between two ticks.
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (Settling || Spent)
                return false;
            Vector2 half = Projectile.Size * 0.5f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft() - half, targetHitbox.Size() + Projectile.Size, sweepStart, Projectile.Center);
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
            Phase phase = CurrentPhase;
            sweepStart = Projectile.Center;
            Projectile.tileCollide = phase == Phase.Outbound; // chain and return fly through tiles (set on every machine)

            if (Projectile.localAI[1] == 0f)
                Projectile.localAI[1] = Projectile.velocity.X >= 0f ? 1f : -1f;
            float spinDirection = Projectile.localAI[1];

            int level = phase >= Phase.Chain ? (int)Projectile.ai[2] : 0;
            Lighting.AddLight(Projectile.Center, 0.2f + 0.05f * level, 0.45f + 0.05f * level, 0.65f + 0.05f * level);

            if (phase != Phase.Outbound)
            {
                Projectile.timeLeft = Math.Max(Projectile.timeLeft, 60); // vanilla's countdown must not end a chain
                if (++Projectile.localAI[2] > IceBoomerangTuning.FlightMaxTicks)
                {
                    Projectile.Kill(); // safety net against a copy that lost its owner's updates
                    return;
                }
            }

            switch (phase)
            {
                case Phase.Outbound:
                    AiOutbound(isOwner, spinDirection);
                    break;
                case Phase.Settling:
                    AiSettling(isOwner, spinDirection);
                    break;
                case Phase.Chain:
                    AiChain(isOwner, spinDirection);
                    break;
                case Phase.Spent:
                    Projectile.Kill();
                    break;
                default:
                    AiReturning(owner, spinDirection);
                    break;
            }
        }

        void FlightDust(int level)
        {
            int dusts = (Main.rand.NextBool(2) ? 1 : 0) + level;
            for (int i = 0; i < dusts; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? DustID.IceTorch : DustID.Ice,
                    Projectile.velocity * -0.1f + Main.rand.NextVector2Circular(1f, 1f), 100, default, 1.1f + 0.1f * level);
                dust.noGravity = true;
            }
        }

        void AiOutbound(bool isOwner, float spinDirection)
        {
            Projectile.rotation += 0.45f * spinDirection;
            FlightDust(0);

            // Touching a crystal starts the ricochet chain (enemies pierced on the way out do not stop the throw).
            if (isOwner)
            {
                Projectile crystal = FindCrystalTouching();
                if (crystal != null)
                {
                    BeginChain(crystal);
                    return;
                }
            }

            float speed = Projectile.velocity.Length();
            if (speed < 0.01f)
            {
                if (isOwner)
                    BeginSettle();
                return;
            }
            Vector2 direction = Projectile.velocity / speed;

            float range = Projectile.ai[1];
            float flown = Projectile.ai[2];
            float remaining = range - flown;

            // Gradual stop: over the last BrakeDistance pixels the speed follows the remaining distance, with a minimum crawl.
            float brake = IceBoomerangTuning.BrakeDistance;
            if (remaining < brake)
            {
                float wanted = Math.Max(IceBoomerangTuning.BrakeMinSpeed, IceBoomerangTuning.ThrowSpeed * Math.Max(remaining, 0f) / brake);
                speed = Math.Min(speed, wanted);
                Projectile.velocity = direction * speed;
            }

            Projectile.ai[2] += speed;
            if (isOwner && flown + speed >= range)
            {
                Projectile.Center += direction * Math.Max(0f, remaining); // stop exactly on the cursor
                BeginSettle();
            }
        }

        void AiSettling(bool isOwner, float spinDirection)
        {
            Projectile.velocity = Vector2.Zero;
            Projectile.localAI[0]++;
            float t = MathHelper.Clamp(Projectile.localAI[0] / IceBoomerangTuning.SettleTicks, 0f, 1f);
            float spin = 0.45f * (1f - t);
            Projectile.rotation += spin * spinDirection;

            IceBoomerangFx.Gather(Projectile.Center, 34f * (1f - t) + 8f);

            if (Projectile.localAI[0] >= IceBoomerangTuning.SettleTicks)
            {
                if (isOwner)
                    SpawnCrystal();
                Projectile.Kill(); // every machine ends its own copy on the same timeline
            }
        }

        void AiChain(bool isOwner, float spinDirection)
        {
            int level = (int)Projectile.ai[2];
            Projectile.rotation += (0.45f + 0.04f * level) * spinDirection;
            FlightDust(level);

            Projectile target = FindCrystalById((int)Projectile.ai[1]);
            if (target == null)
            {
                // The crystal expired or was evicted on the way: the owner picks another one, the others wait for the update.
                if (isOwner)
                    ChooseNextTargetOrReturn(Projectile.Center);
                else
                    Projectile.velocity = Vector2.Zero;
                return;
            }

            if (Vector2.Distance(Projectile.Center, target.Center) < 2f)
            {
                if (!isOwner)
                {
                    Projectile.velocity = Vector2.Zero; // arrived; wait for the owner to name the next crystal
                    return;
                }
                ArriveAtCrystal(target);
                if (CurrentPhase != Phase.Chain)
                {
                    SteerToward(Main.player[Projectile.owner].Center, ChainSpeed((int)Projectile.ai[2]));
                    return;
                }
                target = FindCrystalById((int)Projectile.ai[1]);
                if (target == null)
                    return;
                level = (int)Projectile.ai[2];
            }

            SteerToward(target.Center, ChainSpeed(level));
        }

        void AiReturning(Player owner, float spinDirection)
        {
            int level = (int)Projectile.ai[2];
            Projectile.rotation += (0.45f + 0.04f * level) * spinDirection;
            FlightDust(level);

            float speed = ChainSpeed(level);
            if (Vector2.Distance(Projectile.Center, owner.Center) <= Math.Max(speed, 24f))
            {
                IceBoomerangFx.Burst(owner.Center, 8, 3f, 1f);
                IceBoomerangFx.PlayCatch(owner.Center);
                Projectile.Kill(); // caught; the next throw starts from base damage again
                return;
            }
            SteerToward(owner.Center, speed);
        }

        void SteerToward(Vector2 point, float speed)
        {
            Vector2 delta = point - Projectile.Center;
            float distance = delta.Length();
            Projectile.velocity = distance < 0.01f ? Vector2.Zero : delta / distance * Math.Min(speed, distance);
        }

        static float ChainSpeed(int level)
        {
            return Math.Min(IceBoomerangTuning.ThrowSpeed * (1f + IceBoomerangTuning.ChainSpeedGainPerLevel * level), IceBoomerangTuning.ChainMaxSpeed);
        }

        Projectile FindCrystalTouching()
        {
            int type = CrystalType;
            Rectangle box = Projectile.Hitbox;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && p.owner == Projectile.owner && p.ai[1] != 1f && box.Intersects(p.Hitbox))
                    return p;
            }
            return null;
        }

        Projectile FindCrystalById(int identity)
        {
            int type = CrystalType;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && p.owner == Projectile.owner && p.identity == identity && p.ai[1] != 1f)
                    return p;
            }
            return null;
        }

        // ---- owner-only transitions ----

        void BeginSettle()
        {
            Projectile.ai[0] = (float)Phase.Settling;
            Projectile.velocity = Vector2.Zero;
            Projectile.localAI[0] = 0f;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;
        }

        void BeginChain(Projectile firstCrystal)
        {
            visited = new List<int> { firstCrystal.identity };
            Projectile.ai[0] = (float)Phase.Chain;
            Projectile.ai[1] = firstCrystal.identity;
            Projectile.ai[2] = 0f; // bounce level
            Array.Clear(Projectile.localNPCImmunity, 0, Projectile.localNPCImmunity.Length);
            Projectile.netUpdate = true;
        }

        void ArriveAtCrystal(Projectile crystal)
        {
            int level = (int)Projectile.ai[2] + 1;
            Projectile.ai[2] = level;
            Projectile.damage = (int)(Projectile.originalDamage * (1f + IceBoomerangTuning.DamageGainPerLevel * level));
            Array.Clear(Projectile.localNPCImmunity, 0, Projectile.localNPCImmunity.Length); // enemies can be hit again on the next segment

            int blastDamage = Math.Max(1, (int)(Projectile.damage * IceBoomerangTuning.BlastDamageMultiplier));
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), crystal.Center, Vector2.Zero,
                ModContent.ProjectileType<IceCrystalBlastProjectile>(), blastDamage, Projectile.knockBack, Projectile.owner, level);

            IceCrystalProjectile.RegisterBounce(crystal); // costs the crystal 15 s of life (applied on every machine)

            ChooseNextTargetOrReturn(crystal.Center);
            Projectile.netUpdate = true;
        }

        void ChooseNextTargetOrReturn(Vector2 from)
        {
            visited ??= new List<int>();
            int type = CrystalType;
            Projectile best = null;
            float bestDistance = float.MaxValue;
            float maxDistanceSquared = IceBoomerangTuning.ChainMaxTargetDistance * IceBoomerangTuning.ChainMaxTargetDistance;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != type || p.owner != Projectile.owner || p.ai[1] == 1f || visited.Contains(p.identity))
                    continue;
                float distance = Vector2.DistanceSquared(from, p.Center);
                if (distance > maxDistanceSquared)
                    continue; // too far away to be a target (it is still left alone by the chain, not removed)
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = p;
                }
            }

            if (best != null)
            {
                visited.Add(best.identity);
                Projectile.ai[1] = best.identity;
                Projectile.netUpdate = true;
            }
            else
            {
                Projectile.ai[0] = (float)Phase.Returning;
                Projectile.ai[1] = 0f;
                Array.Clear(Projectile.localNPCImmunity, 0, Projectile.localNPCImmunity.Length); // it hits enemies again on the way back
                Projectile.netUpdate = true;
            }
        }

        void SpawnCrystal()
        {
            IceCrystalProjectile.EvictForNewCrystal(Projectile.owner);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<IceCrystalProjectile>(), 0, 0f, Projectile.owner, IceCrystalProjectile.RandomFrame());
        }

        // A tile hit during the outbound throw turns the boomerang into a crystal at once (no spin-down). The
        // boomerang is flagged Spent (synced) instead of being killed on the spot, so every machine removes its own copy.
        void FormCrystalNow()
        {
            SpawnCrystal();
            Projectile.ai[0] = (float)Phase.Spent;
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Projectile.owner == Main.myPlayer && CurrentPhase == Phase.Outbound)
                FormCrystalNow();
            return false; // never die on a tile; it becomes a crystal against the surface
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner != Main.myPlayer)
                return;

            if (CurrentPhase == Phase.Outbound)
            {
                // It pierces enemies on the way out. The boomerang must NOT be moved here: this runs on the owner's client only, a
                // teleport onto the enemy's edge is not seen by the other machines until the next netUpdate and can push the hitbox into a tile.
                Rectangle box = target.Hitbox;
                Vector2 center = Projectile.Center;
                Vector2 contact = new Vector2(MathHelper.Clamp(center.X, box.Left, box.Right), MathHelper.Clamp(center.Y, box.Top, box.Bottom));
                IceBoomerangFx.Burst(contact, 8, 3f, 1.1f);
            }
            else if (CurrentPhase != Phase.Settling && CurrentPhase != Phase.Spent)
            {
                IceBoomerangFx.Burst(target.Center, 4, 3f, 1f); // piercing along the chain
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (CurrentPhase == Phase.Spent)
                return false;
            Texture2D texture = (customTexture != null && customTexture.IsLoaded) ? customTexture.Value : TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            float settleT = Settling ? MathHelper.Clamp(Projectile.localAI[0] / IceBoomerangTuning.SettleTicks, 0f, 1f) : 0f;
            int level = CurrentPhase >= Phase.Chain ? (int)Projectile.ai[2] : 0;
            float scale = MathHelper.Lerp(1f, 0.65f, settleT) * (1f + 0.06f * level);
            Color glow = Color.Lerp(new Color(110, 200, 255, 0), new Color(235, 250, 255, 0), Math.Min(level, 5) / 5f);

            int length = ProjectileID.Sets.TrailCacheLength[Projectile.type];
            for (int i = length - 1; i >= 1; i--)
            {
                Vector2 old = Projectile.oldPos[i];
                if (old == Vector2.Zero)
                    continue;
                float fade = 1f - i / (float)length;
                Main.EntitySpriteDraw(texture, old + Projectile.Size * 0.5f - Main.screenPosition, null, glow * ((0.55f + 0.07f * level) * fade),
                    Projectile.oldRot[i], origin, scale * (0.6f + 0.4f * fade), SpriteEffects.None, 0);
            }

            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, drawPosition, null, glow * 0.5f, Projectile.rotation, origin, scale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, drawPosition, null, Color.Lerp(lightColor, Color.White, 0.6f), Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}