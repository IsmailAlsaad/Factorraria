using Factorraria.Common.Mounts;
using Factorraria.Content.Items.Mounts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Content.Projectiles.Mounts
{
    // One arm of the Mech Suit. Unlike the handheld MechanicalArmProjectile (thrown, physics-driven,
    // OnTileCollide-latched), the target tile here is already known up front from the search —
    // so this just travels in a straight line to that tile's center and stops. Simpler on purpose.
    public class MechSuitArmProjectile : ModProjectile
    {
        const float UpperArmLength = 100f;
        const float ForearmLength = 100f;
        const float FlySpeed = 24f;

        public Point AnchorTile;
        public float ArmBendSign = 1f;

        float fallbackAngle;
        bool fallbackAngleSet;

        public bool Latched => Projectile.ai[0] == 1f;

        static Asset<Texture2D> ArmSegmentTexture;
        static Asset<Texture2D> JointTexture;

        Vector2 AnchorWorldPos => new Vector2(AnchorTile.X * 16 + 8, AnchorTile.Y * 16 + 8);

        public override void Load()
        {
            if (Main.dedServ) return;
            // Reuses the same arm art as the handheld Mechanical Arm hook.
            ArmSegmentTexture = ModContent.Request<Texture2D>("Factorraria/Content/Projectiles/Hooks/MechanicalArmBeam");
            JointTexture = ModContent.Request<Texture2D>("Factorraria/Content/Projectiles/Hooks/MechanicalArmJoint");
        }

        public override void Unload()
        {
            ArmSegmentTexture = null;
            JointTexture = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false; // target is already a validated tile from the search, no need to physically collide en route
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 36000; // lifetime is managed by MechSuitPlayer, not vanilla despawn
        }

        public override bool? CanDamage() => false;

        // Fully manual position control — no vanilla velocity integration to fight.
        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            MechSuitPlayer mechPlayer = player.GetModPlayer<MechSuitPlayer>();

            if (!player.active || player.dead || !mechPlayer.IsActive)
            {
                Projectile.Kill();
                return;
            }

            if (!fallbackAngleSet)
            {
                fallbackAngle = (AnchorWorldPos - player.Center).ToRotation();
                fallbackAngleSet = true;
            }

            if (!Latched)
            {
                Vector2 toTarget = AnchorWorldPos - Projectile.Center;
                float distance = toTarget.Length();

                if (distance <= FlySpeed)
                {
                    Projectile.Center = AnchorWorldPos;
                    Projectile.ai[0] = 1f;
                }
                else
                {
                    Projectile.Center += toTarget / distance * FlySpeed;
                    Projectile.rotation = toTarget.ToRotation();
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];

            Vector2 shoulder = player.Center;
            Vector2 head = Projectile.Center;
            Vector2 joint = MechanicalArmIK.SolveElbow(shoulder, head, UpperArmLength, ForearmLength, ArmBendSign, fallbackAngle);

            Texture2D beam = ArmSegmentTexture.Value;
            Texture2D headTexture = TextureAssets.Projectile[Type].Value;
            Color drawColor = Lighting.GetColor((int)(head.X / 16f), (int)(head.Y / 16f));

            MechanicalArmIK.DrawSegment(Main.spriteBatch, beam, shoulder, joint, drawColor);
            MechanicalArmIK.DrawSegment(Main.spriteBatch, beam, joint, head, drawColor);

            Texture2D jointTexture = JointTexture.Value;
            Vector2 jointOrigin = jointTexture.Size() / 2f;
            Main.spriteBatch.Draw(jointTexture, joint - Main.screenPosition, null, drawColor, 0f, jointOrigin, 1f, SpriteEffects.None, 0f);

            Vector2 headOrigin = headTexture.Size() / 2f;
            Main.spriteBatch.Draw(headTexture, head - Main.screenPosition, null, drawColor, Projectile.rotation, headOrigin, 1f, SpriteEffects.None, 0f);

            return false;
        }
    }
}