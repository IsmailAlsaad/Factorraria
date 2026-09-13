using Factorraria.Content.Configs;
using Luminance.Common.Easings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Projectiles.Weapons
{
    public class IceBladeSwingProjectile : ModProjectile
    {
        private float START_ANGLE = MathHelper.ToRadians(-30f); // Starting angle of the swing
        private float END_ANGLE = MathHelper.ToRadians(60f); // Ending angle of the swing
        private float SWING_SPEED = 0.3f; // Speed of the swing (in seconds)

        private ref float Timer => ref Projectile.ai[1]; // Timer to keep track of progression of each stage
        bool isFacingRight => Main.MouseWorld.X > Owner.MountedCenter.X;

        float TimerCap = 180f; // Maximum value of the timer

        Player Owner => Main.player[Projectile.owner];
        FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
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


            DrawOriginOffsetX = (int)config.OffsetX;
            DrawOriginOffsetY = (int)config.OffsetY;
        }

        //public override void OnSpawn(IEntitySource source)
        //{
        //    Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        //}

        public override bool ShouldUpdatePosition()
        {
            return false;
        }

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

        bool KillProjectileCondition() 
        {
            return Timer >= TimerCap;
        }

        float releaseSwingAngle = 0f;
        bool releaseSwingAngleSet = false;
        float toMouseAngle = 0f;
        float AngleOffsetByTimer()
        {
            float angleOffset = START_ANGLE + EasingCurves.Cubic.Evaluate(EasingType.Out,(Timer / TimerCap)) * END_ANGLE;

            if(!releaseSwingAngleSet)
            {
                if (Owner.releaseUseItem)
                {
                    releaseSwingAngle = angleOffset;
                    Timer = 0;
                    TimerCap = SWING_SPEED * 60f;
                    toMouseAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
                    releaseSwingAngleSet = true;
                }
            }
            else
            {
                angleOffset = releaseSwingAngle + EasingCurves.Cubic.Evaluate(EasingType.Out, (Timer / TimerCap)) * toMouseAngle;
            }
            
            return isFacingRight ? -angleOffset : angleOffset - MathHelper.Pi;
        }

        public void SetSwordPosition()
        {   
            Projectile.spriteDirection = isFacingRight ? 1 : -1;
            Owner.direction = Projectile.spriteDirection;

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
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Calculate origin of sword (hilt) based on orientation and offset sword rotation (as sword is angled in its sprite)
            Vector2 origin;
            float rotationOffset;
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
                rotationOffset = MathHelper.PiOver2;
                effects = SpriteEffects.FlipHorizontally;
            }

            Texture2D texture = TextureAssets.Projectile[Type].Value;

            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, default, lightColor * Projectile.Opacity, Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);

            // Since we are doing a custom draw, prevent it from normally drawing
            return false;
        }

        //public void SetSwordPosition()
        //{
        //    Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        //    Owner.direction = Projectile.spriteDirection;

        //    Vector2 toMouse = Main.MouseWorld - Owner.MountedCenter;
        //    float targetRotation = toMouse.ToRotation();
        //    Projectile.rotation = targetRotation + MathHelper.ToRadians(config.AngleOffset);
        //    Projectile.rotation += Projectile.spriteDirection == 1 ? 0 : MathHelper.ToRadians(config.AngleOffset2);

        //    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, targetRotation - MathHelper.PiOver2);
        //    Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, targetRotation - MathHelper.PiOver2);

        //    if (Owner.gravDir == -1f)
        //    {
        //        Projectile.rotation = 0f - Projectile.rotation;
        //        armPosition.Y = Owner.Bottom.Y + (Owner.position.Y - armPosition.Y);
        //    }

        //    armPosition.Y += Owner.gfxOffY;

        //    Vector2 NormalOffset = new Vector2(toMouse.Y, -toMouse.X).SafeNormalize(Vector2.Zero) * config.OffsetY;
        //    Projectile.Center = armPosition + Vector2.Normalize(toMouse) * config.OffsetX + NormalOffset * Projectile.spriteDirection;
        //    Projectile.scale = Owner.GetAdjustedItemScale(Owner.HeldItem);

        //    Owner.heldProj = Projectile.whoAmI;
        //}
    }
}
