using Factorraria.Common.Systems;
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
        // --- ITEM PAYLOAD ---
        public int itemType;        // Terraria Item ID
        public int stackSize;       // Stack count
        public bool active;         // Is this item active in the world?

        // --- TIMERS ---
        public int pickupCooldown;  // Ticks before player/machine can collect
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

        // --- CONSTRUCTOR ---
        public VirtualItem(int type, int stack, int startTileX, int startTileY)
        {
            // Guard check: Reject invalid item IDs
            if (VirtualItemSystem.IsValidItemID(type) == false)
            {
                active = false;
                return;
            }

            itemType = type;
            stackSize = stack;
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

        // --- TARGET ASSIGNMENT ---
        bool FoundValidPath;
        public void SetTargetTile(int newTargetX, int newTargetY)
        {
            // If target hasn't changed, do nothing
            if (targetTileX == newTargetX && targetTileY == newTargetY)
            {
                return;
            }

            // Set new target coordinates
            targetTileX = newTargetX;
            targetTileY = newTargetY;

            // Register target tile in dictionary so other items know it's reserved
            VirtualItemSystem.RegisterItemTile(new Point(targetTileX, targetTileY), this);
        }

        // --- TICK UPDATE ---
        public void Update()
        {
            if (active == false)
            {
                return;
            }

            AnimateItem();
            MoveVirtualItem();

            // Countdown pickup timer
            if (pickupCooldown > 0)
            {
                pickupCooldown = pickupCooldown - 1;
            }
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

            // Unregister occupied tiles from dictionary
            VirtualItemSystem.UnregisterItemTile(new Point(currentTileX, currentTileY), this);
            VirtualItemSystem.UnregisterItemTile(new Point(targetTileX, targetTileY), this);
        }

        // --- MOVEMENT ---
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

        bool GetConveyorVector(int checkX, int checkY, int offsetX, int offsetY, out Vector2 push)
        {
            push = Vector2.Zero;

            if (!VirtualItemSystem.IsConveyorTile(checkX, checkY, out bool isClockwise))
            {
                return false;
            }

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

            if (IsDestinationTileValid(currentItemPositionX + (int)push.X, currentItemPositionY) && push.X != 0)
            {
                push = new Vector2(push.X, 0);
                FoundValidPath = true;
                return true;
            }else if(IsDestinationTileValid(currentItemPositionX, currentItemPositionY + (int)push.Y) && push.Y != 0) 
            {
                push = new Vector2(0, push.Y);
                FoundValidPath = true;
                return true;
            }

            push = Vector2.Zero;

            return false;
        }

        private bool IsDestinationTileValid(int targetX, int targetY)
        {
            if (!WorldGen.InWorld(targetX, targetY))
            {
                return false;
            }

            // Reject if another VirtualItem occupies or has reserved this tile
            if (VirtualItemSystem.IsTileOccupied(targetX, targetY, this))
            {
                return false;
            }

            Tile tile = Main.tile[targetX, targetY];

            // Reject solid blocks unless it's an open conveyor tile
            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
            {
                return false;
            }

            return true;
        }

        // Executes vector tallying, destination validation, and constant-speed tile movement
        public void MoveVirtualItem()
        {
            // --- RECALCULATION TRIGGER: IDLE / ARRIVED AT TARGET CENTER ---
            if (currentTileX == targetTileX && currentTileY == targetTileY)
            {
                FoundValidPath = false;
                Vector2 totalPush = Vector2.Zero;

                // do a while priority >= 0 && FoundValidPath = false
                // Scan 3x3 neighborhood (current tile + 8 surrounding neighbors)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        int checkX = currentTileX + offsetX;
                        int checkY = currentTileY + offsetY;

                        if (GetConveyorVector(checkX, checkY, offsetX, offsetY, out Vector2 push) == true)
                        {
                            // here we assign FoundValidPath = true, if false we go down priority
                            totalPush += push;
                        }
                    }
                }

                if (IsDestinationTileValid(currentTileX + (int)totalPush.X, currentTileY) && totalPush.X != 0)
                {
                    totalPush = new Vector2(totalPush.X, 0);
                }
                else if (IsDestinationTileValid(currentTileX, currentTileY + (int)totalPush.Y) && totalPush.Y != 0)
                {
                    totalPush = new Vector2(0, totalPush.Y);
                }

                // Normalize tallied forces to a single King's Move step (-1, 0, or 1)
                int dx = 0;
                int dy = 0;
                if (totalPush.X > 0)
                {
                    dx = 1;
                }
                else if (totalPush.X < 0)
                {
                    dx = -1;
                }
                if (totalPush.Y > 0)
                {
                    dy = 1;
                }
                else if (totalPush.Y < 0)
                {
                    dy = -1;
                }

                // Evaluate destination tile pathing
                if (dx != 0 || dy != 0)
                {
                    if (IsDestinationTileValid(currentTileX + dx, currentTileY + dy))
                    {
                        SetTargetTile(currentTileX + dx, currentTileY + dy);

                    }
                    else if (dx != 0 && dy != 0)
                    {
                        if (IsDestinationTileValid(currentTileX + dx, currentTileY))
                        {
                            SetTargetTile(currentTileX + dx, currentTileY);
                        }
                        else if (IsDestinationTileValid(currentTileX, currentTileY + dy))
                        {
                            SetTargetTile(currentTileX, currentTileY + dy);
                        }
                    }
                }
            }

            if (FoundValidPath)
            {
                CurrentFallVelocity = 1f;
            }
            else if(IsDestinationTileValid(currentTileX, currentTileY + 1))
            {
                SetTargetTile(currentTileX, currentTileY + 1);
                CurrentFallVelocity = Math.Clamp(CurrentFallVelocity + Gravity, 0f, MaxFallVelocity);
            }
            else
            {
                CurrentFallVelocity = 1f;
            }

            // --- CONSTANT-SPEED MOVEMENT (NO INTERMEDIATE RECALCULATIONS) ---
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
            }
            else
            {
                Vector2 direction = targetPixelPosition - worldPosition;
                direction.Normalize();
                worldPosition = worldPosition + (direction * moveSpeed * CurrentFallVelocity);
            }
        }

        // DEBUG
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

                if (adjTile.HasTile && VirtualItemSystem.IsConveyorTile(adjX, adjY, out bool isClockwise))
                {
                    // World center of the nth neighbor tile (16px per tile + 8px center offset)
                    Vector2 conveyorWorldCenter = new Vector2(adjX * 16 + 8, adjY * 16 + 8);

                    // Fetch the vector from your dictionary (using frame, state key, or tile key)
                    GetConveyorVector(adjX, adjY, offset.X, offset.Y, out Vector2 pushVector);

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
    }
}