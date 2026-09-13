using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static tModPorter.ProgressUpdate;

namespace Factorraria.Content.Projectiles.Weapons
{
    public class IceBladeSwingProjectile : ModProjectile
    {
        Vector2 SwordHoldOffset = new Vector2(-14, 16);
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


            //DrawOriginOffsetX = (int)config.OffsetX;
            //DrawOriginOffsetY = (int)config.OffsetY;
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
            return Owner.releaseUseItem || Timer >= TimerCap;
        }

        float TimerAngleOffset()
        {
            float angleOffset = (Timer / TimerCap) * MathHelper.PiOver2;
            return isFacingRight ? -angleOffset : angleOffset;
        }

        public void SetSwordPosition()
        {
            Projectile.spriteDirection = isFacingRight ? 1 : -1;
            Owner.direction = Projectile.spriteDirection;

            Vector2 toMouse = Main.MouseWorld - Owner.MountedCenter;
            //Main.NewText("toMouse: " + toMouse.ToString());
            toMouse = toMouse.RotatedBy(TimerAngleOffset());
            //Main.NewText("toMouseRotated: " + toMouse.ToString());

            float targetRotation = toMouse.ToRotation();
            Projectile.rotation = targetRotation;
            Projectile.rotation += Projectile.spriteDirection == 1 ? 0 : MathHelper.ToRadians(180f);

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, targetRotation - MathHelper.PiOver2);
            Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, targetRotation - MathHelper.PiOver2);

            if (Owner.gravDir == -1f)
            {
                Projectile.rotation = 0f - Projectile.rotation;
                armPosition.Y = Owner.Bottom.Y + (Owner.position.Y - armPosition.Y);
            }

            armPosition.Y += Owner.gfxOffY;

            Vector2 NormalOffset = new Vector2(toMouse.Y, -toMouse.X).SafeNormalize(Vector2.Zero) * 16f;
            Projectile.Center = armPosition + Vector2.Normalize(toMouse) * 16f + NormalOffset * Projectile.spriteDirection;
            Projectile.scale = Owner.GetAdjustedItemScale(Owner.HeldItem);

            Owner.heldProj = Projectile.whoAmI;
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
