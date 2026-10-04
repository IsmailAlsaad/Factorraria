using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Carts
{
    /// <summary>What a cart is carrying/doing. Only plain carts exist for now; the rest comes in later parts.</summary>
    public enum CartKind
    {
        Empty = 0
    }

    /// <summary>All tuning numbers in one place. Distances are pixels, speeds are pixels per tick.</summary>
    public static class CartPhysics
    {
        public const float WheelHalfBase = 19f;   // each wheel sits this far from the cart's centre
        public const int HitboxWidth = (int)(32 * 1.6f);
        public const int HitboxHeight = (int)(22 * 1.6f);

        public const float MaxSpeed = 12f;
        public const float Drag = 0.02f;          // slowdown per tick while on plain track
        public const float BoostAccel = 0.6f;     // speed gained per tick while a wheel is on a booster
        public const float ShoveSpeed = 5f;       // debug "push" from right click
        public const float MaxStep = 2f;          // largest move per sub-step; matches the 2 px rail slices

        public const float Gravity = 0.2f;
        public const float MaxFallSpeed = 10f;
        public const float GroundFriction = 0.92f; // X velocity multiplier per tick while sliding on the ground
        public const int RerailDelay = 15;         // ticks after derailing before the track can catch the cart again
    }

    /// <summary>
    /// One cart. Pure data: it is not an NPC, projectile or mount. While on a track it follows the rail surface
    /// with two wheel probes (kinematic, no gravity). When it leaves the track it becomes a falling box that
    /// collides with tiles, and it re-attaches if it lands on a rail.
    /// </summary>
    public class Cart
    {
        public CartKind Kind;

        /// <summary>Point on the rail midway between the wheels (the cart's "feet").</summary>
        public Vector2 Position;

        /// <summary>Signed speed along X while on the track.</summary>
        public float Speed;

        /// <summary>Velocity while derailed.</summary>
        public Vector2 Velocity;

        public bool OnTrack = true;
        public float Rotation;
        public bool Active = true;

        private int rerailCooldown;

        public Rectangle Hitbox
        {
            get
            {
                return new Rectangle(
                    (int)(Position.X - CartPhysics.HitboxWidth / 2f),
                    (int)(Position.Y - CartPhysics.HitboxHeight),
                    CartPhysics.HitboxWidth,
                    CartPhysics.HitboxHeight);
            }
        }

        public void Update()
        {
            if (!Active)
            {
                return;
            }

            if (OnTrack)
            {
                UpdateOnTrack();
            }
            else
            {
                UpdateDerailed();
            }

            // Fell out of the world
            if (Position.X < 32f || Position.X > (Main.maxTilesX - 2) * 16f || Position.Y > (Main.maxTilesY - 3) * 16f)
            {
                Active = false;
            }
        }

        /// <summary>Gives the cart a push along X. Works on and off the track.</summary>
        public void Shove(float deltaSpeed)
        {
            if (OnTrack)
            {
                Speed = MathHelper.Clamp(Speed + deltaSpeed, -CartPhysics.MaxSpeed, CartPhysics.MaxSpeed);
            }
            else
            {
                Velocity.X += deltaSpeed;
            }
        }

        #region On track

        private void UpdateOnTrack()
        {
            float half = CartPhysics.WheelHalfBase;
            float slope = (float)Math.Tan(Rotation);
            int boost = 0;

            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(Speed) / CartPhysics.MaxStep));
            float step = Speed / steps;
            bool moving = step != 0f;
            bool forward = step >= 0f;

            for (int i = 0; i < steps; i++)
            {
                float newX = Position.X + step;

                TrackSample left;
                TrackSample right;
                bool leftOk = TrackData.TrySample(newX - half, Position.Y - half * slope, out left);
                bool rightOk = TrackData.TrySample(newX + half, Position.Y + half * slope, out right);

                // The leading wheel decides what the end of the track does to us.
                if (moving)
                {
                    bool leadOk = forward ? rightOk : leftOk;
                    TrackSample lead = forward ? right : left;

                    if (leadOk && !lead.HasSurface && lead.End != TrackEnd.None)
                    {
                        if (HitEnd(lead.End))
                        {
                            return; // derailed
                        }

                        break; // stopped or bounced: no more movement this tick
                    }
                }

                bool leftSurface = leftOk && left.HasSurface;
                bool rightSurface = rightOk && right.HasSurface;

                if (!leftSurface && !rightSurface)
                {
                    Derail(new Vector2(Speed, 0f));
                    return;
                }

                // A wheel hanging over nothing copies the other wheel's height.
                float yLeft = leftSurface ? left.SurfaceY : right.SurfaceY;
                float yRight = rightSurface ? right.SurfaceY : left.SurfaceY;

                Position = new Vector2(newX, (yLeft + yRight) * 0.5f);
                Rotation = (float)Math.Atan2(yRight - yLeft, 2f * half);
                slope = (yRight - yLeft) / (2f * half);

                boost = (leftSurface && left.BoostDirection != 0) ? left.BoostDirection : (rightSurface ? right.BoostDirection : 0);
            }

            if (boost != 0)
            {
                Speed += boost * CartPhysics.BoostAccel;
            }
            else if (Speed != 0f)
            {
                Speed = Math.Abs(Speed) <= CartPhysics.Drag ? 0f : Speed - Math.Sign(Speed) * CartPhysics.Drag;
            }

            Speed = MathHelper.Clamp(Speed, -CartPhysics.MaxSpeed, CartPhysics.MaxSpeed);
        }

        /// <returns>true if the cart came off the track.</returns>
        private bool HitEnd(TrackEnd end)
        {
            switch (end)
            {
                case TrackEnd.Stop:
                    Speed = 0f;
                    return false;

                case TrackEnd.Bounce:
                    Speed = -Speed;
                    return false;

                case TrackEnd.Ramp:
                    Derail(new Vector2(Speed, -Math.Abs(Speed)));
                    return true;

                case TrackEnd.Open:
                    Derail(new Vector2(Speed, 0f));
                    return true;
            }

            return false;
        }

        private void Derail(Vector2 velocity)
        {
            OnTrack = false;
            Velocity = velocity;
            Speed = 0f;
            rerailCooldown = CartPhysics.RerailDelay;
        }

        #endregion

        #region Derailed

        private void UpdateDerailed()
        {
            if (rerailCooldown > 0)
            {
                rerailCooldown--;
            }

            Velocity.Y = Math.Min(Velocity.Y + CartPhysics.Gravity, CartPhysics.MaxFallSpeed);

            Vector2 topLeft = new Vector2(Position.X - CartPhysics.HitboxWidth / 2f, Position.Y - CartPhysics.HitboxHeight);
            Vector2 move = Collision.TileCollision(topLeft, Velocity, CartPhysics.HitboxWidth, CartPhysics.HitboxHeight);

            bool landed = Velocity.Y > 0f && move.Y < Velocity.Y;
            bool hitCeiling = Velocity.Y < 0f && move.Y > Velocity.Y;
            bool hitWall = move.X != Velocity.X;

            Position += move;

            if (hitWall)
            {
                Velocity.X = -Velocity.X * 0.3f;
            }

            if (hitCeiling)
            {
                Velocity.Y = 0f;
            }

            if (landed)
            {
                Velocity.Y = 0f;
                Velocity.X *= CartPhysics.GroundFriction;

                if (Math.Abs(Velocity.X) < 0.05f)
                {
                    Velocity.X = 0f;
                }
            }

            Rotation *= 0.92f;

            TryRerail();
        }

        /// <summary>If our feet just crossed a rail surface while falling, snap onto it.</summary>
        private void TryRerail()
        {
            if (rerailCooldown > 0 || Velocity.Y < 0f)
            {
                return;
            }

            TrackSample sample;
            if (!TrackData.TrySample(Position.X, Position.Y, out sample) || !sample.HasSurface)
            {
                return;
            }

            float below = Position.Y - sample.SurfaceY; // > 0 means our feet are under the rail surface
            if (below < -1f || below > Velocity.Y + 4f)
            {
                return;
            }

            OnTrack = true;
            Speed = MathHelper.Clamp(Velocity.X, -CartPhysics.MaxSpeed, CartPhysics.MaxSpeed);
            Velocity = Vector2.Zero;
            Position = new Vector2(Position.X, sample.SurfaceY);
            Rotation = 0f;
        }

        #endregion

        #region Save / load

        public TagCompound Save()
        {
            TagCompound tag = new TagCompound();
            tag["kind"] = (int)Kind;
            tag["x"] = Position.X;
            tag["y"] = Position.Y;
            tag["speed"] = Speed;
            tag["vx"] = Velocity.X;
            tag["vy"] = Velocity.Y;
            tag["onTrack"] = OnTrack;
            return tag;
        }

        public static Cart Load(TagCompound tag)
        {
            Cart cart = new Cart();
            cart.Kind = (CartKind)tag.GetInt("kind");
            cart.Position = new Vector2(tag.GetFloat("x"), tag.GetFloat("y"));
            cart.Speed = tag.GetFloat("speed");
            cart.Velocity = new Vector2(tag.GetFloat("vx"), tag.GetFloat("vy"));
            cart.OnTrack = tag.GetBool("onTrack");
            return cart;
        }

        #endregion
    }
}