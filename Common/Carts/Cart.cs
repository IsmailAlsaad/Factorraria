using Factorraria.Common.Liquids;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Carts
{
    /// <summary>What is installed in a cart. Values are saved as ints: append only, never reorder.</summary>
    public enum CartModule
    {
        Empty = 0,
        Chest = 1,
        Terrarium = 2,
        Motor = 3
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
        public const int BumpCooldown = 10;        // ticks during which a second bounce does not re-fire the Bumped event
    }

    /// <summary>
    /// One cart. Pure data: it is not an NPC, projectile or mount. While on a track it follows the rail surface
    /// with two wheel probes (kinematic, no gravity). When it leaves the track it becomes a falling box that
    /// collides with tiles, and it re-attaches if it lands on a rail.
    /// </summary>
    public class Cart
    {
        /// <summary>Save key of the minecart item this cart imitates (see CartSkins). Pickup returns exactly this item.</summary>
        public string Skin
        {
            get { return skin; }
            set
            {
                skin = string.IsNullOrEmpty(value) ? CartSkins.DefaultKey : value;
                skinType = 0;
            }
        }

        private string skin = CartSkins.DefaultKey;
        private int skinType;

        /// <summary>Item type of the skin. Falls back to the vanilla Minecart if the saved skin no longer exists.</summary>
        public int SkinType
        {
            get
            {
                if (skinType <= 0 && !CartSkins.TryTypeOf(skin, out skinType))
                {
                    skinType = ItemID.Minecart;
                }

                return skinType;
            }
        }

        public CartModule Module = CartModule.Empty;

        public const int ChestSlots = 20;

        /// <summary>Chest module contents. Null unless Module == Chest.</summary>
        public Item[] ChestItems;

        /// <summary>Terrarium module tank. Null unless Module == Terrarium. LiquidType is a LiquidTypeRegistry id.</summary>
        public LiquidStack Tank;

        /// <summary>Motor module fuel slot. Null unless Module == Motor.</summary>
        public Item Fuel;

        /// <summary>Installs a module and creates its (empty) payload. Throws away any previous payload.</summary>
        public void SetModule(CartModule module)
        {
            Module = module;
            ChestItems = null;
            Tank = null;
            Fuel = null;

            switch (module)
            {
                case CartModule.Chest:
                    ChestItems = new Item[ChestSlots];
                    for (int i = 0; i < ChestSlots; i++)
                    {
                        ChestItems[i] = new Item();
                    }
                    break;

                case CartModule.Terrarium:
                    Tank = new LiquidStack();
                    break;

                case CartModule.Motor:
                    Fuel = new Item();
                    break;
            }
        }

        /// <summary>The item that installs a module (what pickup hands back). -1 if the module has no item yet.</summary>
        public static int ModuleItemType(CartModule module)
        {
            switch (module)
            {
                case CartModule.Chest:
                    return ItemID.Chest;
                default:
                    return -1; // Terrarium / Motor items arrive with their phases
            }
        }

        /// <summary>Everything pickup should give back: the skin item, the module item, then the payload contents.</summary>
        public List<Item> CollectPickupItems()
        {
            List<Item> result = new List<Item>();
            result.Add(new Item(SkinType));

            int moduleItem = ModuleItemType(Module);
            if (moduleItem > 0)
            {
                result.Add(new Item(moduleItem));
            }

            if (ChestItems != null)
            {
                for (int i = 0; i < ChestItems.Length; i++)
                {
                    if (ChestItems[i] != null && !ChestItems[i].IsAir)
                    {
                        result.Add(ChestItems[i].Clone());
                    }
                }
            }

            if (Fuel != null && !Fuel.IsAir)
            {
                result.Add(Fuel.Clone());
            }

            return result;
        }

        /// <summary>Point on the rail midway between the wheels (the cart's "feet").</summary>
        public Vector2 Position;

        /// <summary>Signed speed along X while on the track.</summary>
        public float Speed;

        /// <summary>Velocity while derailed.</summary>
        public Vector2 Velocity;

        public bool OnTrack = true;
        public float Rotation;
        public bool Active = true;

        /// <summary>+1 = facing right, -1 = facing left. Follows the sign of Speed and is kept while stopped.</summary>
        public int Facing = 1;

        /// <summary>
        /// Fired once per bounce off a bumper (not once per movement sub-step). Static so listeners need no per-cart wiring.
        /// Later phases hang the motor-cart flip, cargo spill and chain trail on this.
        /// </summary>
        public static event Action<Cart> Bumped;

        private int bumpCooldown;

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

            if (bumpCooldown > 0)
            {
                bumpCooldown--;
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

            if (Speed > 0.01f)
            {
                Facing = 1;
            }
            else if (Speed < -0.01f)
            {
                Facing = -1;
            }
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
                    if (Speed != 0f)
                    {
                        Facing = Speed > 0f ? 1 : -1;
                    }

                    RaiseBumped();
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

        private void RaiseBumped()
        {
            if (bumpCooldown > 0)
            {
                return;
            }

            bumpCooldown = CartPhysics.BumpCooldown;

            if (Bumped != null)
            {
                Bumped(this);
            }
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

        /// <summary>Bump when the save layout changes. A missing "version" key is version 0 (pre data model).</summary>
        public const int SaveVersion = 1;

        public TagCompound Save()
        {
            TagCompound tag = new TagCompound();
            tag["version"] = SaveVersion;
            tag["skin"] = Skin;
            tag["module"] = (int)Module;
            tag["facing"] = Facing;
            tag["x"] = Position.X;
            tag["y"] = Position.Y;
            tag["speed"] = Speed;
            tag["vx"] = Velocity.X;
            tag["vy"] = Velocity.Y;
            tag["onTrack"] = OnTrack;

            if (ChestItems != null)
            {
                List<TagCompound> items = new List<TagCompound>();
                for (int i = 0; i < ChestItems.Length; i++)
                {
                    items.Add(ItemIO.Save(ChestItems[i]));
                }

                tag["chest"] = items;
            }

            if (Tank != null)
            {
                tag["tankType"] = Tank.LiquidType; // LiquidTypeRegistry id (append-only), same as machine tanks
                tag["tankAmount"] = Tank.Amount;
            }

            if (Fuel != null)
            {
                tag["fuel"] = ItemIO.Save(Fuel);
            }

            return tag;
        }

        public static Cart Load(TagCompound tag)
        {
            Cart cart = new Cart();

            // version 0 = saves from before the data model (only kind/x/y/speed/vx/vy/onTrack). Those carts become
            // plain Terraria/Minecart carts with no module, which is exactly what they were.
            int version = tag.GetInt("version");

            if (version >= 1)
            {
                cart.Skin = tag.GetString("skin");
                cart.SetModule((CartModule)tag.GetInt("module"));
                cart.Facing = tag.GetInt("facing") < 0 ? -1 : 1;
            }

            cart.Position = new Vector2(tag.GetFloat("x"), tag.GetFloat("y"));
            cart.Speed = tag.GetFloat("speed");
            cart.Velocity = new Vector2(tag.GetFloat("vx"), tag.GetFloat("vy"));
            cart.OnTrack = tag.GetBool("onTrack");

            if (cart.ChestItems != null && tag.ContainsKey("chest"))
            {
                IList<TagCompound> items = tag.GetList<TagCompound>("chest");
                for (int i = 0; i < cart.ChestItems.Length && i < items.Count; i++)
                {
                    cart.ChestItems[i] = ItemIO.Load(items[i]);
                }
            }

            if (cart.Tank != null && tag.ContainsKey("tankType"))
            {
                cart.Tank.LiquidType = tag.GetInt("tankType");
                cart.Tank.Amount = tag.GetFloat("tankAmount");
            }

            if (cart.Fuel != null && tag.ContainsKey("fuel"))
            {
                cart.Fuel = ItemIO.Load(tag.GetCompound("fuel"));
            }

            return cart;
        }

        #endregion
    }
}