using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.SuperWeapons
{
    // The flying missile (super weapons patch S4.2): ONE projectile that stands for the whole 7 x 3 weapon.
    //
    // AUTHORITY: the server / single-player spawns it (SuperWeaponSystem.Launch) and is its owner. It damages nothing and ignores tiles and
    // water, so it flies over everything along its own row. SYNC (V8): vanilla sends the spawn packet with ai[0..2]; after that every machine
    // runs this same AI, and the motion is a pure function of ai[0] (ticks flown, counted locally), ai[1] (facing, +1 / -1) and ai[2] (launch
    // x), so no velocity needs to be sent per tick. netImportant makes a joining client receive it too. Only the server / single-player
    // decides when it ends (range now, the wall hit in S4.3) and tells the clients with a kill packet.
    //
    // Art (Ismail): Content/Tiles/SuperWeapons/Missile.png, 144 x 48, facing right: the whole weapon as one picture with the overhangs
    // included (16 px rear plate + thruster column 16 + tank 64 + drill 32 + 16 px tip). It is drawn centred on the projectile and mirrored
    // for a left-facing weapon (D9). Until the PNG exists a grey block is drawn.
    public class SuperWeaponMissile : ModProjectile
    {
        public const string ArtPath = "Factorraria/Content/Tiles/SuperWeapons/Missile";

        const int ArtWidth = 144;
        const int ArtHeight = 48;

        // Any existing image: the real drawing is done by PreDraw, so this only has to load.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bullet;

        Asset<Texture2D> art;

        public override void SetStaticDefaults()
        {
            if (!Main.dedServ && ModContent.HasAsset(ArtPath))
                art = ModContent.Request<Texture2D>(ArtPath);
        }

        public override void SetDefaults()
        {
            Projectile.width = 112;
            Projectile.height = 48;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 60;
            Projectile.netImportant = true;
            Projectile.damage = 0;
        }

        public override bool? CanHitNPC(NPC target) => false;

        public override bool CanHitPlayer(Player target) => false;

        public override bool CanHitPvp(Player target) => false;

        public override void AI()
        {
            SuperWeaponSystem system = ModContent.GetInstance<SuperWeaponSystem>();
            int facing = Projectile.ai[1] < 0f ? -1 : 1;

            // The same counter on every machine, so the speed profile matches everywhere without a packet per tick.
            Projectile.ai[0]++;
            float speed = Math.Min(system.MissileMaxSpeedPx, system.MissileAccel * Projectile.ai[0]);
            Projectile.velocity = new Vector2(facing * speed, 0f);
            Projectile.direction = facing;
            Projectile.spriteDirection = facing;
            Projectile.rotation = 0f;

            // Range: every side can end it the same way (same numbers), the server's kill packet only makes sure.
            if (Math.Abs(Projectile.Center.X - Projectile.ai[2]) > system.MissileMaxRangeTiles * 16f)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    SuperWeaponSystem.Say("[Missile] fizzled: flew " + system.MissileMaxRangeTiles + " tiles without reaching a shielded wall");

                End();
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // S4.2 STUB: the wall check without the explosion. S4.3 replaces this with the explosion and BreakShield().
                NPC wall = SuperWeaponSystem.FindShieldedWall();
                if (wall != null && Math.Abs(wall.Center.X - Projectile.Center.X) < system.HitDistancePx)
                {
                    SuperWeaponSystem.Say("[Missile] reached the wall at tile x " + (int)(wall.Center.X / 16f) + " (S4.2 stub: no explosion, the shield stays up; S4.3 breaks it)");
                    End();
                    return;
                }
            }

            if (!Main.dedServ)
                Flames(facing, speed / Math.Max(1f, system.MissileMaxSpeedPx));
        }

        // Server / single-player: remove it and tell the clients. A client just removes its own copy.
        void End()
        {
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.KillProjectile, -1, -1, null, Projectile.identity, Projectile.owner);

            Projectile.Kill();
        }

        void Flames(int facing, float speedFraction)
        {
            if (!BaseMachine.IsOnScreen(Projectile.Center, 800f))
                return;

            float rearX = Projectile.Center.X - facing * (ArtWidth / 2f);
            for (int row = -1; row <= 1; row++)
            {
                Vector2 at = new Vector2(rearX, Projectile.Center.Y + row * 16f);
                Vector2 velocity = new Vector2(-facing * Main.rand.NextFloat(3f, 7f) * (0.6f + speedFraction) + Projectile.velocity.X * 0.5f, Main.rand.NextFloat(-0.6f, 0.6f));
                Dust fire = Dust.NewDustPerfect(at, DustID.Torch, velocity, 0, default, Main.rand.NextFloat(1.5f, 2.6f));
                fire.noGravity = true;

                if (Main.rand.NextBool(2))
                {
                    Dust smoke = Dust.NewDustPerfect(at, DustID.Smoke, velocity * 0.4f, 100, default, 1.8f);
                    smoke.noGravity = true;
                }

                Lighting.AddLight(at, 1f, 0.6f, 0.2f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteEffects effects = Projectile.ai[1] < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 center = Projectile.Center - Main.screenPosition;

            if (art != null && art.IsLoaded)
            {
                Texture2D texture = art.Value;
                Main.EntitySpriteDraw(texture, center, null, lightColor, 0f, texture.Size() / 2f, 1f, effects, 0);
                return false;
            }

            // No PNG yet: a grey block the size of the art, with a lighter tip. TextureAssets.MagicPixel is not 1x1, so the source rectangle is.
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new Rectangle(0, 0, 1, 1);
            int facing = Projectile.ai[1] < 0f ? -1 : 1;
            Vector2 body = new Vector2(ArtWidth - 16f, ArtHeight);
            Vector2 bodyCenter = center - new Vector2(facing * 8f, 0f);
            Main.EntitySpriteDraw(pixel, bodyCenter, one, new Color(96, 98, 106) * 0.9f, 0f, new Vector2(0.5f, 0.5f), body, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(pixel, center + new Vector2(facing * (ArtWidth / 2f - 8f), 0f), one, new Color(150, 160, 190) * 0.9f, 0f, new Vector2(0.5f, 0.5f), new Vector2(16f, 24f), SpriteEffects.None, 0);
            return false;
        }

        /// <summary>True while any missile is flying (the trigger lets only one weapon fire per wall).</summary>
        public static bool AnyInFlight()
        {
            int type = ModContent.ProjectileType<SuperWeaponMissile>();
            for (int i = 0; i < Main.maxProjectiles; i++)
                if (Main.projectile[i].active && Main.projectile[i].type == type)
                    return true;

            return false;
        }

        /// <summary>Server / single-player (debug /fweapon reset): removes every missile in flight. Returns how many.</summary>
        public static int KillAll()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return 0;

            int type = ModContent.ProjectileType<SuperWeaponMissile>();
            int killed = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.type != type)
                    continue;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.KillProjectile, -1, -1, null, projectile.identity, projectile.owner);

                projectile.Kill();
                killed++;
            }

            return killed;
        }
    }
}