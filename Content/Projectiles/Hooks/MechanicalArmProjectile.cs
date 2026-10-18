using Factorraria.Common.Mounts;
using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Projectiles.Hooks
{
    // A hook that does the opposite of a hook: instead of pulling you toward the latch
    // point, it shoves you away from it once attached, and auto-releases once you've
    // been pushed far enough. Fully custom AI (aiStyle = -1) — none of vanilla's
    // pull-toward-hook logic is involved, so there's nothing to fight or override.
    public class MechanicalArmProjectile : ModProjectile
    {
        // --- TUNABLES ---
        const float UpperArmLength = 100f;   // shoulder -> elbow
        const float ForearmLength = 100f;    // elbow -> head
        const float DetachDistance = 230f;   // slightly more than UpperArm+Forearm, so the
                                            // arm reads as fully taut right as it lets go
        const float PushAcceleration = 0.8f;
        const float MaxPushSpeed = 16f; // 16 is a nice middle ground
        const float playerSpeedCap = 25f; // 16 is a nice middle ground
        Vector2 playerOldSpeed = Vector2.Zero; 
        Vector2 pushDirection = Vector2.Zero;

        static Asset<Texture2D> ArmSegmentTexture;
        static Asset<Texture2D> JointTexture;

        // ai[0]: 0 = flying, 1 = latched, 2 = retracting, 3 = released (the owner pressed Jump; every copy removes itself)
        // ai[1]: bend sign, -1 or 1, locked in on first tick (synced)
        // localAI[0]: fallback "cocked" elbow angle for the near-zero-distance case (visual only, not synced)
        // localAI[1]: 0/1 init flag for the two fields above
        bool Latched => Projectile.ai[0] == 1f;
        bool Retracting => Projectile.ai[0] == 2f;
        bool Released => Projectile.ai[0] == 3f;

        // Owner only: how many AI updates the owner keeps a released arm alive, so the state packet (netUpdate) goes out before it is removed.
        int ReleaseKillTicks = 4;
        int releaseTicks;
        const float RetractSpeed = 14f; // pixels per AI tick while pulling back in

        public override void Load()
        {
            if (Main.dedServ) return;
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
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.aiStyle = -1; // custom — vanilla's hook aiStyle pulls the player IN, which is exactly what we don't want
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 600;
        }

        public override bool? CanDamage() => false;

        // While latched the projectile is frozen in the wall — don't let the normal
        // position-integration step (velocity added to position every frame) touch it.
        public override bool ShouldUpdatePosition() => !Latched && !Retracting; // manual position control in both non-flying states

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            if (Projectile.localAI[1] == 0f)
            {
                Projectile.ai[1] = Projectile.velocity.Y >= 0f ? 1f : -1f;
                float throwAngle = Projectile.velocity.ToRotation();
                Projectile.localAI[0] = throwAngle + (MathHelper.PiOver2 * Projectile.ai[1]);
                Projectile.localAI[1] = 1f;
                Projectile.netUpdate = true;
            }

            if (Released)
            {
                // The owner let go. Remote copies remove themselves as soon as they see the state; Projectile.Kill() on the owner is not
                // known to reach other clients (verification item 7), so the removal does not depend on it.
                if (!FactorrariaNet.IsOwner(player) || --releaseTicks <= 0)
                    Projectile.Kill();
                return;
            }

            if (Retracting)
            {
                UpdateRetract(player);
                return;
            }

            if (!Latched)
            {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            else
            {
                Projectile.timeLeft = 2;

                // The push and the Jump release belong to the owner only. The other machines hold a copy of the player whose position and
                // velocity come from the player packets, so adding velocity there as well pushed it twice.
                if (FactorrariaNet.IsOwner(player))
                {
                    if (playerOldSpeed == Vector2.Zero)
                    {
                        playerOldSpeed = player.velocity;
                    }

                    if (PlayerInput.Triggers.JustPressed.Jump)
                    {
                        Release(); // instant for the owner (hidden at once), synced to everyone else
                        return;
                    }

                    ApplyPush(player);
                }
            }

            if (Vector2.Distance(player.Center, Projectile.Center) >= DetachDistance)
            {
                BeginRetract();
            }
        }

        void UpdateRetract(Player player)
        {
            FactorrariaNet.SyncPlayerNow(player); // the owner is still carrying the push's momentum while the arm reels in

            Vector2 toPlayer = player.Center - Projectile.Center;
            float distance = toPlayer.Length();

            if (distance <= RetractSpeed)
            {
                Projectile.Kill(); // fully reeled in — despawns here, not before
                return;
            }

            Projectile.Center += toPlayer / distance * RetractSpeed;
        }

        void Release()
        {
            Projectile.ai[0] = 3f;
            releaseTicks = ReleaseKillTicks;
            Projectile.netUpdate = true;
        }

        void BeginRetract()
        {
            Projectile.ai[0] = 2f;
            Projectile.tileCollide = false; // was embedded in a tile — don't let it re-collide flying back through the same spot
            Projectile.netUpdate = true;
        }

        // One last player packet when the arm goes away (release, reel-in or death), so the owner's final position and velocity are not stale.
        public override void OnKill(int timeLeft)
        {
            FactorrariaNet.SyncPlayerNow(Main.player[Projectile.owner]);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Latched)
                return false;

            // Lock in the push direction as the OPPOSITE of where the head was traveling/facing
            // on impact — fixed for the rest of this latch, independent of player position.
            pushDirection = oldVelocity != Vector2.Zero
                ? -Vector2.Normalize(oldVelocity)
                : new Vector2(0f, -1f); // degenerate fallback, shouldn't normally hit

            Projectile.velocity = Vector2.Zero;
            Projectile.ai[0] = 1f;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);

            return false;
        }

        void ApplyPush(Player player)
        {
            player.velocity += pushDirection * PushAcceleration;

            float speed = player.velocity.Length();
            if (speed > MaxPushSpeed + playerOldSpeed.Length() || speed > playerSpeedCap)
                player.velocity *= (Math.Min(MaxPushSpeed + playerOldSpeed.Length(), playerSpeedCap)) / speed;

            player.fallStart = (int)(player.position.Y / 16f);

            // The push changes the owner's velocity without any key press, and vanilla only sends the player when it sees a change it watches.
            // Mid-air that happens anyway; on the ground it did not, so the other client saw the owner standing still for a second or two and
            // then a snap. Send the player every tick while pushing (the projectile's AI runs after the players, so the position is current).
            FactorrariaNet.SyncPlayerNow(player);

            if (Main.myPlayer == Projectile.owner && PlayerInput.Triggers.JustPressed.Grapple)
            {
                BeginRetract();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Released)
                return false; // the owner let go: nothing to draw while the copies wait to be removed

            Player player = Main.player[Projectile.owner];

            Vector2 shoulder = player.Center;
            Vector2 head = Projectile.Center;
            Vector2 joint = MechanicalArmIK.SolveElbow(shoulder, head, UpperArmLength, ForearmLength, Projectile.ai[1], Projectile.localAI[0]);

            Texture2D beam = ArmSegmentTexture.Value;
            Texture2D headTexture = TextureAssets.Projectile[Type].Value;
            Color drawColor = Lighting.GetColor((int)(head.X / 16f), (int)(head.Y / 16f));

            MechanicalArmIK.DrawSegment(Main.spriteBatch,beam, shoulder, joint, drawColor);
            MechanicalArmIK.DrawSegment(Main.spriteBatch,beam, joint, head, drawColor);

            Texture2D jointTexture = JointTexture.Value;
            Vector2 jointOrigin = jointTexture.Size() / 2f;
            Main.spriteBatch.Draw(jointTexture, joint - Main.screenPosition, null, drawColor, 0f, jointOrigin, 1f, SpriteEffects.None, 0f);


            Vector2 headOrigin = headTexture.Size() / 2f;
            Main.spriteBatch.Draw(headTexture, head - Main.screenPosition, null, drawColor, Projectile.rotation, headOrigin, 1f, SpriteEffects.None, 0f);

            return false;
        }

        // Multiplayer stuff
        //public override void SendExtraAI(System.IO.BinaryWriter writer)
        //{
        //    writer.WriteVector2(pushDirection);
        //}

        //public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        //{
        //    pushDirection = reader.ReadVector2();
        //}
    }
}