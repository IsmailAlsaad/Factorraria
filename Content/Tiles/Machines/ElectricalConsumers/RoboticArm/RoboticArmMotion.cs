using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // The deterministic motion of one Robotic Arm's hand (patch 2.7, finding A1). It owns everything the worker's timing depends on:
    // the target, which side the arm is on, the critically damped hand spring (so "in position" happens after the same number of ticks
    // as it always did), the strike timer and the impact point. It has no textures and reads nothing that only exists on a client, so it
    // runs the same in single-player and on a dedicated server. The entity owns one and ticks it once per logic tick (right after the worker),
    // and RoboticArmVisual only reads it to draw. Never saved.
    public class RoboticArmMotion
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public float TrackSmoothTime = 0.06f;     // seconds for the hand to settle onto a moving target (critically damped spring)
        public float BendSmoothTime = 0.15f;      // seconds for the ELBOW to swing from one side to the other (only the elbow eases when the side flips)
        public float IdleOffsetX = 16f;           // idle hand position relative to the shoulder (X is mirrored by side)
        public float IdleOffsetY = -20f;          // also the height of the point the hand sweeps over when switching side
        public float SideDeadZone = 6f;           // target must be this far to one side before the arm switches side
        public float ThrustDistance = 6f;         // extra reach of a block thrust
        public float ImpactPoint = 0.6f;          // fraction of the strike at which the tool/block touches the target (the world changes here)
        public float HoldBackMax = 70f;           // longest distance the claw hub stops short of the target (grip + tool length)
        public float GripDistance = 12f;          // claw hub to the spot between its jaws where a tool grip / block centre sits
        public float InPositionDistance = 3f;

        bool initialized;
        Vector2 head;
        int side = 1;                 // 1 = target/elbow on the right, -1 = left
        float bend = -1f;             // eased bend sign handed to the IK (-side when settled)
        Vector2? target;              // world position of the target tile centre
        int strikeTimer;
        int strikeDuration;
        bool inPosition;
        Vector2 headVelocity;         // spring velocities (see DampStep)
        float bendVelocity;

        public bool Initialized => initialized;
        public int Side => side;
        public Vector2 Head => head;
        public float Bend => bend;
        public Vector2? Target => target;
        public bool HasTarget => target.HasValue;
        public bool Striking => strikeTimer > 0;
        public int StrikeDuration => strikeDuration;   // patch 7b.4: the server sends it so a client's swing is as long as the real strike
        public bool InPosition => inPosition;
        public float StrikeProgress => Striking ? 1f - strikeTimer / (float)strikeDuration : 0f;

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
                initialized = true;
            }

            if (strikeTimer > 0)
                strikeTimer--;

            // Side switching: the side (and with it the elbow's bend direction) changes the moment the target crosses to the other
            // side of the shoulder. Only the elbow eases to its new position (the bend value); the hand never leaves the target.
            if (target.HasValue)
            {
                float dx = target.Value.X - shoulder.X;
                if (MathF.Abs(dx) > SideDeadZone)
                    side = dx > 0f ? 1 : -1;
            }

            Vector2 goal = shoulder + new Vector2(side * IdleOffsetX, IdleOffsetY);
            bool aiming = target.HasValue;

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
            }

            // The hand moves with a critically damped spring (smooth start and stop).
            head.X = DampStep(head.X, goal.X, ref headVelocity.X, TrackSmoothTime);
            head.Y = DampStep(head.Y, goal.Y, ref headVelocity.Y, TrackSmoothTime);
            bend = DampStep(bend, -side, ref bendVelocity, BendSmoothTime);

            inPosition = aiming && Vector2.Distance(head, goal) < InPositionDistance;
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

        // Length of the held item's sprite diagonal. A dedicated server has no item textures, so it falls back to the item's hitbox size
        // (close to the sprite size for tools). Single-player and clients keep using the sprite, so their timing is unchanged.
        float ItemLength(Item item)
        {
            if (Main.dedServ)
                return MathF.Sqrt(item.width * item.width + item.height * item.height);

            Main.instance.LoadItem(item.type);
            Texture2D texture = TextureAssets.Item[item.type].Value;
            Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? texture.Frame();
            return MathF.Sqrt(frame.Width * frame.Width + frame.Height * frame.Height);
        }
    }
}