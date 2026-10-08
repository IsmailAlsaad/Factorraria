using Factorraria.Common.Liquids;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
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

        public const float CollisionRestitution = 1f;   // 1 = perfectly elastic (equal masses swap speeds), 0 = carts stick together
        public const float CollisionMinSpeed = 0.1f;    // closing speed below this is ignored (no jitter or sound spam from resting contact)

        public const float AnimStopSpeed = 0.05f;        // below this |Speed| the wheels stop turning (frame is frozen)
        public const float AnimPixelsPerFrame = 16f;     // distance travelled per wheel animation frame (wheel circumference / frames)
        public const float AnimMaxFramesPerTick = 0.5f;  // cap so the wheels do not strobe at top speed (0.5 = a frame every 2 ticks)
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

        /// <summary>The exact item that installed the module (e.g. a Gold Chest), so pickup returns that item. 0 or less = use the default.</summary>
        public int ModuleItem;

        /// <summary>True for any placeable chest item (all vanilla chest varieties, and modded ones that use the chest tiles).</summary>
        public static bool IsChestItem(Item item)
        {
            return item != null && !item.IsAir && (item.createTile == TileID.Containers || item.createTile == TileID.Containers2);
        }

        private static int terrariumItemType = -1;

        /// <summary>The vanilla Terrarium item, looked up by name (ids shift between loads). 0 if the game has no item with that name.</summary>
        public static int TerrariumItemType
        {
            get
            {
                if (terrariumItemType < 0)
                {
                    int id;
                    terrariumItemType = ItemID.Search.TryGetId("Terrarium", out id) ? id : 0;
                }

                return terrariumItemType;
            }
        }

        public static bool IsTerrariumItem(Item item)
        {
            return item != null && !item.IsAir && TerrariumItemType > 0 && item.type == TerrariumItemType;
        }

        public const int ChestSlots = 6; // 3 columns x 2 rows

        /// <summary>
        /// Moves as much of item as fits into the chest module: tops up matching stacks first, then (unless onlyIfAlreadyStored)
        /// uses an empty slot. Reduces item.stack by what moved. Returns true if anything moved.
        /// Shared by quick stack, shift-click and (Phase 6) conveyor pickup.
        /// </summary>
        public bool TryAddToChest(Item item, bool onlyIfAlreadyStored = false)
        {
            if (ChestItems == null || item == null || item.IsAir)
            {
                return false;
            }

            int before = item.stack;
            bool alreadyStored = false;

            for (int i = 0; i < ChestItems.Length; i++)
            {
                Item slot = ChestItems[i];
                if (slot == null || slot.IsAir || slot.type != item.type || slot.prefix != item.prefix || !ItemLoader.CanStack(slot, item))
                {
                    continue;
                }

                alreadyStored = true;

                int room = slot.maxStack - slot.stack;
                if (room <= 0)
                {
                    continue;
                }

                int move = Math.Min(room, item.stack);
                slot.stack += move;
                item.stack -= move;

                if (item.stack <= 0)
                {
                    item.TurnToAir();
                    return true;
                }
            }

            if (!onlyIfAlreadyStored || alreadyStored)
            {
                for (int i = 0; i < ChestItems.Length; i++)
                {
                    if (ChestItems[i] == null || ChestItems[i].IsAir)
                    {
                        ChestItems[i] = item.Clone();
                        item.TurnToAir();
                        return true;
                    }
                }
            }

            return item.stack != before;
        }

        /// <summary>Chest module contents. Null unless Module == Chest.</summary>
        public Item[] ChestItems;

        /// <summary>Items from older saves that do not fit the smaller chest. Handed back when the cart is picked up. Null if none.</summary>
        public List<Item> OverflowItems;

        /// <summary>Terrarium module tank. Null unless Module == Terrarium. LiquidType is a LiquidTypeRegistry id.</summary>
        public LiquidStack Tank;

        /// <summary>Motor module fuel slot. Null unless Module == Motor.</summary>
        public Item Fuel;

        /// <summary>Installs a module and creates its (empty) payload. Throws away any previous payload.</summary>
        public void SetModule(CartModule module, int itemType = -1)
        {
            Module = module;
            ModuleItem = itemType;
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
                case CartModule.Terrarium:
                    return TerrariumItemType > 0 ? TerrariumItemType : -1;
                default:
                    return -1; // the Motor item arrives with its phase
            }
        }

        /// <summary>Everything pickup should give back: the skin item, the module item, then the payload contents.</summary>
        public List<Item> CollectPickupItems()
        {
            List<Item> result = new List<Item>();
            result.Add(new Item(SkinType));

            int moduleItem = ModuleItem > 0 ? ModuleItem : ModuleItemType(Module);
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

            if (OverflowItems != null)
            {
                for (int i = 0; i < OverflowItems.Count; i++)
                {
                    if (OverflowItems[i] != null && !OverflowItems[i].IsAir)
                    {
                        result.Add(OverflowItems[i].Clone());
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

        /// <summary>Current frame of the skin's mount sheet. Driven by speed, see UpdateAnimation. Not saved.</summary>
        public int AnimFrame;

        private float animCounter;

        /// <summary>
        /// Fired once per bounce off a bumper (not once per movement sub-step). Static so listeners need no per-cart wiring.
        /// Later phases hang the motor-cart flip, cargo spill and chain trail on this.
        /// </summary>
        public static event Action<Cart> Bumped;

        private int bumpCooldown;

        /// <summary>
        /// Fired once per cart-vs-cart collision, AFTER the elastic exchange has been applied.
        /// Arguments are (left cart, right cart) by X position. Static like Bumped; CartSystem.OnCartCollide is the default listener.
        /// </summary>
        public static event Action<Cart, Cart> Collided;

        /// <summary>Signed X speed whether on the track (Speed) or derailed (Velocity.X). Setting it also updates Facing.</summary>
        public float AlongSpeed
        {
            get { return OnTrack ? Speed : Velocity.X; }
            set
            {
                if (OnTrack)
                {
                    Speed = MathHelper.Clamp(value, -CartPhysics.MaxSpeed, CartPhysics.MaxSpeed);
                }
                else
                {
                    Velocity.X = value;
                }

                if (value > 0.01f)
                {
                    Facing = 1;
                }
                else if (value < -0.01f)
                {
                    Facing = -1;
                }
            }
        }

        /// <summary>
        /// If the two carts overlap and are closing on each other, exchanges their X speeds (equal masses, restitution from
        /// CartPhysics.CollisionRestitution; 1 = perfectly elastic) and fires Collided. Resting or separating overlaps are left alone.
        /// </summary>
        /// <returns>true if a collision happened.</returns>
        public static bool TryCollide(Cart a, Cart b)
        {
            if (a == b || !a.Active || !b.Active || !a.Hitbox.Intersects(b.Hitbox))
            {
                return false;
            }

            Cart left = a.Position.X <= b.Position.X ? a : b;
            Cart right = left == a ? b : a;

            float vLeft = left.AlongSpeed;
            float vRight = right.AlongSpeed;

            if (vLeft - vRight < CartPhysics.CollisionMinSpeed)
            {
                return false; // not closing in on each other
            }

            float e = CartPhysics.CollisionRestitution;
            left.AlongSpeed = ((1f + e) * vRight + (1f - e) * vLeft) * 0.5f;
            right.AlongSpeed = ((1f + e) * vLeft + (1f - e) * vRight) * 0.5f;

            if (Collided != null)
            {
                Collided(left, right);
            }

            return true;
        }

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

            UpdateAnimation();

            // Fell out of the world
            if (Position.X < 32f || Position.X > (Main.maxTilesX - 2) * 16f || Position.Y > (Main.maxTilesY - 3) * 16f)
            {
                Active = false;
            }
        }

        /// <summary>
        /// Wheel animation: the frame advances with the distance travelled (so faster = quicker wheels), at most
        /// AnimMaxFramesPerTick, and is frozen while stopped. Derailed carts show the skin's in-air frame.
        /// Frame ranges come from the skin's mount (CartSkinTable.GetAnimation).
        /// </summary>
        private void UpdateAnimation()
        {
            CartSkinTable.SkinAnimation anim = CartSkinTable.GetAnimation(SkinType);

            if (!OnTrack)
            {
                AnimFrame = anim.AirFrame;
                animCounter = 0f;
                return;
            }

            bool inRunRange = AnimFrame >= anim.RunStart && AnimFrame < anim.RunStart + anim.RunCount;
            float speed = Math.Abs(Speed);

            if (speed < CartPhysics.AnimStopSpeed)
            {
                if (!inRunRange)
                {
                    AnimFrame = anim.StandFrame;
                }

                return;
            }

            if (!inRunRange)
            {
                AnimFrame = anim.RunStart;
            }

            if (anim.RunCount <= 1)
            {
                return;
            }

            animCounter += Math.Min(speed / CartPhysics.AnimPixelsPerFrame, CartPhysics.AnimMaxFramesPerTick);

            if (animCounter >= 1f)
            {
                int steps = (int)animCounter;
                animCounter -= steps;
                AnimFrame = anim.RunStart + (AnimFrame - anim.RunStart + steps) % anim.RunCount;
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
            float half = CartPhysics.WheelHalfBase * 0.6f;
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
                bool leftOk = TrySampleSmooth(newX - half, Position.Y - half * slope, out left);
                bool rightOk = TrySampleSmooth(newX + half, Position.Y + half * slope, out right);

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

        private static readonly float[] SmoothOffsets = { -0.75f, -0.25f, 0.25f, 0.75f };

        /// <summary>
        /// Vanilla rail height is a staircase of 2 px slices, so a wheel's height jumps every 2 px of travel and the cart
        /// shakes on slopes. Averaging the surface over one slice width (+-1 px) turns the staircase into a smooth ramp.
        /// Samples that differ by more than 4 px (a cliff, the end of a rail) are ignored so edges stay sharp.
        /// </summary>
        private static bool TrySampleSmooth(float x, float referenceY, out TrackSample sample)
        {
            bool ok = TrackData.TrySample(x, referenceY, out sample);
            if (!ok || !sample.HasSurface)
            {
                return ok;
            }

            float sum = sample.SurfaceY;
            int count = 1;

            for (int i = 0; i < SmoothOffsets.Length; i++)
            {
                TrackSample extra;
                if (TrackData.TrySample(x + SmoothOffsets[i], referenceY, out extra) && extra.HasSurface && Math.Abs(extra.SurfaceY - sample.SurfaceY) <= 4f)
                {
                    sum += extra.SurfaceY;
                    count++;
                }
            }

            sample.SurfaceY = sum / count;
            return true;
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

            if (ModuleItem > 0)
            {
                tag["moduleItem"] = CartSkins.KeyOf(ModuleItem);
            }
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

            if (OverflowItems != null && OverflowItems.Count > 0)
            {
                List<TagCompound> overflow = new List<TagCompound>();
                for (int i = 0; i < OverflowItems.Count; i++)
                {
                    overflow.Add(ItemIO.Save(OverflowItems[i]));
                }

                tag["overflow"] = overflow;
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

                int moduleItem;
                if (tag.ContainsKey("moduleItem") && CartSkins.TryTypeOf(tag.GetString("moduleItem"), out moduleItem))
                {
                    cart.ModuleItem = moduleItem;
                }
                cart.Facing = tag.GetInt("facing") < 0 ? -1 : 1;
            }

            cart.Position = new Vector2(tag.GetFloat("x"), tag.GetFloat("y"));
            cart.Speed = tag.GetFloat("speed");
            cart.Velocity = new Vector2(tag.GetFloat("vx"), tag.GetFloat("vy"));
            cart.OnTrack = tag.GetBool("onTrack");

            if (cart.ChestItems != null && tag.ContainsKey("chest"))
            {
                IList<TagCompound> items = tag.GetList<TagCompound>("chest");
                for (int i = 0; i < items.Count; i++)
                {
                    Item loaded = ItemIO.Load(items[i]);

                    if (i < cart.ChestItems.Length)
                    {
                        cart.ChestItems[i] = loaded;
                    }
                    else if (!loaded.IsAir)
                    {
                        // saved when the chest was bigger: keep the extra items so nothing is lost
                        if (cart.OverflowItems == null)
                        {
                            cart.OverflowItems = new List<Item>();
                        }

                        cart.OverflowItems.Add(loaded);
                    }
                }
            }

            if (tag.ContainsKey("overflow"))
            {
                IList<TagCompound> extra = tag.GetList<TagCompound>("overflow");
                if (cart.OverflowItems == null)
                {
                    cart.OverflowItems = new List<Item>();
                }

                for (int i = 0; i < extra.Count; i++)
                {
                    Item loaded = ItemIO.Load(extra[i]);
                    if (!loaded.IsAir)
                    {
                        cart.OverflowItems.Add(loaded);
                    }
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