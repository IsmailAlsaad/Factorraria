using Factorraria.Common.Mounts;
using Factorraria.Common.Net;
using Factorraria.Content.Items.Mounts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
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
        const float FlySpeed = 20f;

        float RetractSpeed = 20f;      // pixels per tick while a claw reels back in before its arm is removed
        int RetractTimeoutTicks = 120; // a retracting arm is removed after this long even if it never gets home
        float BreathAmount = 0.02f;    // how far each arm segment lengthens and shortens while latched, as a fraction of its length
        float BreathSpeed = 2f;        // radians per second
        float breathBlend;             // 0..1, eases the breathing in once latched and out when the arm lets go (visual only, not synced)

        // Set by MechSuitPlayer through the spawn packet: ai[0], ai[1] = anchor tile, ai[2] = bend side. Read on every machine, never written after spawn.
        public Point AnchorTile => new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
        public float ArmBendSign => Projectile.ai[2] >= 0f ? 1f : -1f;

        float fallbackAngle;
        bool fallbackAngleSet;

        // Each machine works this out itself from the anchor (ai[0..2] now carry the anchor and bend side).
        bool latched;
        public bool Latched => latched && !Retracting;

        // The claw is reeling back in before the arm is removed. Every machine decides it for itself (suit off, owner dead, anchor block gone) and
        // keeps it; the owner also writes it into ai[2] (|ai[2]| = 2, the sign is still the bend side) with netUpdate, so a cutoff drop, which only
        // the owner decides, reaches the others. A retracting arm no longer counts as latched or as one of the owner's active arms.
        bool retractingLocal;
        public bool Retracting => retractingLocal || Projectile.ai[2] > 1.5f || Projectile.ai[2] < -1.5f;

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

            // Runs on every machine. IsActive is the owner's real flag on the owner's client and the mirrored flag (MechSuitNet) everywhere else.
            // A player who left the game takes the arm with them at once; every other reason to remove the arm reels the claw back in first.
            if (!player.active)
            {
                Projectile.Kill();
                return;
            }

            // The suit is off, the owner died, or the block this arm holds (or flies to) is gone (mined, exploded, replaced). Tile changes reach
            // every machine, so every machine reels in its own copy; the owner's UpdateArms then sees a free slot (a retracting arm no longer
            // counts as active) and launches a new arm at the nearest valid tile.
            if (!Retracting && (player.dead || !mechPlayer.IsActive || !AnchorTileIsSolid()))
                BeginRetract();

            if (!fallbackAngleSet)
            {
                fallbackAngle = (AnchorWorldPos - player.Center).ToRotation();
                fallbackAngleSet = true;
            }

            breathBlend = MathHelper.Clamp(breathBlend + (Latched ? 0.05f : -0.1f), 0f, 1f);

            if (Retracting)
            {
                UpdateRetract(player);
                return;
            }

            if (!Latched)
            {
                Vector2 toTarget = AnchorWorldPos - Projectile.Center;
                float distance = toTarget.Length();

                if (distance <= FlySpeed)
                {
                    Projectile.Center = AnchorWorldPos;
                    latched = true;

                    // Cosmetic and local: every machine works out the latch itself, so every client hears it (positional) and sees the dust,
                    // the server skips both.
                    if (!Main.dedServ)
                    {
                        SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
                        SpawnLatchDust();
                    }
                }
                else
                {
                    Projectile.Center += toTarget / distance * FlySpeed;
                    Projectile.rotation = toTarget.ToRotation();
                }
            }
        }

        // Same rule the anchor search uses (MechSuitTileSearch): a tile that exists and is solid.
        bool AnchorTileIsSolid()
        {
            Point anchor = AnchorTile;
            if (!WorldGen.InWorld(anchor.X, anchor.Y))
                return false;

            Tile tile = Main.tile[anchor.X, anchor.Y];
            return tile.HasTile && Main.tileSolid[tile.TileType];
        }

        // Start reeling the claw back to the player; the arm removes itself once it is home. Safe to call on any machine, more than once.
        // The owner also publishes it (ai[2] = bend side * 2 + netUpdate) so the other machines reel in even when only the owner knows why.
        public void BeginRetract()
        {
            if (Retracting)
                return;

            retractingLocal = true;

            if (FactorrariaNet.IsOwner(Main.player[Projectile.owner]))
            {
                Projectile.ai[2] = ArmBendSign * 2f;
                Projectile.netUpdate = true;
            }
        }

        void UpdateRetract(Player player)
        {
            if (Projectile.timeLeft > RetractTimeoutTicks)
                Projectile.timeLeft = RetractTimeoutTicks;

            Vector2 toPlayer = player.Center - Projectile.Center;
            float distance = toPlayer.Length();

            if (distance <= RetractSpeed)
            {
                Projectile.Kill(); // the claw is home
                return;
            }

            Projectile.Center += toPlayer / distance * RetractSpeed;
            Projectile.rotation = (-toPlayer).ToRotation();
        }

        // The dust the block would throw if it were mined: vanilla's own dust amount and dust maker for that tile (modded tiles included).
        void SpawnLatchDust()
        {
            Point anchor = AnchorTile;
            if (!WorldGen.InWorld(anchor.X, anchor.Y))
                return;

            Tile tile = Main.tile[anchor.X, anchor.Y];
            if (!tile.HasTile)
                return;

            int amount = WorldGen.KillTile_GetTileDustAmount(false, tile, anchor.X, anchor.Y);
            for (int i = 0; i < amount; i++)
                WorldGen.KillTile_MakeTileDust(anchor.X, anchor.Y, tile);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];

            Vector2 shoulder = player.Center;
            Vector2 head = Projectile.Center;

            // A slow breathing while the arm holds on: each segment lengthens and shortens a little, out of step with its other segment and with the
            // other arms (the phase comes from the anchor tile, which every machine knows). Visual only: the suit's movement is driven by
            // BoundedSuitVelocity, not by this solve.
            float upperLength = UpperArmLength;
            float foreLength = ForearmLength;
            if (breathBlend > 0f)
            {
                float time = Main.GlobalTimeWrappedHourly * BreathSpeed;
                float phase = AnchorTile.X * 0.7f + AnchorTile.Y * 1.3f;
                upperLength *= 1f + BreathAmount * breathBlend * System.MathF.Sin(time + phase);
                foreLength *= 1f + BreathAmount * breathBlend * System.MathF.Sin(time + phase + 2.1f);
            }

            // The beams never stretch: when the player is farther from the claw than the arm can reach, both segments lengthen just enough to reach it.
            float reach = upperLength + foreLength;
            float distance = Vector2.Distance(shoulder, head);
            if (distance > reach - 1f)
            {
                float grow = (distance + 1f) / reach;
                upperLength *= grow;
                foreLength *= grow;
            }

            Vector2 joint = MechanicalArmIK.SolveElbow(shoulder, head, upperLength, foreLength, ArmBendSign, fallbackAngle);

            Texture2D beam = ArmSegmentTexture.Value;
            Texture2D headTexture = TextureAssets.Projectile[Type].Value;
            Color drawColor = Lighting.GetColor((int)(head.X / 16f), (int)(head.Y / 16f));

            MechanicalArmIK.DrawSegment(Main.spriteBatch, beam, shoulder, joint, drawColor, 0.6f);
            MechanicalArmIK.DrawSegment(Main.spriteBatch, beam, joint, head, drawColor, 0.6f);

            Texture2D jointTexture = JointTexture.Value;
            Vector2 jointOrigin = jointTexture.Size() / 2f;
            Main.spriteBatch.Draw(jointTexture, joint - Main.screenPosition, null, drawColor, 0f, jointOrigin, 1f, SpriteEffects.None, 0f);

            // The claw points along the forearm: from the elbow joint to the point of contact (also while it is still flying out).
            Vector2 forearm = head - joint;
            float headRotation = forearm.LengthSquared() > 0.01f ? forearm.ToRotation() : Projectile.rotation;

            Vector2 headOrigin = headTexture.Size() / 2f;
            Main.spriteBatch.Draw(headTexture, head - Main.screenPosition, null, drawColor, headRotation, headOrigin, 1f, SpriteEffects.None, 0f);

            return false;
        }
    }
}