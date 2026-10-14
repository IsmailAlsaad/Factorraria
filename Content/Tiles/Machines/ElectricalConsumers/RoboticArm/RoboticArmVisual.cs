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
    // Client-only animation state of one Robotic Arm: where the hand is, which side the elbow bends to, and the strike animation.
    // It is driven by whoever owns the arm's logic (SetTarget / StartStrike) and only ever reads that state, so the real worker
    // (Phase 4) can replace the Phase 2 debug controller without touching this class. Never saved, never synced.
    public class RoboticArmVisual
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public float TrackSmoothTime = 0.06f;     // seconds for the hand to settle onto a moving target (critically damped spring)
        public float FlipSmoothTime = 0.28f;      // the same while the arm sweeps over to the other side (slower = gentler)
        public float SmoothingEase = 0.05f;       // how fast the spring time eases from FlipSmoothTime back to TrackSmoothTime
        public float BendSmoothTime = 0.15f;      // seconds for the elbow to swing from one side to the other
        public float AimSmoothing = 0.25f;        // how fast the claw turns toward its aim
        public float IdleOffsetX = 8f;            // idle hand position relative to the shoulder (X is mirrored by side)
        public float IdleOffsetY = -14f;          // also the height of the point the hand sweeps over when switching side
        public float IdleAimTilt = 1.6f;          // idle claw angle away from straight up
        public float SideDeadZone = 6f;           // target must be this far to one side before the arm switches side
        public float SideFlipDistance = 10f;      // the hand must be this close to the point above the shoulder before the side may flip
        public float SwingArc = 1.1f;             // radians the claw swings the tool back before it strikes
        public float ThrustDistance = 6f;         // extra reach of a block thrust
        public float ImpactPoint = 0.6f;          // fraction of the strike at which the tool/block touches the target (Phase 4 changes the world here)
        public float HoldBackMax = 70f;           // longest distance the claw hub stops short of the target (grip + tool length)
        public float GripDistance = 12f;          // claw hub to the spot between its jaws where a tool grip / block centre sits
        public float ClawPivotX = 9f;             // the claw's hub centre inside MechanicalArmProjectile.png, in sprite pixels
        public float ClawPivotY = 16f;
        public float BlockScale = 0.8f;           // a held block is drawn a bit smaller than its item sprite
        public float BeamThickness = 0.56f;       // beam sprite scale across the arm (the shared hook arm uses 0.7)
        public float JointScale = 0.8f;
        public float InPositionDistance = 3f;

        Asset<Texture2D> beamTexture;
        Asset<Texture2D> jointTexture;
        Asset<Texture2D> clawTexture;

        bool initialized;
        Vector2 head;
        int side = 1;                 // 1 = target/elbow on the right, -1 = left
        float bend = -1f;             // eased bend sign handed to the IK (-side when settled)
        float aimAngle;
        Vector2? target;              // world position of the target tile centre
        int strikeTimer;
        int strikeDuration;
        bool inPosition;
        Vector2 headVelocity;         // spring velocities (see DampStep)
        float bendVelocity;
        float smoothTime;             // current hand spring time, eased between track and flip
        Vector2 elbow;                // solved once per tick in Update, shared by both draw passes

        public int Side => side;
        public Vector2 Head => head;
        public bool HasTarget => target.HasValue;
        public bool Striking => strikeTimer > 0;
        public bool InPosition => inPosition;
        public float StrikeProgress => Striking ? 1f - strikeTimer / (float)strikeDuration : 0f;

        public RoboticArmVisual()
        {
            beamTexture = ModContent.Request<Texture2D>("Factorraria/Content/Projectiles/Hooks/MechanicalArmBeam", AssetRequestMode.ImmediateLoad);
            jointTexture = ModContent.Request<Texture2D>("Factorraria/Content/Projectiles/Hooks/MechanicalArmJoint", AssetRequestMode.ImmediateLoad);
            clawTexture = ModContent.Request<Texture2D>("Factorraria/Content/Projectiles/Hooks/MechanicalArmProjectile", AssetRequestMode.ImmediateLoad);
        }

        public void SetTarget(Vector2? worldTarget)
        {
            target = worldTarget;
            if (!target.HasValue)
                strikeTimer = 0;
        }

        public void StartStrike(int ticks)
        {
            strikeDuration = Math.Max(ticks, 2);
            strikeTimer = strikeDuration;
        }

        public void Update(Vector2 shoulder, float upperArm, float forearm, Item held, bool heldIsTool)
        {
            float reach = upperArm + forearm;

            if (!initialized)
            {
                head = shoulder + new Vector2(side * IdleOffsetX, IdleOffsetY);
                bend = -side;
                aimAngle = IdleAim();
                smoothTime = TrackSmoothTime;
                initialized = true;
            }

            if (strikeTimer > 0)
                strikeTimer--;

            // Side switching: the hand sweeps over a point straight above the shoulder, slowly, and the side only changes once it
            // is there. The elbow then eases through the folded pose instead of snapping.
            int desiredSide = side;
            if (target.HasValue)
            {
                float dx = target.Value.X - shoulder.X;
                if (MathF.Abs(dx) > SideDeadZone)
                    desiredSide = dx > 0f ? 1 : -1;
            }

            Vector2 passPoint = shoulder + new Vector2(0f, IdleOffsetY);
            bool flipping = desiredSide != side;
            if (flipping && Vector2.DistanceSquared(head, passPoint) < SideFlipDistance * SideFlipDistance)
            {
                side = desiredSide;
                flipping = false;
            }

            Vector2 idle = shoulder + new Vector2(side * IdleOffsetX, IdleOffsetY);
            Vector2 goal = idle;
            float desiredAim = IdleAim();
            bool aiming = target.HasValue && !flipping;

            if (flipping)
            {
                strikeTimer = 0;
                goal = passPoint;
                desiredAim = -MathHelper.PiOver2;
            }

            if (aiming)
            {
                Vector2 toTarget = target.Value - shoulder;
                float distance = toTarget.Length();
                Vector2 dir = distance > 1f ? toTarget / distance : new Vector2(0f, -1f);

                // The hub stops short of the target by the grip distance (plus the tool length for a tool), so that what the claw
                // holds is what reaches the target tile.
                float hold = GripDistance;
                if (!held.IsAir && heldIsTool)
                    hold = MathHelper.Clamp(GripDistance + ItemLength(held) * 0.85f, 0f, HoldBackMax);

                hold = MathF.Min(hold, distance * 0.5f);
                goal = target.Value - dir * hold;

                // Blocks are thrust forward at the target instead of swung.
                if (!held.IsAir && !heldIsTool && Striking)
                    goal += dir * ThrustCurve(StrikeProgress) * ThrustDistance;

                Vector2 fromShoulder = goal - shoulder;
                float maxDistance = reach * 0.98f;
                if (fromShoulder.Length() > maxDistance)
                    goal = shoulder + Vector2.Normalize(fromShoulder) * maxDistance;

                Vector2 toTargetFromHead = target.Value - head;
                desiredAim = toTargetFromHead.LengthSquared() > 1f ? toTargetFromHead.ToRotation() : dir.ToRotation();
            }

            // The hand moves with a critically damped spring (smooth start and stop). It is slow while switching side and eases
            // back to the quick tracking speed afterwards, so neither the retreat nor the return is a sudden jump.
            float desiredTime = flipping ? FlipSmoothTime : TrackSmoothTime;
            smoothTime = desiredTime > smoothTime ? desiredTime : smoothTime + (desiredTime - smoothTime) * SmoothingEase;

            head.X = DampStep(head.X, goal.X, ref headVelocity.X, smoothTime);
            head.Y = DampStep(head.Y, goal.Y, ref headVelocity.Y, smoothTime);
            bend = DampStep(bend, -side, ref bendVelocity, BendSmoothTime);
            aimAngle += MathHelper.WrapAngle(desiredAim - aimAngle) * AimSmoothing;

            elbow = MechanicalArmIK.SolveElbow(shoulder, head, upperArm, forearm, bend, -MathHelper.PiOver2 + side * 0.5f);

            inPosition = aiming && Vector2.Distance(head, goal) < InPositionDistance;
        }

        // Drawn BEHIND the tiles (see MachineDynamicDrawSystem): the two beams and the elbow joint.
        public void DrawBehind(SpriteBatch spriteBatch, Vector2 shoulder)
        {
            if (!initialized)
                return;

            Texture2D beam = beamTexture.Value;
            MechanicalArmIK.DrawSegment(spriteBatch, beam, shoulder, elbow, LightAt((shoulder + elbow) / 2f), BeamThickness);
            MechanicalArmIK.DrawSegment(spriteBatch, beam, elbow, head, LightAt((elbow + head) / 2f), BeamThickness);

            Texture2D joint = jointTexture.Value;
            spriteBatch.Draw(joint, elbow - Main.screenPosition, null, LightAt(elbow), 0f, joint.Size() / 2f, JointScale, SpriteEffects.None, 0f);
        }

        // Drawn in front of the tiles: the held tool/block, then the claw over it so its jaws sit in front of what it holds.
        // The claw rotates about its hub (the wrist, where the forearm ends); a held tool is attached between its jaws and turns
        // with it, so a swing is the claw swinging the tool.
        public void Draw(SpriteBatch spriteBatch, Item held, bool heldIsTool)
        {
            if (!initialized)
                return;

            bool hasItem = !held.IsAir;
            float angle = aimAngle;
            if (hasItem && heldIsTool)
                angle += side * SwingCurve(StrikeProgress) * SwingArc;

            Vector2 grip = head + angle.ToRotationVector2() * GripDistance;
            Color color = LightAt(head);

            if (hasItem)
            {
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

            // The claw art points right and is lit from above, so it is flipped vertically on the left side to keep it upright.
            // SpriteBatch mirrors only the texture, not the origin, hence the mirrored pivot Y.
            Texture2D claw = clawTexture.Value;
            bool flipped = side < 0;
            Vector2 pivot = new Vector2(ClawPivotX, flipped ? claw.Height - ClawPivotY : ClawPivotY);
            spriteBatch.Draw(claw, head - Main.screenPosition, null, color, angle, pivot, 1f, flipped ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
        }

        // One step of a critically damped spring (same maths as a "smooth damp"): eases in and out and never overshoots.
        float DampStep(float current, float goal, ref float velocity, float time)
        {
            float dt = 1f / 60f;
            float omega = 2f / MathF.Max(time, 0.0001f);
            float x = omega * dt;
            float decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - goal;
            float temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * decay;
            return goal + (change + temp) * decay;
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
            return 0.3f * MathF.Sin(r * MathHelper.Pi);
        }

        // Forward to the target by ImpactPoint, then back.
        float ThrustCurve(float p)
        {
            if (p < ImpactPoint)
            {
                float t = p / ImpactPoint;
                return t * t * (3f - 2f * t);
            }

            float r = (p - ImpactPoint) / (1f - ImpactPoint);
            return 1f - r * r * (3f - 2f * r);
        }

        float IdleAim() => -MathHelper.PiOver2 + side * IdleAimTilt;

        static Color LightAt(Vector2 world) => Lighting.GetColor((int)(world.X / 16f), (int)(world.Y / 16f));

        static float ItemLength(Item item)
        {
            Main.instance.LoadItem(item.type);
            Texture2D texture = TextureAssets.Item[item.type].Value;
            Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? texture.Frame();
            return MathF.Sqrt(frame.Width * frame.Width + frame.Height * frame.Height);
        }
    }
}