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
    /// A stationary ice crystal left by the Ice Boomerang. Lives 30 s, at most 5 per player (the oldest is evicted), and chills
    /// every enemy that touches it.
    /// MULTIPLAYER: the owner's client spawns it, evicts it and applies the chill. An eviction sets ai[1] = 1 and netUpdate, and
    /// every machine removes its own copy when it sees the flag, so it does not depend on a kill packet.
    /// ai[0]: which frame of the sprite sheet this crystal shows (picked at random by the owner at spawn; ai is sent with the spawn packet,
    /// so every machine draws the same one). ai[1]: 1 = evicted. ai[2]: how many times the boomerang bounced off it; every bounce costs
    /// CrystalBounceLifeLossTicks of life, and each machine applies the new bounces to its own timeLeft (timeLeft itself is not synced).
    /// localAI[0]: age in ticks (visual only, each machine counts its own).
    /// </summary>
    public class IceCrystalProjectile : ModProjectile
    {
        // Vanilla art until the art file below exists.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NorthPoleSnowflake;

        static Asset<Texture2D> customTexture;

        int appliedBounces; // bounces already subtracted from this machine's timeLeft

        public override void Load()
        {
            if (Main.dedServ)
                return;
            //ModContent.RequestIfExists<Texture2D>("Factorraria/Content/Projectiles/Weapons/IceBoomerang/IceCrystalProjectile", out customTexture);
        }

        public override void Unload()
        {
            customTexture = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 38;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = IceBoomerangTuning.CrystalLifeTicks;
        }

        // Picks the sheet frame of a new crystal (the owner passes it as ai0 when it spawns the crystal).
        public static int RandomFrame()
        {
            return Main.rand.Next(Math.Max(1, IceBoomerangTuning.CrystalSheetFrames));
        }

        // Called by the boomerang (owner's client) when it bounces off this crystal. Only the counter is set and synced here;
        // every machine (the owner included) subtracts the life from its own copy in AI().
        public static void RegisterBounce(Projectile crystal)
        {
            crystal.ai[2] += 1f;
            crystal.netUpdate = true;
        }

        // Called by the boomerang (owner's client) right before it spawns a new crystal: flags the oldest ones until only
        // MaxCrystals - 1 are left. The oldest is the one with the least time left.
        public static void EvictForNewCrystal(int ownerIndex)
        {
            int type = ModContent.ProjectileType<IceCrystalProjectile>();
            for (int guard = 0; guard < 32; guard++)
            {
                int count = 0;
                Projectile oldest = null;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.type != type || p.owner != ownerIndex || p.ai[1] == 1f)
                        continue;
                    count++;
                    if (oldest == null || p.timeLeft < oldest.timeLeft)
                        oldest = p;
                }
                if (count < IceBoomerangTuning.MaxCrystals || oldest == null)
                    return;
                oldest.ai[1] = 1f;
                oldest.netUpdate = true;
            }
        }

        public override void AI()
        {
            if (!Main.player[Projectile.owner].active)
            {
                Projectile.Kill();
                return;
            }
            if (Projectile.ai[1] == 1f)
            {
                Projectile.Kill(); // evicted
                return;
            }

            int bounces = (int)Projectile.ai[2];
            if (bounces > appliedBounces)
            {
                Projectile.timeLeft -= (bounces - appliedBounces) * IceBoomerangTuning.CrystalBounceLifeLossTicks;
                appliedBounces = bounces;
                if (Projectile.timeLeft <= 0)
                {
                    Projectile.Kill(); // it had less life left than a bounce costs
                    return;
                }
            }

            float age = Projectile.localAI[0]++;
            if (Projectile.localAI[1] > 0f)
                Projectile.localAI[1]--; // flash after a blast, set by the blast on every machine
            if (age == 0f)
            {
                IceBoomerangFx.PlayFormed(Projectile.Center);
                IceBoomerangFx.Burst(Projectile.Center, 16, 4.5f, 1.3f);
                IceBoomerangFx.Ring(Projectile.Center, 14, 6f, 3.5f);
            }

            float pulse = 0.5f + 0.5f * (float)Math.Sin(age * 0.08f);
            Lighting.AddLight(Projectile.Center, 0.2f + 0.15f * pulse, 0.45f + 0.2f * pulse, 0.7f + 0.2f * pulse);

            if (Main.rand.NextBool(8))
            {
                Vector2 spot = Projectile.position + new Vector2(Main.rand.NextFloat(Projectile.width), Main.rand.NextFloat(Projectile.height));
                Dust dust = Dust.NewDustPerfect(spot, DustID.IceTorch, new Vector2(0f, -0.4f), 100, default, 0.9f);
                dust.noGravity = true;
            }
            if (Projectile.timeLeft < IceBoomerangTuning.ExpiryWarnTicks && Main.rand.NextBool(5))
                IceBoomerangFx.Burst(Projectile.Center, 1, 1.5f, 0.9f);

            if (Projectile.owner == Main.myPlayer && (int)age % IceBoomerangTuning.ChillCheckInterval == 0)
                ChillTouchingEnemies();
        }

        void ChillTouchingEnemies()
        {
            Rectangle box = Projectile.Hitbox;
            box.Inflate(4, 4);
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy(Projectile) && npc.Hitbox.Intersects(box))
                    IceBoomerangChill.Apply(npc);
            }
        }

        public override void OnKill(int timeLeft)
        {
            IceBoomerangFx.Burst(Projectile.Center, 14, 4f, 1.2f);
            IceBoomerangFx.PlayShatter(Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            bool custom = customTexture != null && customTexture.IsLoaded;
            Texture2D texture = custom ? customTexture.Value : TextureAssets.Projectile[Projectile.type].Value;
            // Custom art is a single frame. The vanilla snowflake is a vertical sheet (frames padded by 2 px): draw the frame this crystal picked.
            Rectangle source = texture.Bounds;
            if (!custom)
            {
                int frames = Math.Max(1, IceBoomerangTuning.CrystalSheetFrames);
                int frameHeight = texture.Height / frames;
                int frame = Math.Min((int)Projectile.ai[0], frames - 1);
                source = new Rectangle(0, frameHeight * Math.Max(frame, 0), texture.Width, frames > 1 ? frameHeight - 2 : frameHeight);
            }
            Vector2 origin = new Vector2(source.Width, source.Height) * 0.5f;
            float age = Projectile.localAI[0];

            // Pop-in with a small overshoot, then a slow bob.
            float t = MathHelper.Clamp(age / IceBoomerangTuning.CrystalPopTicks, 0f, 1f);
            float scale = 1f + 2.70158f * (float)Math.Pow(t - 1f, 3) + 1.70158f * (t - 1f) * (t - 1f);
            float bob = (float)Math.Sin(age * 0.06f) * 3f;

            float visible = 1f;
            if (Projectile.timeLeft < IceBoomerangTuning.ExpiryWarnTicks)
                visible = 0.65f + 0.35f * (float)Math.Sin(age * 0.5f);

            Vector2 drawPosition = Projectile.Center + new Vector2(0f, bob) - Main.screenPosition;
            float pulse = 0.5f + 0.5f * (float)Math.Sin(age * 0.08f);
            Color glow = new Color(110, 200, 255, 0);

            // Halo: pulses of the same frame, tinted blue-white and see-through, that grow and fade out. A new one is born every
            // HaloIntervalTicks, so several overlap. They are a pure function of the age, so nothing is stored or synced.
            int interval = Math.Max(1, IceBoomerangTuning.HaloIntervalTicks);
            int haloLife = Math.Max(1, IceBoomerangTuning.HaloLifeTicks);
            Color haloColor = new Color(170, 225, 255, 0);
            for (int pulse = (int)age / interval; pulse >= 0; pulse--)
            {
                int pulseAge = (int)age - pulse * interval;
                if (pulseAge >= haloLife)
                    break;
                float u = pulseAge / (float)haloLife;
                float grow = 1f - (1f - u) * (1f - u); // fast at first, then slows down
                float haloScale = scale * MathHelper.Lerp(1f, IceBoomerangTuning.HaloMaxScale, grow);
                float haloOpacity = IceBoomerangTuning.HaloAlpha * (1f - u) * (1f - u) * visible;
                Main.EntitySpriteDraw(texture, drawPosition, source, haloColor * haloOpacity, 0f, origin, haloScale, SpriteEffects.None, 0);
            }
            float flash = Projectile.localAI[1] / IceBoomerangTuning.BlastFlashTicks;
            Main.EntitySpriteDraw(texture, drawPosition, source, glow * (0.35f + 0.25f * pulse + 0.6f * flash) * visible, 0f, origin, scale * (1.3f + 0.35f * flash), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, drawPosition, source, Color.Lerp(lightColor, Color.White, 0.7f + 0.3f * flash) * visible, 0f, origin, scale * (1f + 0.12f * flash), SpriteEffects.None, 0);
            return false;
        }
    }
}