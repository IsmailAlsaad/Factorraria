using Factorraria.Common.Harvester;
using Factorraria.Common.Systems;
using Factorraria.Common.VirtualItems;
using Factorraria.Content.Tiles.Conveyors;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.VirtualItems
{
    public class VirtualItem
    {
        #region Variables

        // --- ITEM PAYLOAD ---
        // The real item this vItem carries (type, stack, prefix, ModItem/GlobalItem data). Single source of truth.
        // Null only when the vItem was rejected in its constructor (active == false).
        // Never share this instance with an inventory/chest/machine slot: clone on the way in and out.
        public Item Payload;

        // Compatibility surface: existing call sites keep using these.
        public int itemType => Payload?.type ?? 0;   // Terraria Item ID
        public int stackSize                          // Stack count
        {
            get => Payload?.stack ?? 0;
            set { if (Payload != null) Payload.stack = value; }
        }
        public int prefix => Payload?.prefix ?? 0;

        public bool active;         // Is this item active in the world?

        // Patch 7.1: stable runtime id handed out by VirtualItemSystem when the vItem is spawned (0 = none). Never saved, never reused in a session.
        public int Id;

        // Patch 7.2: client replicas only. The server tells the client where the vItem is going and how many pixels it moves per tick.
        public float ReplicaStep;

        // Pixels per tick the vItem is moving toward its target right now (what a replica needs to move the same way).
        public float StepPerTick => moveSpeed * CurrentFallVelocity;

        // --- TIMERS ---
        public int pickupCooldown;  // Ticks before player/machine can collect
        public int cartGrabCooldown; // Ticks before a cart can grab it. Set when a cart spills it. Unlike pickupCooldown, falling does not reset it.
        public int frame;          // Current animation frame index
        public int frameCounter;   // Ticks spent on current frame

        // --- GRID POSITION ---
        public int currentTileX;    // Current tile X coordinate
        public int currentTileY;    // Current tile Y coordinate

        public int targetTileX;     // Destination tile X coordinate
        public int targetTileY;     // Destination tile Y coordinate

        // --- MOVEMENT & POSITION ---
        public Vector2 worldPosition;  // Visual world position in pixels
        public float moveSpeed;        // Speed in pixels per frame
        float Gravity = 0.07f;
        float MaxFallVelocity = 10f;
        float CurrentFallVelocity;

        int MaxConveyorPriority;
        bool FoundValidPath;
        bool FoundConveyor;
        // Per conveyor priority: which of the 4 directions have a valid path (bit d = direction d, see DirX/DirY). Same direction twice is one path; opposite directions no longer cancel.
        Dictionary<int,int> pathMasks = new Dictionary<int, int>();

        // True on a vItem created by an auto-split while it slides from its parent's position into its own tile. No conveyor logic runs until it has arrived.
        bool settlingFromSplit;

        static readonly Dictionary<(int, int), Vector2> ClockwiseConveyorPushTable = new Dictionary<(int, int), Vector2>
        {
            [(0, 0)] = new Vector2(0, 0),
            [(1, 1)] = new Vector2(1, 0),
            [(1, 0)] = new Vector2(0, -1),
            [(1, -1)] = new Vector2(0, -1),
            [(0, -1)] = new Vector2(-1, 0),
            [(-1, -1)] = new Vector2(-1, 0),
            [(-1, 0)] = new Vector2(0, 1),
            [(-1, 1)] = new Vector2(0, 1),
            [(0, 1)] = new Vector2(1, 0)
        }; static readonly Dictionary<(int, int), Vector2> CounterClockwiseConveyorPushTable = new Dictionary<(int, int), Vector2>
        {
            [(0, 0)] = new Vector2(0, 0),
            [(1, 1)] = new Vector2(0, 1),
            [(1, 0)] = new Vector2(0, 1),
            [(1, -1)] = new Vector2(1, 0),
            [(0, -1)] = new Vector2(1, 0),
            [(-1, -1)] = new Vector2(0, -1),
            [(-1, 0)] = new Vector2(0, -1),
            [(-1, 1)] = new Vector2(-1, 0),
            [(0, 1)] = new Vector2(-1, 0)
        };


        #endregion 

        // Plain item of the given type (fresh item, no prefix or data).
        public VirtualItem(int type, int stack, int startTileX, int startTileY)
        {
            // Guard check: Reject invalid item IDs
            if (VirtualItemSystem.IsValidItemID(type) == false)
            {
                active = false;
                return;
            }

            Payload = new Item(type, stack);
            Init(startTileX, startTileY);
        }

        // Takes OWNERSHIP of payload (pass item.Clone() or a detached item, never a slot's own instance).
        public VirtualItem(Item payload, int startTileX, int startTileY)
        {
            if (payload == null || payload.IsAir || VirtualItemSystem.IsValidItemID(payload.type) == false)
            {
                active = false;
                return;
            }

            Payload = payload;
            Payload.favorited = false;
            Payload.GetGlobalItem<VItemGlobalItem>().ConveyorImmunityTimer = 0;
            Init(startTileX, startTileY);
        }

        void Init(int startTileX, int startTileY)
        {
            active = true;

            pickupCooldown = 80;

            currentTileX = startTileX;
            currentTileY = startTileY;

            targetTileX = startTileX;
            targetTileY = startTileY;

            // Center position inside 16x16 tile grid
            float pixelX = (startTileX * 16) + 8;
            float pixelY = (startTileY * 16) + 8;
            worldPosition = new Vector2(pixelX, pixelY);

            moveSpeed = 1.0f;

            // Register initial tile in the dictionary map
            VirtualItemSystem.RegisterItemTile(new Point(currentTileX, currentTileY), this);
        }

        public void Update()
        {
            if (active == false)
            {
                return;
            }

            AnimateItem();
            MoveVirtualItem();
            TryUpgradeCoinTier();

            if (pickupCooldown > 0)
            {
                pickupCooldown = pickupCooldown - 1;
            }

            if (cartGrabCooldown > 0)
            {
                cartGrabCooldown--;
            }
        }

        private VirtualItem() { }

        /// <summary>
        /// Patch 7.2, multiplayer client only: a display copy of a server vItem. It owns the payload, is never put in tileItemMap and runs
        /// ReplicaTick instead of Update, so no conveyor logic ever runs for it.
        /// </summary>
        public static VirtualItem CreateReplica(int id, Item payload, Vector2 position, int targetX, int targetY, float step)
        {
            VirtualItem replica = new VirtualItem();
            replica.Payload = payload;
            replica.active = true;
            replica.Id = id;
            replica.worldPosition = position;
            replica.currentTileX = (int)(position.X / 16f);
            replica.currentTileY = (int)(position.Y / 16f);
            replica.targetTileX = targetX;
            replica.targetTileY = targetY;
            replica.moveSpeed = 1f;
            replica.ReplicaStep = step;
            return replica;
        }

        /// <summary>Applies one update entry. newPayload is null when only the stack changed.</summary>
        public void ApplyReplicaState(Item newPayload, int stack, Vector2 position, int targetX, int targetY, float step)
        {
            if (newPayload != null)
            {
                Payload = newPayload;
            }
            else if (Payload != null)
            {
                Payload.stack = stack;
            }

            targetTileX = targetX;
            targetTileY = targetY;
            ReplicaStep = step;

            // Between updates the replica moves on its own the same way the server does, so only a real difference is corrected.
            if (Vector2.Distance(worldPosition, position) > 6f)
            {
                worldPosition = position;
            }
        }

        /// <summary>Client replica: animate and move toward the target tile, exactly like MoveVirtualItem does on the server.</summary>
        public void ReplicaTick()
        {
            if (active == false)
            {
                return;
            }

            AnimateItem();

            Vector2 targetPixelPosition = new Vector2((targetTileX * 16) + 8, (targetTileY * 16) + 8);
            float distanceToTarget = Vector2.Distance(worldPosition, targetPixelPosition);

            if (distanceToTarget <= ReplicaStep)
            {
                worldPosition = targetPixelPosition;
            }
            else
            {
                Vector2 direction = targetPixelPosition - worldPosition;
                direction.Normalize();
                worldPosition = worldPosition + (direction * ReplicaStep);
            }

            currentTileX = (int)(worldPosition.X / 16f);
            currentTileY = (int)(worldPosition.Y / 16f);
        }

        public void SetTargetTile(int newTargetX, int newTargetY)
        {
            // If target hasn't changed, do nothing
            if (targetTileX == newTargetX && targetTileY == newTargetY)
            {
                return;
            }

            VirtualItemSystem.UnregisterItemTile(new Point(targetTileX, targetTileY), this);

            // Set new target coordinates
            targetTileX = newTargetX;
            targetTileY = newTargetY;

            // Register target tile in dictionary so other items know it's reserved
            VirtualItemSystem.RegisterItemTile(new Point(targetTileX, targetTileY), this);
        }

        void AnimateItem()
        {
            if (Main.itemAnimations[itemType] != null)
            {
                frameCounter++;
                if (frameCounter >= Main.itemAnimations[itemType].TicksPerFrame)
                {
                    frameCounter = 0;
                    frame++;
                    if (frame >= Main.itemAnimations[itemType].FrameCount)
                    {
                        frame = 0;
                    }
                }
            }
        }

        // --- ANIMATION HELPER ---
        public Rectangle GetSourceRectangle(Texture2D texture)
        {
            if (Main.itemAnimations[itemType] != null)
            {
                int totalFrames = Main.itemAnimations[itemType].FrameCount;
                int frameHeight = texture.Height / totalFrames;
                return new Rectangle(0, frame * frameHeight, texture.Width, frameHeight);
            }

            return texture.Frame(); // Returns full texture bounds for non-animated items
        }

        // --- DESPAWN / REMOVE CLEANUP ---
        public void Remove()
        {
            active = false;
            VirtualItemSystem.ForgetId(this);

            // Unregister occupied tiles from dictionary
            VirtualItemSystem.UnregisterItemTile(new Point(currentTileX, currentTileY), this);
            VirtualItemSystem.UnregisterItemTile(new Point(targetTileX, targetTileY), this);
        }

        // --- MOVEMENT ---
        bool GetConveyorVector(int checkX, int checkY, int offsetX, int offsetY, out Vector2 push, out int ConveyorPriority)
        {
            push = Vector2.Zero;

            if (!VirtualItemSystem.IsConveyorTile(checkX, checkY, out bool isClockwise, out ConveyorPriority))
            {
                return false;
            }

            FoundConveyor = true;

            if (isClockwise)
            {
                push = ClockwiseConveyorPushTable[(offsetX, offsetY)];
            }
            else
            {
                push = CounterClockwiseConveyorPushTable[(offsetX, offsetY)];
            }

            int currentItemPositionX = checkX - offsetX;
            int currentItemPositionY = checkY - offsetY;

            if (IsTilePassable(currentItemPositionX + (int)push.X, currentItemPositionY) && push.X != 0)
            {
                push = new Vector2(push.X, 0);
                FoundValidPath = true;
                return true;
            }
            else if (IsTilePassable(currentItemPositionX, currentItemPositionY + (int)push.Y) && push.Y != 0)
            {
                push = new Vector2(0, push.Y);
                FoundValidPath = true;
                return true;
            }

            push = Vector2.Zero;

            return false;
        }

        // Scan 3x3 neighborhood (current tile + 8 surrounding neighbors)
        void ScanNeighborsForConveyors()
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int checkX = currentTileX + offsetX;
                    int checkY = currentTileY + offsetY;

                    // here we assign FoundValidPath = true
                    if (GetConveyorVector(checkX, checkY, offsetX, offsetY, out Vector2 push, out int conveyorPriority))
                    {
                        if (VirtualItemSystem.TryGetConveyorFilter(checkX,checkY, out int FilterID))
                        {
                            if(FilterID == itemType)
                            {
                                conveyorPriority++;
                            }
                            if(FilterID != itemType && FilterID != ItemID.None)
                            {
                                conveyorPriority = 0;
                            }
                        }

                        // We are only assigining MaxConveyorPriority on conveyors that had a valid path, if a higher priority didn't have a valid path it won't affect MaxConveyorPriority
                        if (MaxConveyorPriority < conveyorPriority)
                        {
                            MaxConveyorPriority = conveyorPriority;
                        }

                        // Each valid conveyor adds its direction to the set of paths for its priority
                        pathMasks.TryGetValue(conveyorPriority, out int existingMask);
                        pathMasks[conveyorPriority] = existingMask | PushToDirectionBit(push);
                    }
                }
            }
        }

        void CalculateMovementPath()
        {
            // A split-off vItem is still sliding out of its parent's tile (MoveVirtualItem clears this on arrival)
            if (settlingFromSplit)
            {
                return;
            }

            if (currentTileX == targetTileX && currentTileY == targetTileY)
            {
                MaxConveyorPriority = 0;
                FoundConveyor = false;
                FoundValidPath = false;
                pathMasks.Clear();

                // Scan 3x3 neighborhood
                ScanNeighborsForConveyors();

                int pathMask = 0;
                if (pathMasks.Count > 0)
                {
                    pathMasks.TryGetValue(MaxConveyorPriority, out pathMask);
                }

                MoveAlongPaths(pathMask);
            }

            // --- GRAVITY CHECK ---
            if (FoundValidPath || FoundConveyor)
            {
                CurrentFallVelocity = 1f;
            }
            else if (IsTilePassable(currentTileX, currentTileY + 1) && !RestsOn(currentTileX, currentTileY + 1))
            {
                pickupCooldown = 0;
                VirtualItem targetItem = VirtualItemSystem.GetVirtualItemAtTile(currentTileX, currentTileY + 1, this);
                
                if (!TryMergeWithVirtualItem(targetItem) && IsTileClear(currentTileX, currentTileY + 1))
                {
                    SetTargetTile(currentTileX, currentTileY + 1);

                    CurrentFallVelocity = Math.Clamp(CurrentFallVelocity + Gravity, 0f, MaxFallVelocity);
                }
            }
            else
            {
                pickupCooldown = 0;
                CurrentFallVelocity = 1f;
            }
        }

        // Directions: 0 = +X, 1 = -X, 2 = +Y, 3 = -Y (bit d of a path mask). X is listed first so a lone item still prefers X like before.
        int DirX(int d)
        {
            return d == 0 ? 1 : (d == 1 ? -1 : 0);
        }

        int DirY(int d)
        {
            return d == 2 ? 1 : (d == 3 ? -1 : 0);
        }

        int PushToDirectionBit(Vector2 push)
        {
            if (push.X > 0) return 1;
            if (push.X < 0) return 2;
            if (push.Y > 0) return 4;
            if (push.Y < 0) return 8;
            return 0;
        }

        // Same checks TryMergeWithVirtualItem makes: could this stack put at least one item into target right now?
        bool CanMergeInto(VirtualItem target)
        {
            if (target == null || !target.active || target == this)
            {
                return false;
            }

            if (!VItemStacking.CanMerge(target.Payload, Payload))
            {
                return false;
            }

            if (Vector2.Distance(worldPosition, target.worldPosition) > 17f)
            {
                return false;
            }

            return target.Payload.maxStack - target.stackSize > 0;
        }

        // Server / single-player only (Update never runs on a client). Splits the stack over every available path of the highest priority.
        void MoveAlongPaths(int pathMask)
        {
            if (pathMask == 0 || stackSize <= 0)
            {
                return;
            }

            // A path is available when its tile is free, or holds a stack this one can merge into
            Span<int> available = stackalloc int[4];
            int availableCount = 0;

            for (int d = 0; d < 4; d++)
            {
                if ((pathMask & (1 << d)) == 0)
                {
                    continue;
                }

                int nextX = currentTileX + DirX(d);
                int nextY = currentTileY + DirY(d);
                VirtualItem occupant = VirtualItemSystem.GetVirtualItemAtTile(nextX, nextY, this);

                bool isAvailable = occupant != null ? CanMergeInto(occupant) : IsTileClear(nextX, nextY);
                if (isAvailable)
                {
                    available[availableCount] = d;
                    availableCount++;
                }
            }

            if (availableCount == 0)
            {
                return;
            }

            // Can't split a stack into more parts than it has items. A stack of 1 just takes the first available path.
            int pathsUsed = Math.Min(availableCount, stackSize);
            int total = stackSize;
            int baseShare = total / pathsUsed;
            int remainder = total % pathsUsed;

            // Children first (paths 1..n-1); this vItem keeps whatever is left and takes path 0
            for (int i = 1; i < pathsUsed; i++)
            {
                int d = available[i];
                int amount = baseShare + (i < remainder ? 1 : 0);
                SendPartTo(currentTileX + DirX(d), currentTileY + DirY(d), amount);
            }

            int firstX = currentTileX + DirX(available[0]);
            int firstY = currentTileY + DirY(available[0]);
            VirtualItem firstOccupant = VirtualItemSystem.GetVirtualItemAtTile(firstX, firstY, this);

            if (firstOccupant != null)
            {
                TryMergeWithVirtualItem(firstOccupant);
            }
            else
            {
                SetTargetTile(firstX, firstY);
            }
        }

        // Moves "amount" items of this stack into the neighbouring tile: merged into the stack standing there, or a new vItem that slides out of this one.
        void SendPartTo(int tileX, int tileY, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            VirtualItem occupant = VirtualItemSystem.GetVirtualItemAtTile(tileX, tileY, this);

            if (occupant != null)
            {
                int moved = Math.Min(amount, occupant.Payload.maxStack - occupant.stackSize);
                if (moved > 0)
                {
                    occupant.stackSize += moved;
                    stackSize -= moved;
                }

                return;
            }

            Item part = Payload.Clone();
            part.stack = amount;

            VirtualItem child = VirtualItemSystem.SpawnVirtualItem(part, tileX, tileY);
            if (child == null)
            {
                return;
            }

            stackSize -= amount;

            // Spawned logically on its own tile, but visually starts where this vItem is and slides over (replicas get the same position + target)
            child.worldPosition = worldPosition;
            child.pickupCooldown = pickupCooldown;
            child.cartGrabCooldown = cartGrabCooldown;
            child.CurrentFallVelocity = 1f;
            child.settlingFromSplit = true;
        }

        public void MoveVirtualItem()
        {
            CalculateMovementPath();

            float targetPixelX = (targetTileX * 16) + 8;
            float targetPixelY = (targetTileY * 16) + 8;
            Vector2 targetPixelPosition = new Vector2(targetPixelX, targetPixelY);

            float distanceToTarget = Vector2.Distance(worldPosition, targetPixelPosition);

            if (distanceToTarget <= moveSpeed * CurrentFallVelocity)
            {
                if (currentTileX != targetTileX || currentTileY != targetTileY)
                {
                    VirtualItemSystem.UnregisterItemTile(new Point(currentTileX, currentTileY), this);
                    currentTileX = targetTileX;
                    currentTileY = targetTileY;
                    worldPosition = targetPixelPosition;
                }
                else if (settlingFromSplit)
                {
                    // A split-off vItem has arrived at its own tile
                    worldPosition = targetPixelPosition;
                    settlingFromSplit = false;
                }
            }
            else
            {
                Vector2 direction = targetPixelPosition - worldPosition;
                direction.Normalize();
                worldPosition = worldPosition + (direction * moveSpeed * CurrentFallVelocity);
            }
        }

        private bool IsTilePassable(int targetX, int targetY)
        {
            if (!WorldGen.InWorld(targetX, targetY))
            {
                return false;
            }

            Tile tile = Main.tile[targetX, targetY];
            if (tile.HasTile && Main.tileSolid[tile.TileType] && !isPlatform(targetX, targetY))
            {
                return false;
            }

            return true;
        }

        // Checks terrain AND verifies the tile has no item occupied or reserved
        private bool IsTileClear(int targetX, int targetY)
        {
            return IsTilePassable(targetX, targetY) && !VirtualItemSystem.IsTileOccupied(targetX, targetY, this);
        }

        bool isPlatform(int tileX,int tileY)
        {
            if (!WorldGen.InWorld(tileX, tileY))
            {
                return false;
            }

            Tile tile = Main.tile[tileX, tileY];

            return tile.HasTile && Main.tileSolidTop[tile.TileType];
        }

        // A platform an item rests on. Harvester boxes are solid-top for the player and NPCs, but items fall straight through them.
        bool RestsOn(int tileX, int tileY)
        {
            if (!isPlatform(tileX, tileY))
            {
                return false;
            }

            return !HarvesterBoxRows.IsBoxTile(Main.tile[tileX, tileY].TileType);
        }

        public bool TryMergeWithVirtualItem(VirtualItem targetItem)
        {
            if (targetItem == null || !targetItem.active || targetItem == this)
            {
                return false;
            }

            // Same type, same prefix and compatible mod data (ItemLoader.CanStack)
            if (!VItemStacking.CanMerge(targetItem.Payload, Payload))
            {
                return false;
            }

            // Wait until the two are about touching
            float distance = Vector2.Distance(worldPosition, targetItem.worldPosition);
            if(distance > 17f)
            {
                return false;
            }

            // Calculate how much space remains in the target item's stack
            int spaceRemaining = targetItem.Payload.maxStack - targetItem.stackSize;
            if (spaceRemaining <= 0)
            {
                return false;
            }

            // Partial or full transfer logic
            int transferAmount = Math.Min(stackSize, spaceRemaining);
            targetItem.stackSize += transferAmount;
            stackSize -= transferAmount;

            // Deactivate and remove if stack is emptied
            if (stackSize <= 0)
            {
                //Remove();
                return true;
            }

            return false;
        }

        void TryUpgradeCoinTier()
        {
            // Only coins merge up a tier. (This used to reset ANY stack of exactly 100 to 1, which destroyed items.)
            bool isUpgradableCoin = itemType == ItemID.CopperCoin || itemType == ItemID.SilverCoin || itemType == ItemID.GoldCoin;

            if (stackSize != 100 || !isUpgradableCoin)
            {
                return;
            }

            int nextTier = ItemID.PlatinumCoin;
            if (itemType == ItemID.CopperCoin)
            {
                nextTier = ItemID.SilverCoin;
            }
            else if (itemType == ItemID.SilverCoin)
            {
                nextTier = ItemID.GoldCoin;
            }

            // Coins carry no prefix or data, so resetting the payload to the next coin type is safe
            Payload.SetDefaults(nextTier);
            Payload.stack = 1;
        }

        #region Debugging
        public void DrawAdjacentConveyorVectors(SpriteBatch spriteBatch, int originTileX, int originTileY)
        {
            // All 8 neighboring tile offsets (Moore neighborhood)
            Point[] adjacentOffsets = new Point[]
            {
                new Point(-1, -1), new Point(0, -1), new Point(1, -1), // Top-Left, Top, Top-Right
                new Point(-1,  0),                   new Point(1,  0), // Left, Right
                new Point(-1,  1), new Point(0,  1), new Point(1,  1)  // Bottom-Left, Bottom, Bottom-Right
            };

            foreach (Point offset in adjacentOffsets)
            {
                int adjX = originTileX + offset.X;
                int adjY = originTileY + offset.Y;

                if (!WorldGen.InWorld(adjX, adjY))
                    continue;

                Tile adjTile = Main.tile[adjX, adjY];

                if (adjTile.HasTile && VirtualItemSystem.IsConveyorTile(adjX, adjY, out bool isClockwise, out _))
                {
                    // World center of the nth neighbor tile (16px per tile + 8px center offset)
                    Vector2 conveyorWorldCenter = new Vector2(adjX * 16 + 8, adjY * 16 + 8);

                    // Fetch the vector from your dictionary (using frame, state key, or tile key)
                    GetConveyorVector(adjX, adjY, offset.X, offset.Y, out Vector2 pushVector, out _);
                    
                    // Convert world coords to screen space for drawing
                    Vector2 startScreenPos = conveyorWorldCenter - Main.screenPosition;
                    Vector2 endScreenPos = startScreenPos + (pushVector * 16f); // Scale length as needed

                    // Draw vector originating at the center of this specific neighbor
                    Texture2D pixel = TextureAssets.MagicPixel.Value;
                    DrawLine(spriteBatch, pixel,startScreenPos, endScreenPos, Color.Yellow, 2f);
                }
            }
        }
        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 delta = end - start;
            float angle = (float)Math.Atan2(delta.Y, delta.X);
            sb.Draw(
                pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)delta.Length(), (int)thickness),
                null,
                color,
                angle,
                Vector2.Zero,
                SpriteEffects.None,
                0f
            );
        }
        #endregion
    }
}