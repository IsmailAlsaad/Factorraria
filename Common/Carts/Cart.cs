using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Content.Tiles.Machines.GelBurner;
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

        public const float PickupPadding = 8f;           // chest carts also grab conveyor items this many px outside the hitbox
        public const int SpillGrabCooldown = 120;        // ticks a spilled item cannot be grabbed by a cart again
        public const int TerrariumDrainPerTick = 16;      // world liquid units pulled into the tank per tick (a full tile is 255)

        public const int MaxCarriages = 5;                  // carriages a motor cart can pull
        public const float ChainSpacing = HitboxWidth + 14f; // rail distance from one cart's centre to the next (the 14 px gap shows the chain)
        public const float ChainSampleStep = 2f;            // the motor records its trail every this many px of rail driven (matches MaxStep)
        public const float ChainCloseGapSpeed = 3f;         // px per tick a carriage glides forward to close the gap left by a picked-up carriage
        public const float CouplingMaxRelativeSpeed = 24f;  // a free cart touching a motor / carriage hooks on if the two are closing slower than this. 24 = any touch couples (top speed is 12 each way); lower it to make hard hits collide instead

        public const float MotorSpeedCap = 6f;           // top speed a motor cart reaches under its own power (boosters and collisions can still exceed it)
        public const float MotorAccel = 0.08f;           // speed a burning motor cart gains per tick towards its facing (0.12 = about 80 ticks to MaxSpeed)
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

        public static bool IsTerrariumItem(Item item)
        {
            return item != null && !item.IsAir && item.type == ItemID.Terrarium;
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
            bool alreadyStored = HasFilterFor(item.type);

            for (int i = 0; i < ChestItems.Length; i++)
            {
                Item slot = ChestItems[i];
                if (slot == null || slot.IsAir || slot.type != item.type || slot.prefix != item.prefix || !ItemLoader.CanStack(slot, item) || !SlotAccepts(i, item))
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
                    if ((ChestItems[i] == null || ChestItems[i].IsAir) && SlotAccepts(i, item))
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

        /// <summary>
        /// Per-slot filter of a chest cart (same indices as ChestItems). 0 = unfiltered, otherwise the item type that slot is
        /// reserved for. A filtered slot only takes its own item and a bump always leaves 1 of it behind. Null unless Module == Chest.
        /// </summary>
        public int[] SlotFilter;

        public bool IsSlotFiltered(int slot)
        {
            return SlotFilter != null && slot >= 0 && slot < SlotFilter.Length && SlotFilter[slot] != 0;
        }

        /// <summary>True if any slot is filtered for this item type.</summary>
        public bool HasFilterFor(int itemType)
        {
            if (SlotFilter == null)
            {
                return false;
            }

            for (int i = 0; i < SlotFilter.Length; i++)
            {
                if (SlotFilter[i] == itemType && itemType != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Can this slot take the item? A filtered slot takes only its own item type. An unfiltered slot takes anything
        /// except item types that some other slot is filtered for.
        /// </summary>
        public bool SlotAccepts(int slot, Item item)
        {
            if (SlotFilter == null || slot < 0 || slot >= SlotFilter.Length)
            {
                return true;
            }

            if (SlotFilter[slot] != 0)
            {
                return SlotFilter[slot] == item.type;
            }

            return !HasFilterFor(item.type);
        }

        /// <summary>How many of this slot's items a bump must leave in the chest: 1 for a filtered slot holding its item, else 0.</summary>
        public int FilterKeepCount(int slot, Item item)
        {
            return IsSlotFiltered(slot) && item != null && !item.IsAir && SlotFilter[slot] == item.type ? 1 : 0;
        }

        /// <summary>The "Set filter" button: every slot that holds an item is filtered for it, every empty slot becomes unfiltered.</summary>
        public void SetFilterFromSlots()
        {
            if (ChestItems == null || SlotFilter == null)
            {
                return;
            }

            for (int i = 0; i < SlotFilter.Length; i++)
            {
                Item item = ChestItems[i];
                SlotFilter[i] = item == null || item.IsAir ? 0 : item.type;
            }
        }

        /// <summary>Empties the tank's liquid type once (almost) nothing is left, so the terrarium can take a different liquid next.</summary>
        public void NormalizeTank()
        {
            if (Tank != null && Tank.Amount < 1f)
            {
                Tank.Amount = 0f;
                Tank.LiquidType = -1;
            }
        }

        /// <summary>Items from older saves that do not fit the smaller chest. Handed back when the cart is picked up. Null if none.</summary>
        public List<Item> OverflowItems;

        /// <summary>Terrarium module tank. Null unless Module == Terrarium. LiquidType is a LiquidTypeRegistry id.</summary>
        public LiquidStack Tank;

        /// <summary>Most items the motor cart's fuel slot holds.</summary>
        public const int FuelSlotCap = 100;

        // The slot lives in a one-item array so FuelModule (the Gel Burner's burn logic) can take fuel straight out of it.
        private Item[] fuelSlots;
        private FuelModule burner;

        /// <summary>Motor module fuel slot (at most FuelSlotCap items). Null unless Module == Motor.</summary>
        public Item Fuel
        {
            get { return fuelSlots != null ? fuelSlots[0] : null; }
            set { fuelSlots = value == null ? null : new Item[] { value }; }
        }

        /// <summary>True while a fuel item is being burned (the flame gauge is not empty).</summary>
        public bool IsBurning
        {
            get { return burner != null && burner.Remaining > 0; }
        }

        /// <summary>0..1 of the current fuel item left, -1 when nothing is burning. Same meaning as the Gel Burner's flame gauge.</summary>
        public float FuelBurnFraction
        {
            get { return burner != null ? burner.GetBurnFraction(0f) : -1f; }
        }

        /// <summary>Only what the Gel Burner can burn (GelBurnerRecipeRegistry.Fuels) is fuel.</summary>
        public static bool AcceptsFuel(int itemType)
        {
            return GelBurnerRecipeRegistry.Fuels.Contains(itemType);
        }

        public static bool AcceptsFuel(Item item)
        {
            return item != null && !item.IsAir && AcceptsFuel(item.type);
        }

        /// <summary>
        /// Moves as much of item as fits into the fuel slot (same item type as what is there, never past FuelSlotCap).
        /// Reduces item.stack by what moved. Shared by shift-click and fuel pickup. Returns true if anything moved.
        /// </summary>
        public bool TryAddFuel(Item item)
        {
            if (fuelSlots == null || item == null || item.IsAir || !AcceptsFuel(item.type))
            {
                return false;
            }

            Item slot = fuelSlots[0];

            if (slot == null || slot.IsAir)
            {
                int first = Math.Min(item.stack, FuelSlotCap);
                Item placed = item.Clone();
                placed.stack = first;
                fuelSlots[0] = placed;

                item.stack -= first;
                if (item.stack <= 0)
                {
                    item.TurnToAir();
                }

                return true;
            }

            if (slot.type != item.type || slot.prefix != item.prefix || !ItemLoader.CanStack(slot, item))
            {
                return false;
            }

            int room = FuelSlotCap - slot.stack;
            if (room <= 0)
            {
                return false;
            }

            int move = Math.Min(room, item.stack);
            slot.stack += move;
            item.stack -= move;

            if (item.stack <= 0)
            {
                item.TurnToAir();
            }

            return true;
        }

        /// <summary>Burns one tick of fuel (taking a new item from the slot when the last one ran out). True if there was fuel to burn.</summary>
        private bool BurnFuelForDrive()
        {
            if (burner == null || fuelSlots == null || !burner.TryEnsureFuel(fuelSlots))
            {
                return false;
            }

            burner.Consume();
            return true;
        }

        /// <summary>Puts back a half-burnt fuel item after loading a save.</summary>
        public void RestoreBurn(int remaining, int capacity)
        {
            if (burner != null)
            {
                burner.Restore(remaining, capacity);
            }
        }

        /// <summary>True for the skin that is always a motor cart (the vanilla Steampunk Minecart).</summary>
        public static bool IsMotorSkin(int skinItemType)
        {
            return skinItemType > 0 && skinItemType == ItemID.SteampunkMinecart;
        }

        /// <summary>Installs a module and creates its (empty) payload. Throws away any previous payload.</summary>
        public void SetModule(CartModule module, int itemType = -1)
        {
            Module = module;
            ModuleItem = itemType;
            ChestItems = null;
            SlotFilter = null;
            Tank = null;
            Fuel = null;
            burner = null;

            switch (module)
            {
                case CartModule.Chest:
                    ChestItems = new Item[ChestSlots];
                    SlotFilter = new int[ChestSlots];
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
                    burner = new FuelModule(GelBurnerRecipeRegistry.Fuels, 0, 1);
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
                    return ItemID.Terrarium > 0 ? ItemID.Terrarium : -1;
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

        /// <summary>The chain this cart belongs to (as the motor or as a carriage). Null for a free cart.</summary>
        public CartChain Chain;

        /// <summary>True for a cart hooked behind a motor. Carriages are placed by their chain and run no physics of their own.</summary>
        public bool IsCarriage
        {
            get { return Chain != null && Chain.Motor != this; }
        }

        /// <summary>The cart that really moves: the chain's motor for a chained cart, the cart itself otherwise.</summary>
        public Cart Body
        {
            get { return Chain != null ? Chain.Motor : this; }
        }

        /// <summary>1 for a free cart, the number of linked carts for a chained one (used by collisions).</summary>
        public int CollisionMass
        {
            get { return Chain != null ? Chain.Count : 1; }
        }

        /// <summary>Extra distance behind its slot a carriage is lagging, in px. Grows when a carriage in front is removed and shrinks to 0.</summary>
        public float ChainSlack;

        /// <summary>Highest trail sample whose bump this carriage has already replayed.</summary>
        public int ReplayScanIndex;

        // Filled by Load, consumed once by CartChain.RebuildFromSave.
        public int LoadChainId;
        public int LoadChainSlot;

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

        // The pressure plate track tile this cart is standing on (-1 = none). A plate fires once when the cart rolls onto it.
        private Point lastPlate = new Point(-1, -1);

        /// <summary>Triggers vanilla pressure plate tracks (the wire kind) when this cart rolls onto one, like a vanilla minecart does.</summary>
        private void CheckPressurePlate()
        {
            Point plate;
            if (!TrackData.TryGetPressurePlate(Position.X, Position.Y, out plate))
            {
                lastPlate = new Point(-1, -1);
                return;
            }

            if (plate == lastPlate)
            {
                return;
            }

            lastPlate = plate;

            // Same as vanilla: a client asks the server to hit the switch, single player / server trips the wires directly.
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.SendData(MessageID.HitSwitch, -1, -1, null, plate.X, plate.Y);
            }
            else
            {
                Wiring.HitSwitch(plate.X, plate.Y);
            }
        }

        /// <summary>
        /// Fired once per cart-vs-cart collision, AFTER the elastic exchange has been applied.
        /// Arguments are (left cart, right cart) by X position. Static like Bumped; CartSystem.OnCartCollide is the default listener.
        /// </summary>
        public static event Action<Cart, Cart> Collided;

        /// <summary>
        /// Fired whenever the cart's animation frame changes, with (cart, previous frame, new frame). Static like Bumped.
        /// Hang smoke, sparks or sounds for any skin on this; CartSystem.OnCartFrameChanged is the default listener (motor cart smoke).
        /// </summary>
        public static event Action<Cart, int, int> FrameChanged;

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

                if (Module == CartModule.Motor)
                {
                    return; // a motor cart's facing is its drive direction: only a bump (or placement) changes it
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

            if (a.Chain != null && a.Chain == b.Chain)
            {
                return false; // linked carts never collide with each other
            }

            Cart left = a.Position.X <= b.Position.X ? a : b;
            Cart right = left == a ? b : a;

            // A chain collides as ONE body: its mass is the number of linked carts and the speed change goes to its motor
            // (the carriages just follow the motor along the trail).
            Cart bodyLeft = left.Body;
            Cart bodyRight = right.Body;

            float vLeft = bodyLeft.AlongSpeed;
            float vRight = bodyRight.AlongSpeed;

            if (vLeft - vRight < CartPhysics.CollisionMinSpeed)
            {
                return false; // not closing in on each other
            }

            float e = CartPhysics.CollisionRestitution;
            float massLeft = left.CollisionMass;
            float massRight = right.CollisionMass;
            float total = massLeft + massRight;

            bodyLeft.AlongSpeed = ((massLeft - e * massRight) * vLeft + (1f + e) * massRight * vRight) / total;
            bodyRight.AlongSpeed = ((massRight - e * massLeft) * vRight + (1f + e) * massLeft * vLeft) / total;

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
            if (!Active || IsCarriage)
            {
                return; // carriages are placed by their chain (CartChain.UpdateCarriages)
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

            if (OnTrack)
            {
                CheckPressurePlate();
            }

            // Fell out of the world
            if (Position.X < 32f || Position.X > (Main.maxTilesX - 2) * 16f || Position.Y > (Main.maxTilesY - 3) * 16f)
            {
                Active = false;
            }
        }

        /// <summary>Called by the chain after it placed this carriage: counts the bump cooldown down and runs the wheel animation.</summary>
        public void TickCarriage()
        {
            if (!Active)
            {
                return;
            }

            if (bumpCooldown > 0)
            {
                bumpCooldown--;
            }

            UpdateAnimation();
            CheckPressurePlate();
        }

        /// <summary>The motor bounced off a bumper earlier and this carriage has now reached that spot on the trail: fire Bumped for it.</summary>
        public void ReplayBump()
        {
            RaiseBumped();
        }

        /// <summary>
        /// Wheel animation: the frame advances with the distance travelled (so faster = quicker wheels), at most
        /// AnimMaxFramesPerTick, and is frozen while stopped. Derailed carts show the skin's in-air frame.
        /// Frame ranges come from the skin's mount (CartSkinTable.GetAnimation).
        /// </summary>
        private void UpdateAnimation()
        {
            int previousFrame = AnimFrame;

            StepAnimation();

            if (AnimFrame != previousFrame && FrameChanged != null)
            {
                FrameChanged(this, previousFrame, AnimFrame);
            }
        }

        private void StepAnimation()
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
            if (IsCarriage)
            {
                Chain.Motor.Shove(deltaSpeed); // a carriage just follows: pushing it pushes the whole chain
                return;
            }

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
            // A motor cart burns one unit of fuel per tick while it has some (same rule as the Gel Burner).
            bool driving = Module == CartModule.Motor && BurnFuelForDrive();

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

                if (Chain != null)
                {
                    Chain.Record(Position, Rotation); // carriages follow this trail
                }

                boost = (leftSurface && left.BoostDirection != 0) ? left.BoostDirection : (rightSurface ? right.BoostDirection : 0);
            }

            if (boost != 0)
            {
                Speed += boost * CartPhysics.BoostAccel;
            }
            else if (driving && Facing * Speed < CartPhysics.MotorSpeedCap)
            {
                Speed += Facing * CartPhysics.MotorAccel;

                if (Facing * Speed > CartPhysics.MotorSpeedCap)
                {
                    Speed = Facing * CartPhysics.MotorSpeedCap; // do not overshoot the cap
                }
            }
            else if (Speed != 0f)
            {
                Speed = Math.Abs(Speed) <= CartPhysics.Drag ? 0f : Speed - Math.Sign(Speed) * CartPhysics.Drag;
            }

            Speed = MathHelper.Clamp(Speed, -CartPhysics.MaxSpeed, CartPhysics.MaxSpeed);

            if (Module == CartModule.Motor)
            {
                return; // see AlongSpeed: the motor's facing is not derived from its speed
            }

            if (Speed > 0.01f)
            {
                Facing = 1;
            }
            else if (Speed < -0.01f)
            {
                Facing = -1;
            }
        }

        /// <summary>
        /// The pose a cart standing at x would have on the rail near referenceY (surface height under its feet and tilt), using the same
        /// two wheel probes as UpdateOnTrack. Used to lay out the trail behind a motor when a chain is created. False if there is no rail.
        /// </summary>
        public static bool TryRailPose(float x, float referenceY, float slope, out float surfaceY, out float rotation)
        {
            surfaceY = referenceY;
            rotation = 0f;

            float half = CartPhysics.WheelHalfBase * 0.6f;
            TrackSample left;
            TrackSample right;
            bool leftOk = TrySampleSmooth(x - half, referenceY - half * slope, out left);
            bool rightOk = TrySampleSmooth(x + half, referenceY + half * slope, out right);

            bool leftSurface = leftOk && left.HasSurface;
            bool rightSurface = rightOk && right.HasSurface;

            if (!leftSurface && !rightSurface)
            {
                return false;
            }

            float yLeft = leftSurface ? left.SurfaceY : right.SurfaceY;
            float yRight = rightSurface ? right.SurfaceY : left.SurfaceY;

            surfaceY = (yLeft + yRight) * 0.5f;
            rotation = (float)Math.Atan2(yRight - yLeft, 2f * half);
            return true;
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
                    if (Module == CartModule.Motor)
                    {
                        Facing = -Facing; // a motor cart must turn round at a dead end or it would sit there burning fuel
                    }

                    return false;

                case TrackEnd.Bounce:
                    Speed = -Speed;
                    if (Module == CartModule.Motor)
                    {
                        Facing = -Facing; // bump: the motor flips its facing and drives back the other way
                    }
                    else if (Speed != 0f)
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

            if (Chain != null && Chain.Motor == this)
            {
                Chain.MarkBump(); // stored in the trail: each carriage replays it when it reaches the bumper
            }

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

            if (Chain != null)
            {
                tag["chainId"] = Chain.SaveId;
                tag["chainSlot"] = Chain.Motor == this ? 0 : Chain.Carriages.IndexOf(this) + 1;
            }

            if (ChestItems != null)
            {
                List<TagCompound> items = new List<TagCompound>();
                for (int i = 0; i < ChestItems.Length; i++)
                {
                    items.Add(ItemIO.Save(ChestItems[i]));
                }

                tag["chest"] = items;

                // Which slots are filtered. The filter item is the item in the slot, so no item ids are saved.
                int filterMask = 0;
                for (int i = 0; i < ChestItems.Length; i++)
                {
                    if (IsSlotFiltered(i) && !ChestItems[i].IsAir)
                    {
                        filterMask |= 1 << i;
                    }
                }

                if (filterMask != 0)
                {
                    tag["chestFilter"] = filterMask;
                }
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

            if (burner != null && burner.Remaining > 0)
            {
                tag["fuelRemaining"] = burner.Remaining;
                tag["fuelCapacity"] = burner.Capacity;
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

            // Steampunk carts placed before the motor cart existed have no module: they become motor carts now.
            if (cart.Module == CartModule.Empty && IsMotorSkin(cart.SkinType))
            {
                cart.SetModule(CartModule.Motor);
            }

            cart.Position = new Vector2(tag.GetFloat("x"), tag.GetFloat("y"));
            cart.Speed = tag.GetFloat("speed");
            cart.Velocity = new Vector2(tag.GetFloat("vx"), tag.GetFloat("vy"));
            cart.OnTrack = tag.GetBool("onTrack");
            cart.LoadChainId = tag.GetInt("chainId");   // 0 = not in a chain (also what saves from before chains read as)
            cart.LoadChainSlot = tag.GetInt("chainSlot");

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

            if (cart.ChestItems != null && cart.SlotFilter != null && tag.ContainsKey("chestFilter"))
            {
                int filterMask = tag.GetInt("chestFilter");
                for (int i = 0; i < cart.ChestItems.Length; i++)
                {
                    if ((filterMask & (1 << i)) != 0 && !cart.ChestItems[i].IsAir)
                    {
                        cart.SlotFilter[i] = cart.ChestItems[i].type;
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
                cart.NormalizeTank();
            }

            if (cart.Fuel != null && tag.ContainsKey("fuel"))
            {
                cart.Fuel = ItemIO.Load(tag.GetCompound("fuel"));
            }

            if (tag.ContainsKey("fuelRemaining"))
            {
                cart.RestoreBurn(tag.GetInt("fuelRemaining"), tag.GetInt("fuelCapacity"));
            }

            return cart;
        }

        #endregion
    }
}