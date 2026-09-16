using Factorraria.Content.Configs;
using Factorraria.Content.Particles;
using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Projectiles.Weapons
{
    public class IceBladeSwingProjectile : ModProjectile, IPixelatedPrimitiveRenderer
    {
        private float START_ANGLE = MathHelper.ToRadians(-30f); // Starting angle of the swing
        private float END_ANGLE = MathHelper.ToRadians(60f); // Ending angle of the swing
        private float SWING_SPEED = 0.25f; // Speed of the swing (in seconds)

        private ref float Timer => ref Projectile.ai[1]; // Timer to keep track of progression of each stage
        float TimerCap = 180f; // Maximum value of the timer

        bool isFacingRight => Main.MouseWorld.X > Owner.MountedCenter.X;
        
        Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.AllowsContactDamageFromJellyfish[Type] = true;
        }
        public override void SetDefaults()
        {
            Projectile.width = 34; // Hitbox width of projectile
            Projectile.height = 40; // Hitbox height of projectile
            Projectile.friendly = true; // Projectile hits enemies
            Projectile.timeLeft = 10000; // Time it takes for projectile to expire
            Projectile.penetrate = -1; // Projectile pierces infinitely
            Projectile.tileCollide = false; // Projectile does not collide with tiles
            Projectile.usesLocalNPCImmunity = true; // Uses local immunity frames
            Projectile.localNPCHitCooldown = -1; // We set this to -1 to make sure the projectile doesn't hit twice
            Projectile.ownerHitCheck = true; // Make sure the owner of the projectile has line of sight to the target (aka can't hit things through tile).
            Projectile.DamageType = DamageClass.Melee; // Projectile is a melee projectile


            DrawOriginOffsetX = 0;
            DrawOriginOffsetY = 0;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            // Kill the projectile if the player dies or gets crowd controlled
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed || KillProjectileCondition())
            {
                Projectile.Kill();
                return;
            }

            SetSwordPosition();
            Timer++;
        }

        public void RenderPixelatedPrimitives(SpriteBatch spriteBatch)
        {
            PrimitiveRenderer.RenderTrail(
                Projectile.oldPos,new(
                    t => MathHelper.Lerp(20f, 0f, t), // Width Function
                    _ => Color.White, // Color Function
                    _ => new Vector2(20f,-20f).RotatedBy(Projectile.rotation), // Offset Function
                true,
                true),
                20);
        }

        public override bool PreDraw(ref Color lightColor)
        {

            // Calculate origin of sword (hilt) based on orientation and offset sword rotation (as sword is angled in its sprite)
            Vector2 origin;
            float rotationOffset = 0f;
            SpriteEffects effects;

            if (Projectile.spriteDirection > 0)
            {
                origin = new Vector2(0, Projectile.height);
                rotationOffset = 0f;
                effects = SpriteEffects.None;
            }
            else
            {
                origin = new Vector2(Projectile.width, Projectile.height);
                rotationOffset = -MathHelper.Pi;
                effects = SpriteEffects.FlipHorizontally;
            }

            Texture2D texture = TextureAssets.Projectile[Type].Value;

            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, default, lightColor * Projectile.Opacity, Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);

            // Since we are doing a custom draw, prevent it from normally drawing
            return false;
        }

        // Helper methods
        bool KillProjectileCondition() 
        {
            return Timer >= TimerCap;
        }
        
        float releaseSwingAngle = 0f;
        bool releaseSwingAngleSet = false;
        float AngleOffsetByTimer()
        {
            float angleOffset = MathHelper.Lerp(START_ANGLE,END_ANGLE,EasingCurves.Cubic.Evaluate(EasingType.Out,Timer/TimerCap));

            if (!releaseSwingAngleSet)
            {
                if (Owner.releaseUseItem)
                {
                    releaseSwingAngle = angleOffset;

                    ReleasedSwing();

                    Timer = 0;
                    TimerCap = SWING_SPEED * 60f;
                    
                    releaseSwingAngleSet = true;
                }
            }
            else
            {
                angleOffset = MathHelper.Lerp(releaseSwingAngle, START_ANGLE - MathHelper.ToRadians(30f), EasingCurves.Cubic.Evaluate(EasingType.InOut, Timer / TimerCap));
            }

            return isFacingRight ? -angleOffset : angleOffset - MathHelper.Pi;
        }
        
        void SetSwordPosition()
        {   
            Projectile.spriteDirection = isFacingRight ? 1 : -1;
            Owner.ChangeDir(Projectile.spriteDirection);

            float currentAngle = AngleOffsetByTimer();

            Projectile.rotation = currentAngle;

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentAngle - MathHelper.PiOver2);
            Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, currentAngle - MathHelper.PiOver2);

            if (Owner.gravDir == -1f)
            {
                Projectile.rotation = 0f - Projectile.rotation;
                armPosition.Y = Owner.Bottom.Y + (Owner.position.Y - armPosition.Y);
            }

            armPosition.Y += Owner.gfxOffY;

            Projectile.Center = armPosition;
            Projectile.scale = Owner.GetAdjustedItemScale(Owner.HeldItem);

            Owner.heldProj = Projectile.whoAmI;

            // Manually update oldPos array
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
            {
                Projectile.oldPos[i] = Projectile.oldPos[i - 1];
            }
            Projectile.oldPos[0] = Projectile.Center;
        }

        void ReleasedSwing()
        {
            // Spawn puff explosion particles
            for (int i = 0; i < 3; i++)
            {
                Vector2 position = Projectile.Center + Main.rand.NextVector2Circular(10f, 10f);
                Vector2 velocity = Main.rand.NextVector2Circular(1f, 1f) * 10f;
                Color color = Color.White;
                int lifetime = Main.rand.Next(20, 40);
                new PuffExplosion(position, velocity, color, lifetime).Spawn();
            }
        }
    }
}
