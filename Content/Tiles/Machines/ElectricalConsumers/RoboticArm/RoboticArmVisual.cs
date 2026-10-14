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
        public float HeadSmoothing = 0.35f;       // fraction of the remaining distance the hand covers per tick
        public float BendSmoothing = 0.2f;        // how fast the elbow eases from one side to the other
        public float AimSmoothing = 0.25f;        // how fast the held item turns toward its aim
        public float IdleOffsetX = 8f;            // idle hand position relative to the shoulder (X is mirrored by side)
        public float IdleOffsetY = -14f;
        public float IdleAimTilt = 0.6f;          // idle item angle away from straight up
        public float SideDeadZone = 6f;           // target must be this far to one side before the arm switches side
        public float SideFlipDistance = 10f;      // the hand must be this close to the idle spot before the side may flip
        public float SwingArc = 1.1f;             // radians the tool swings back before it strikes
        public float ThrustDistance = 6f;         // extra reach of a block thrust
        public float ImpactPoint = 0.6f;          // fraction of the strike at which the tool/block touches the target (Phase 4 changes the world here)
        public float HoldBackMax = 44f;           // longest distance the hand stops short of the target (tool length)
        public float BlockCarryDistance = 8f;     // a carried block sits this far in front of the hand
        public float BeamThickness = 0.45f;       // beam sprite scale across the arm (the shared hook arm uses 0.7)
        public float JointScale = 0.8f;
        public float InPositionDistance = 3f;

        Asset<Texture2D> beamTexture;
        Asset<Texture2D> jointTexture;

        bool initialized;
        Vector2 head;
        int side = 1;                 // 1 = target/elbow on the right, -1 = left
        float bend = -1f;             // eased bend sign handed to the IK (-side when settled)
        float aimAngle;
        Vector2? target;              // world position of the target tile centre
        int strikeTimer;
        int strikeDuration;
        bool inPosition;

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
                initialized = true;
            }

            if (strikeTimer > 0)
                strikeTimer--;

            // Side switching: the arm only changes side once the hand is back at the idle spot, and the elbow then eases through
            // the folded pose instead of snapping.
            int desiredSide = side;
            if (target.HasValue)
            {
                float dx = target.Value.X - shoulder.X;
                if (MathF.Abs(dx) > SideDeadZone)
                    desiredSide = dx > 0f ? 1 : -1;
            }

            Vector2 idle = shoulder + new Vector2(side * IdleOffsetX, IdleOffsetY);
            bool flipping = desiredSide != side;
            if (flipping && Vector2.DistanceSquared(head, idle) < SideFlipDistance * SideFlipDistance)
            {
                side = desiredSide;
                flipping = false;
                idle = shoulder + new Vector2(side * IdleOffsetX, IdleOffsetY);
            }

            Vector2 goal = idle;
            float desiredAim = IdleAim();
            bool aiming = target.HasValue && !flipping;

            if (flipping)
                strikeTimer = 0;

            if (aiming)
            {
                Vector2 toTarget = target.Value - shoulder;
                float distance = toTarget.Length();
                Vector2 dir = distance > 1f ? toTarget / distance : new Vector2(0f, -1f);

                float hold;
                if (held.IsAir)
                    hold = 0f;
                else if (heldIsTool)
                    hold = MathHelper.Clamp(ItemLength(held) * 0.85f, 0f, HoldBackMax);
                else
                    hold = BlockCarryDistance;

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

            head += (goal - head) * HeadSmoothing;
            bend += (-side - bend) * BendSmoothing;
            aimAngle += MathHelper.WrapAngle(desiredAim - aimAngle) * AimSmoothing;

            inPosition = aiming && Vector2.Distance(head, goal) < InPositionDistance;
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 shoulder, float upperArm, float forearm, Item held, bool heldIsTool)
        {
            if (!initialized)
                return;

            Vector2 elbow = MechanicalArmIK.SolveElbow(shoulder, head, upperArm, forearm, bend, -MathHelper.PiOver2 + side * 0.5f);

            Texture2D beam = beamTexture.Value;
            MechanicalArmIK.DrawSegment(spriteBatch, beam, shoulder, elbow, LightAt((shoulder + elbow) / 2f), BeamThickness);
            MechanicalArmIK.DrawSegment(spriteBatch, beam, elbow, head, LightAt((elbow + head) / 2f), BeamThickness);

            Texture2D joint = jointTexture.Value;
            spriteBatch.Draw(joint, elbow - Main.screenPosition, null, LightAt(elbow), 0f, joint.Size() / 2f, JointScale, SpriteEffects.None, 0f);

            if (held.IsAir)
                return;

            Main.instance.LoadItem(held.type);
            Texture2D texture = TextureAssets.Item[held.type].Value;
            Rectangle frame = Main.itemAnimations[held.type]?.GetFrame(texture) ?? texture.Frame();
            Color color = LightAt(head);

            if (heldIsTool)
            {
                // Tool sprites point up-right (up-left when mirrored) with the grip at the bottom corner, so the grip goes on the hand
                // and the rotation turns that diagonal onto the aim. The swing offset is mirrored with the side.
                float angle = aimAngle + side * SwingCurve(StrikeProgress) * SwingArc;
                bool mirrored = side < 0;
                Vector2 origin = new Vector2(mirrored ? frame.Width : 0f, frame.Height);
                float rotation = angle + (mirrored ? 3f * MathHelper.PiOver4 : MathHelper.PiOver4);
                spriteBatch.Draw(texture, head - Main.screenPosition, frame, color, rotation, origin, 1f, mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
            else
            {
                Vector2 carried = head + aimAngle.ToRotationVector2() * BlockCarryDistance;
                spriteBatch.Draw(texture, carried - Main.screenPosition, frame, color, 0f, frame.Size() / 2f, 0.9f, SpriteEffects.None, 0f);
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