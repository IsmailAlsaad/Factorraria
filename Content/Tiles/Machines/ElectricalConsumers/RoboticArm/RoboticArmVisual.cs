using Factorraria.Common.Mounts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // Client-only drawing state of one Robotic Arm: the elbow, the claw angle and the textures. Where the hand is, which side the arm is
    // on and the strike timing come from RoboticArmMotion (owned by the entity and ticked on the logic side), so the worker never waits on
    // this class (patch 2.7). It only reads the motion model and draws it. Never saved, never synced.
    public class RoboticArmVisual
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public float AimSmoothing = 0.25f;        // how fast the claw turns toward its aim
        public float IdleAimTilt = 1.6f;          // idle claw angle away from straight up
        public float SwingArc = 1.1f;             // radians the claw swings the tool back before it strikes
        public float SwingEndAngle = 0.33f;       // radians the claw ends the swing PAST the target line (follow-through), before it returns to rest
        public float ClawPivotX = 9f;             // the claw's hub centre inside MechanicalArmProjectile.png, in sprite pixels
        public float ClawPivotY = 16f;
        public float BlockScale = 0.8f;           // a held block is drawn a bit smaller than its item sprite
        public float BeamThickness = 0.45f;       // beam sprite scale across the arm (the shared hook arm uses 0.7)
        public float JointScale = 1f;

        Asset<Texture2D> beamTexture;
        Asset<Texture2D> jointTexture;
        Asset<Texture2D> clawTexture;

        bool initialized;
        RoboticArmMotion motion;
        float aimAngle;
        float drawAngle;              // claw angle incl. the tool swing, set once per tick in Update so both draw passes agree
        Vector2 elbow;                // solved once per tick in Update, shared by both draw passes

        // Read from the motion model (the hand position, side, grip and impact point are logic state, not drawing state).
        Vector2 head => motion.Head;
        int side => motion.Side;
        float GripDistance => motion.GripDistance;
        float ImpactPoint => motion.ImpactPoint;

        public RoboticArmVisual()
        {
            beamTexture = ModContent.Request<Texture2D>("Factorraria/Content/Projectiles/Hooks/MechanicalArmBeam", AssetRequestMode.ImmediateLoad);
            jointTexture = ModContent.Request<Texture2D>("Factorraria/Content/Projectiles/Hooks/MechanicalArmSmallJoint", AssetRequestMode.ImmediateLoad);
            clawTexture = ModContent.Request<Texture2D>("Factorraria/Content/Projectiles/Hooks/MechanicalArmProjectile", AssetRequestMode.ImmediateLoad);
        }

        // Once per tick. The motion model is ticked by the logic side first (the entity), so this only solves the elbow and the claw angle.
        public void Update(RoboticArmMotion motion, Vector2 shoulder, float upperArm, float forearm, Item held, bool heldIsTool)
        {
            this.motion = motion;
            if (!motion.Initialized)
                return;

            if (!initialized)
            {
                aimAngle = IdleAim();
                initialized = true;
            }

            float desiredAim = IdleAim();
            elbow = MechanicalArmIK.SolveElbow(shoulder, head, upperArm, forearm, motion.Bend, -MathHelper.PiOver2 + side * 0.5f);

            // The claw faces along the line from the ELBOW joint to the target tile.
            if (motion.HasTarget && (motion.Target.Value - elbow).LengthSquared() > 1f)
                desiredAim = (motion.Target.Value - elbow).ToRotation();

            aimAngle += MathHelper.WrapAngle(desiredAim - aimAngle) * AimSmoothing;

            drawAngle = aimAngle;
            if (!held.IsAir && heldIsTool)
                drawAngle += side * SwingCurve(motion.StrikeProgress) * SwingArc;
        }

        // Drawn BEHIND the tiles (see MachineDynamicDrawSystem): the two beams, the elbow joint, the claw and the held item.
        public void DrawBehind(SpriteBatch spriteBatch, Vector2 shoulder, Item held, bool heldIsTool)
        {
            if (!initialized)
                return;

            Texture2D beam = beamTexture.Value;
            MechanicalArmIK.DrawSegment(spriteBatch, beam, shoulder, elbow, LightAt((shoulder + elbow) / 2f), BeamThickness);
            MechanicalArmIK.DrawSegment(spriteBatch, beam, elbow, head, LightAt((elbow + head) / 2f), BeamThickness);

            Texture2D joint = jointTexture.Value;
            spriteBatch.Draw(joint, elbow - Main.screenPosition, null, LightAt(elbow), 0f, joint.Size() / 2f, JointScale, SpriteEffects.None, 0f);

            // The claw art points right and is lit from above, so it is flipped vertically on the left side to keep it upright.
            // SpriteBatch mirrors only the texture, not the origin, hence the mirrored pivot Y. It rotates about its hub (the wrist).
            Texture2D claw = clawTexture.Value;
            bool flipped = side < 0;
            Vector2 pivot = new Vector2(ClawPivotX, flipped ? claw.Height - ClawPivotY : ClawPivotY);
            spriteBatch.Draw(claw, head - Main.screenPosition, null, LightAt(head), drawAngle, pivot, 1f, flipped ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);

            DrawHeld(spriteBatch, held, heldIsTool);
        }

        // The held tool/block, drawn right after the claw so it sits over the claw's jaws (everything is behind the tiles).
        // A held tool is attached between the jaws and turns with the claw, so a swing is the claw swinging the tool.
        void DrawHeld(SpriteBatch spriteBatch, Item held, bool heldIsTool)
        {
            if (!initialized || held.IsAir)
                return;

            float angle = drawAngle;
            Vector2 grip = head + angle.ToRotationVector2() * GripDistance;
            Color color = LightAt(head);

            Main.instance.LoadItem(held.type);
            Texture2D texture = TextureAssets.Item[held.type].Value;
            Rectangle frame = Main.itemAnimations[held.type]?.GetFrame(texture) ?? texture.Frame();

            if (heldIsTool)
            {
                // Tool sprites point up-right (up-left when mirrored) with the grip at the bottom corner: the grip goes between
                // the jaws and the rotation turns that diagonal onto the claw's direction.
                bool mirrored = side < 0;
                Vector2 origin = new Vector2(mirrored ? frame.Width : 0f, frame.Height);
                float rotation = angle + (mirrored ? 3f * MathHelper.PiOver4 : MathHelper.PiOver4);
                spriteBatch.Draw(texture, grip - Main.screenPosition, frame, color, rotation, origin, 1f, mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
            else
            {
                // A block is just held between the jaws: no rotation.
                spriteBatch.Draw(texture, grip - Main.screenPosition, frame, color, 0f, frame.Size() / 2f, BlockScale, SpriteEffects.None, 0f);
            }
        }

        // Wind-up (0 -> -1), fast sweep onto the target (-1 -> 0 at ImpactPoint), small follow-through and return.
        float SwingCurve(float p)
        {
            float windUpEnd = ImpactPoint * 0.65f;
            if (p < windUpEnd)
                return -MathF.Sin(p / windUpEnd * MathHelper.PiOver2);

            if (p < ImpactPoint)
            {
                float t = (p - windUpEnd) / (ImpactPoint - windUpEnd);
                return MathHelper.Lerp(-1f, 0f, t * t * (3f - 2f * t));
            }

            float r = (p - ImpactPoint) / (1f - ImpactPoint);
            return SwingEndAngle / MathF.Max(SwingArc, 0.0001f) * MathF.Sin(r * MathHelper.Pi); // the caller multiplies by SwingArc, so the peak is SwingEndAngle radians
        }

        float IdleAim() => -MathHelper.PiOver2 + side * IdleAimTilt;

        static Color LightAt(Vector2 world) => Lighting.GetColor((int)(world.X / 16f), (int)(world.Y / 16f));
    }
}