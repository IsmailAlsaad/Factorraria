using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// A motor cart and the carriages hooked behind it (Phase 8). The motor records a TRAIL while it drives: one sample
    /// (position, tilt, travel direction, bump flag) every CartPhysics.ChainSampleStep pixels of rail travelled. The trail is
    /// indexed by distance driven (odometer), not by time, so a carriage always sits ChainSpacing * slot pixels behind the
    /// motor along the rail and the gaps stay constant when the motor slows, stops or reverses. Bumps are stored in the
    /// trail too, so a carriage only reacts to a bumper when it reaches it (a chest carriage spills there, not earlier).
    /// Carriages never simulate physics: CartSystem asks the chain to place them every tick after the motor has moved.
    /// </summary>
    public class CartChain
    {
        private struct TrailSample
        {
            public Vector2 Position;
            public float Rotation;
            public int Direction; // sign of X travel while the motor passed this sample (+1 / -1), never 0
            public bool Bump;     // the motor bounced off a bumper right before this sample
        }

        /// <summary>
        /// Fired when a free cart hooks onto a chain, with (motor, new carriage), AFTER it has been placed in its slot.
        /// Static like Cart.Bumped; CartSystem.OnCartCoupled is the default listener (a placeholder sound).
        /// </summary>
        public static event Action<Cart, Cart> Coupled;

        /// <summary>The cart that drives the chain and records the trail. Always a motor cart.</summary>
        public Cart Motor { get; private set; }

        /// <summary>Carriages in order: [0] is directly behind the motor.</summary>
        public readonly List<Cart> Carriages = new List<Cart>();

        /// <summary>Id written to the save so members can find each other again. Assigned by CartSystem.SaveWorldData.</summary>
        public int SaveId;

        /// <summary>Motor plus carriages. This is the chain's mass when it collides with an unlinked cart.</summary>
        public int Count
        {
            get { return 1 + Carriages.Count; }
        }

        private const float Step = CartPhysics.ChainSampleStep;

        private readonly TrailSample[] samples; // ring buffer, sample k lives at samples[k % length] and sits k * Step px along the trail
        private int newestIndex;                // absolute index of the newest sample
        private float odometer;                 // distance driven so far; always >= newestIndex * Step and < (newestIndex + 1) * Step
        private Vector2 lastPosition;           // where the motor was at the odometer
        private float lastRotation;
        private int lastDirection;
        private bool pendingBump;

        #region Building and tearing down

        public CartChain(Cart motor)
        {
            Motor = motor;

            // Enough samples to reach the last carriage slot with a margin.
            int count = (int)Math.Ceiling((CartPhysics.MaxCarriages + 1) * CartPhysics.ChainSpacing / Step) + 1;
            samples = new TrailSample[count + 24];

            Backfill(count);
            motor.Chain = this;
        }

        /// <summary>
        /// A brand new chain has no history, so the trail starts as the rail BEHIND the motor (opposite to where it is heading,
        /// read straight from the track). If the rail ends early the remaining samples stack on the last valid point.
        /// </summary>
        private void Backfill(int count)
        {
            float heading = Math.Abs(Motor.Speed) > 0.2f ? Math.Sign(Motor.Speed) : Motor.Facing;
            int behind = heading >= 0f ? -1 : 1; // X direction of "behind"
            int travel = -behind;                // a carriage passes these samples moving this way

            Vector2 pose = Motor.Position;
            float poseRotation = Motor.Rotation;
            bool onRail = true;

            for (int j = 0; j < count; j++)
            {
                if (j > 0 && onRail)
                {
                    float nextX = pose.X + behind * Step * (float)Math.Cos(poseRotation);
                    float surface;
                    float rotation;

                    if (Cart.TryRailPose(nextX, pose.Y, (float)Math.Tan(poseRotation), out surface, out rotation))
                    {
                        pose = new Vector2(nextX, surface);
                        poseRotation = rotation;
                    }
                    else
                    {
                        onRail = false;
                    }
                }

                TrailSample sample = new TrailSample();
                sample.Position = pose;
                sample.Rotation = poseRotation;
                sample.Direction = travel;
                sample.Bump = false;
                samples[count - 1 - j] = sample; // sample count-1 is the motor's own pose (the newest)
            }

            newestIndex = count - 1;
            odometer = newestIndex * Step;
            lastPosition = Motor.Position;
            lastRotation = Motor.Rotation;
            lastDirection = travel;
        }

        /// <summary>Hooks a cart onto the end of the chain and snaps it into its slot behind the last member.</summary>
        public void Attach(Cart cart, bool raiseEvent)
        {
            Carriages.Add(cart);
            cart.Chain = this;
            cart.ChainSlack = 0f;
            cart.OnTrack = true;
            cart.Velocity = Vector2.Zero;
            cart.Speed = 0f;

            Vector2 position;
            float rotation;
            int direction;
            int index;
            Read(odometer - Carriages.Count * CartPhysics.ChainSpacing, out position, out rotation, out direction, out index);

            cart.Position = position;
            cart.Rotation = rotation;
            if (direction != 0)
            {
                cart.Facing = direction;
            }

            cart.ReplayScanIndex = index; // bumps that happened before it joined are not replayed

            if (raiseEvent && Coupled != null)
            {
                Coupled(Motor, cart);
            }
        }

        /// <summary>
        /// Takes a cart out of the chain (picked up, destroyed). Carriages behind it close the gap smoothly (ChainSlack).
        /// Removing the motor, or the last carriage, ends the chain.
        /// </summary>
        public void Remove(Cart cart)
        {
            if (cart == Motor)
            {
                Dissolve();
                return;
            }

            int index = Carriages.IndexOf(cart);
            if (index < 0)
            {
                return;
            }

            Carriages.RemoveAt(index);
            cart.Chain = null;
            cart.ChainSlack = 0f;

            for (int i = index; i < Carriages.Count; i++)
            {
                Carriages[i].ChainSlack += CartPhysics.ChainSpacing; // they keep their place now and glide forward into the gap
            }

            if (Carriages.Count == 0)
            {
                Dissolve();
            }
        }

        /// <summary>Breaks the chain up: every carriage becomes a free cart where it stands, keeping its current speed.</summary>
        public void Dissolve()
        {
            for (int i = 0; i < Carriages.Count; i++)
            {
                Carriages[i].Chain = null;
                Carriages[i].ChainSlack = 0f;
            }

            Carriages.Clear();

            if (Motor != null && Motor.Chain == this)
            {
                Motor.Chain = null;
            }
        }

        #endregion

        #region Trail

        private TrailSample At(int index)
        {
            return samples[index % samples.Length];
        }

        private int OldestIndex
        {
            get { return Math.Max(0, newestIndex - samples.Length + 1); }
        }

        /// <summary>
        /// Called by the motor after every movement sub-step (at most 2 px, same as CartPhysics.MaxStep). Pushes a new sample
        /// each time the odometer passes a multiple of Step, placed by interpolating along the sub-step.
        /// </summary>
        public void Record(Vector2 position, float rotation)
        {
            float segment = Vector2.Distance(lastPosition, position);
            if (segment < 0.0001f)
            {
                lastRotation = rotation;
                return;
            }

            int direction = Math.Abs(position.X - lastPosition.X) > 0.0001f ? Math.Sign(position.X - lastPosition.X) : lastDirection;
            float start = odometer;
            odometer += segment;

            while ((newestIndex + 1) * Step <= odometer)
            {
                float t = ((newestIndex + 1) * Step - start) / segment;

                newestIndex++;
                TrailSample sample = new TrailSample();
                sample.Position = Vector2.Lerp(lastPosition, position, t);
                sample.Rotation = MathHelper.Lerp(lastRotation, rotation, t);
                sample.Direction = direction;
                sample.Bump = pendingBump;
                pendingBump = false;
                samples[newestIndex % samples.Length] = sample;
            }

            lastPosition = position;
            lastRotation = rotation;
            lastDirection = direction;
        }

        /// <summary>The motor just bounced off a bumper: the next sample carries the flag, carriages replay it when they pass.</summary>
        public void MarkBump()
        {
            pendingBump = true;
        }

        /// <summary>Pose on the trail at a distance along it (clamped to what is stored). index is the sample just behind that point.</summary>
        private void Read(float distance, out Vector2 position, out float rotation, out int direction, out int index)
        {
            float oldest = OldestIndex * Step;
            if (distance < oldest)
            {
                distance = oldest;
            }

            if (distance > odometer)
            {
                distance = odometer;
            }

            float headStart = newestIndex * Step;

            if (distance >= headStart)
            {
                // between the newest stored sample and the motor itself
                TrailSample newest = At(newestIndex);
                float span = odometer - headStart;
                float t = span > 0.0001f ? (distance - headStart) / span : 1f;

                position = Vector2.Lerp(newest.Position, lastPosition, t);
                rotation = MathHelper.Lerp(newest.Rotation, lastRotation, t);
                direction = lastDirection;
                index = newestIndex;
                return;
            }

            int k = (int)Math.Floor(distance / Step);
            float f = distance / Step - k;
            TrailSample a = At(k);
            TrailSample b = At(k + 1);

            position = Vector2.Lerp(a.Position, b.Position, f);
            rotation = MathHelper.Lerp(a.Rotation, b.Rotation, f);
            direction = b.Direction; // the way the motor was moving when it travelled from a to b
            index = k;
        }

        /// <summary>
        /// Places every carriage on the trail (slot i sits (i + 1) * ChainSpacing behind the motor), gives it the speed it moved
        /// at this tick (so its wheels turn) and replays any bump it just passed. Call after the motor has updated.
        /// </summary>
        public void UpdateCarriages()
        {
            for (int i = 0; i < Carriages.Count; i++)
            {
                Cart cart = Carriages[i];

                cart.ChainSlack = Math.Max(0f, cart.ChainSlack - CartPhysics.ChainCloseGapSpeed);

                Vector2 position;
                float rotation;
                int direction;
                int index;
                Read(odometer - (i + 1) * CartPhysics.ChainSpacing - cart.ChainSlack, out position, out rotation, out direction, out index);

                float previousX = cart.Position.X;
                cart.Position = position;
                cart.Rotation = rotation;
                cart.OnTrack = true;
                cart.Velocity = Vector2.Zero;
                cart.Speed = MathHelper.Clamp(position.X - previousX, -CartPhysics.MaxSpeed, CartPhysics.MaxSpeed);

                if (direction != 0)
                {
                    cart.Facing = direction;
                }

                if (index > cart.ReplayScanIndex)
                {
                    int from = Math.Max(cart.ReplayScanIndex + 1, OldestIndex);
                    int to = Math.Min(index, newestIndex);
                    bool bumped = false;

                    for (int k = from; k <= to; k++)
                    {
                        if (At(k).Bump)
                        {
                            bumped = true;
                        }
                    }

                    cart.ReplayScanIndex = index;

                    if (bumped)
                    {
                        cart.ReplayBump();
                    }
                }

                cart.TickCarriage();
            }
        }

        #endregion

        #region Auto-attach and loading

        /// <summary>A cart that may become a carriage: free, on the track, and not a motor itself.</summary>
        private static bool CanJoin(Cart cart)
        {
            return cart.Active && cart.Chain == null && cart.OnTrack && cart.Module != CartModule.Motor && !Cart.IsMotorSkin(cart.SkinType);
        }

        private static bool TouchesChain(Cart motor, Cart candidate)
        {
            Rectangle box = candidate.Hitbox;

            if (motor.Hitbox.Intersects(box))
            {
                return true;
            }

            if (motor.Chain != null)
            {
                for (int i = 0; i < motor.Chain.Carriages.Count; i++)
                {
                    if (motor.Chain.Carriages[i].Hitbox.Intersects(box))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// A free cart that touches a motor or any of its carriages hooks onto the end of that chain, up to MaxCarriages (the closing
        /// speed must be at most CouplingMaxRelativeSpeed, which by default allows every touch). Carts that cannot join (another
        /// motor, a full chain, a derailed cart) are left to the normal collision. One cart per motor per tick.
        /// </summary>
        public static void AutoAttach(List<Cart> carts)
        {
            for (int i = 0; i < carts.Count; i++)
            {
                Cart motor = carts[i];

                if (!motor.Active || motor.Module != CartModule.Motor || !motor.OnTrack || motor.IsCarriage)
                {
                    continue;
                }

                if (motor.Chain != null && motor.Chain.Carriages.Count >= CartPhysics.MaxCarriages)
                {
                    continue;
                }

                for (int j = 0; j < carts.Count; j++)
                {
                    Cart candidate = carts[j];

                    if (!CanJoin(candidate) || Math.Abs(motor.AlongSpeed - candidate.AlongSpeed) > CartPhysics.CouplingMaxRelativeSpeed)
                    {
                        continue;
                    }

                    if (!TouchesChain(motor, candidate))
                    {
                        continue;
                    }

                    CartChain chain = motor.Chain != null ? motor.Chain : new CartChain(motor);
                    chain.Attach(candidate, true);
                    break;
                }
            }
        }

        /// <summary>
        /// After loading a world: groups the carts by the chain id they were saved with and builds each chain again (motor first,
        /// carriages in saved order). The trail is rebuilt from the rail behind the motor, so carriages snap onto it.
        /// Carts whose chain cannot be rebuilt (missing motor, not on track) simply load as free carts.
        /// </summary>
        public static void RebuildFromSave(List<Cart> carts)
        {
            Dictionary<int, List<Cart>> groups = new Dictionary<int, List<Cart>>();

            for (int i = 0; i < carts.Count; i++)
            {
                Cart cart = carts[i];
                if (cart.LoadChainId <= 0)
                {
                    continue;
                }

                List<Cart> members;
                if (!groups.TryGetValue(cart.LoadChainId, out members))
                {
                    members = new List<Cart>();
                    groups[cart.LoadChainId] = members;
                }

                members.Add(cart);
            }

            foreach (KeyValuePair<int, List<Cart>> group in groups)
            {
                List<Cart> members = group.Value;
                members.Sort((p, q) => p.LoadChainSlot.CompareTo(q.LoadChainSlot));

                Cart motor = null;
                for (int i = 0; i < members.Count && motor == null; i++)
                {
                    if (members[i].LoadChainSlot == 0 && members[i].Active && members[i].OnTrack && members[i].Module == CartModule.Motor)
                    {
                        motor = members[i];
                    }
                }

                if (motor == null)
                {
                    continue;
                }

                CartChain chain = null;

                for (int i = 0; i < members.Count; i++)
                {
                    Cart member = members[i];

                    if (member == motor || !CanJoin(member) || (chain != null && chain.Carriages.Count >= CartPhysics.MaxCarriages))
                    {
                        continue;
                    }

                    if (chain == null)
                    {
                        chain = new CartChain(motor);
                    }

                    chain.Attach(member, false);
                }
            }

            for (int i = 0; i < carts.Count; i++)
            {
                carts[i].LoadChainId = 0;
                carts[i].LoadChainSlot = 0;
            }
        }

        #endregion
    }
}