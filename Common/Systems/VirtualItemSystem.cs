using Factorraria.Common.VirtualItems;
using Factorraria.Content.Configs;
using Factorraria.Content.Tiles.Conveyors;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI.Chat;

namespace Factorraria.Content.VirtualItems
{
    public class VirtualItemSystem : ModSystem
    {
        // Master list of active items
        public static List<VirtualItem> virtualItems = new List<VirtualItem>();

        // Fast O(1) position lookup dictionary
        public static Dictionary<Point, VirtualItem> tileItemMap = new Dictionary<Point, VirtualItem>();

        // --- ITEM ID SAFETY GUARD ---
        public static bool IsValidItemID(int type)
        {
            if (type <= ItemID.None)
            {
                return false;
            }

            if (type >= ItemLoader.ItemCount)
            {
                return false;
            }

            return true;
        }

        // --- DICTIONARY REGISTRATION HELPERS ---
        public static void RegisterItemTile(Point tilePoint, VirtualItem item)
        {
            tileItemMap[tilePoint] = item;
        }

        public static void UnregisterItemTile(Point tilePoint, VirtualItem item)
        {
            if (tileItemMap.TryGetValue(tilePoint, out VirtualItem existingItem))
            {
                if (existingItem == item)
                {
                    tileItemMap.Remove(tilePoint);
                }
            }
        }

        // --- WORLD LIFECYCLE ---
        public override void OnWorldLoad()
        {
            virtualItems.Clear();
            tileItemMap.Clear();
        }

        public override void OnWorldUnload()
        {
            virtualItems.Clear();
            tileItemMap.Clear();
        }

        // --- SAVE DATA TO WORLD ---
        public override void SaveWorldData(TagCompound tag)
        {
            List<TagCompound> savedItemList = new List<TagCompound>();

            for (int i = 0; i < virtualItems.Count; i++)
            {
                VirtualItem item = virtualItems[i];

                if (item.active == true && IsValidItemID(item.itemType) == true)
                {
                    TagCompound itemData = new TagCompound();

                    itemData["itemType"] = item.itemType;
                    itemData["stackSize"] = item.stackSize;
                    itemData["currentTileX"] = item.currentTileX;
                    itemData["currentTileY"] = item.currentTileY;
                    itemData["targetTileX"] = item.targetTileX;
                    itemData["targetTileY"] = item.targetTileY;
                    itemData["worldPosX"] = item.worldPosition.X;
                    itemData["worldPosY"] = item.worldPosition.Y;

                    savedItemList.Add(itemData);
                }
            }

            tag["virtualItemsData"] = savedItemList;
        }

        // --- LOAD DATA FROM WORLD ---
        public override void LoadWorldData(TagCompound tag)
        {
            virtualItems.Clear();
            tileItemMap.Clear();

            if (tag.ContainsKey("virtualItemsData") == true)
            {
                IList<TagCompound> savedItemList = tag.GetList<TagCompound>("virtualItemsData");

                for (int i = 0; i < savedItemList.Count; i++)
                {
                    TagCompound itemData = savedItemList[i];

                    int type = itemData.GetInt("itemType");

                    if (IsValidItemID(type) == false)
                    {
                        continue;
                    }

                    int stack = itemData.GetInt("stackSize");
                    int currentX = itemData.GetInt("currentTileX");
                    int currentY = itemData.GetInt("currentTileY");

                    VirtualItem item = SpawnVirtualItem(type, stack, currentX, currentY);

                    if (item != null)
                    {
                        int targetX = itemData.GetInt("targetTileX");
                        int targetY = itemData.GetInt("targetTileY");
                        float posX = itemData.GetFloat("worldPosX");
                        float posY = itemData.GetFloat("worldPosY");

                        item.worldPosition.X = posX;
                        item.worldPosition.Y = posY;

                        item.SetTargetTile(targetX, targetY);
                    }
                }
            }
        }

        // --- TICK UPDATE ---
        public override void PostUpdateWorld()
        {
            ConvertItemsToVItems();
            CheckPlayerPickups();

            for (int i = virtualItems.Count - 1; i >= 0; i--)
            {
                VirtualItem item = virtualItems[i];

                item.Update();

                if (item.active == false)
                {
                    item.Remove();
                    virtualItems.RemoveAt(i);
                }
            }
        }

        // --- DRAWING ITEMS IN WORLD ---
        public override void PostDrawTiles()
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            for (int i = 0; i < virtualItems.Count; i++)
            {
                VirtualItem item = virtualItems[i];

                if (item.active == false)
                {
                    continue;
                }

                //
                FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
                if (config.EnableDebugs)
                {
                    DrawItemDebug(Main.spriteBatch, item);
                    item.DrawAdjacentConveyorVectors(Main.spriteBatch,item.currentTileX,item.currentTileY);
                }
                //

                // Get the single-frame source rectangle and texture
                Texture2D texture = TextureAssets.Item[item.itemType].Value;
                Rectangle sourceRect = item.GetSourceRectangle(texture);

                Vector2 screenPosition = item.worldPosition - Main.screenPosition;
                Vector2 origin = sourceRect.Size() / 2f;

                int tileX = (int)(item.worldPosition.X / 16f);
                int tileY = (int)(item.worldPosition.Y / 16f);
                Color lightColor = Lighting.GetColor(tileX, tileY);

                // Render at native size (1f scale)
                Main.spriteBatch.Draw(
                    texture,
                    screenPosition,
                    sourceRect,
                    lightColor,
                    0f,
                    origin,
                    1f, // Native 1:1 scale
                    SpriteEffects.None,
                    0f
                );
            }

            Main.spriteBatch.End();
        }

        // --- HELPER METHODS ---

        public static VirtualItem SpawnVirtualItem(int type, int stack, int tileX, int tileY)
        {
            if (IsValidItemID(type) == false)
            {
                return null;
            }

            VirtualItem newItem = new VirtualItem(type, stack, tileX, tileY);

            if (newItem.active == false)
            {
                return null;
            }

            virtualItems.Add(newItem);
            return newItem;
        }

        // FAST O(1) LOOKUP USING DICTIONARY
        public static VirtualItem GetVirtualItemAtTile(int tileX, int tileY, VirtualItem ignoreItem = null)
        {
            Point searchPoint = new Point(tileX, tileY);

            if (tileItemMap.TryGetValue(searchPoint, out VirtualItem item) == true)
            {
                if (item != ignoreItem && item.active == true)
                {
                    return item;
                }
            }

            return null;
        }

        public static bool IsTileOccupied(int tileX, int tileY, VirtualItem ignoreItem = null)
        {
            VirtualItem item = GetVirtualItemAtTile(tileX, tileY, ignoreItem);

            if (item != null)
            {
                return true;
            }

            return false;
        }

        static Vector2[] Neighbors4 = new Vector2[4]
        {
            new Vector2(0,1),
            new Vector2(1,0),
            new Vector2(0,-1),
            new Vector2(-1,0)
        };

        // Converts loose dropped world items into Virtual Items if they land on a conveyor
        public static void ConvertItemsToVItems()
        {
            for (int i = 0; i < Main.maxItems; i++)
            {
                Item worldItem = Main.item[i];

                if (worldItem.active == false || worldItem.GetGlobalItem<VItemGlobalItem>().ConveyorImmunityTimer > 0)
                {
                    continue;
                }

                if (IsValidItemID(worldItem.type) == false)
                {
                    continue;
                }

                int centerTileX = (int)(worldItem.Center.X / 16f);
                int centerTileY = (int)(worldItem.Center.Y / 16f);

                int tileX;
                int tileY;

                for (int k = 0; k < Neighbors4.Length; k++)
                {
                    tileX = centerTileX + (int)Neighbors4[k].X;
                    tileY = centerTileY + (int)Neighbors4[k].Y;

                    if (IsConveyorTile(tileX, tileY, out _) == true)
                    {
                        if (IsTileOccupied(tileX, tileY) == false)
                        {
                            SpawnVirtualItem(worldItem.type, worldItem.stack, centerTileX, centerTileY);

                            worldItem.active = false;
                            worldItem.TurnToAir();
                            return;
                        }
                    }
                }
            }
        }

        // Converts a single VirtualItem back into a real dropped world item
        public static Item ConvertVItemsToItems(VirtualItem vItem)
        {
            if (vItem == null)
            {
                return null;
            }

            if (vItem.active == false)
            {
                return null;
            }

            if (IsValidItemID(vItem.itemType) == false)
            {
                return null;
            }

            // Spawn real vanilla dropped item at the virtual item's exact world position
            int newItemIndex = Item.NewItem(
                new EntitySource_Misc("Factorraria_VirtualItem"),
                (int)vItem.worldPosition.X,
                (int)vItem.worldPosition.Y,
                0,
                0,
                vItem.itemType,
                vItem.stackSize
            );

            // Mark virtual item for removal from conveyor system
            vItem.Remove();

            if (newItemIndex >= 0 && newItemIndex < Main.maxItems)
            {
                return Main.item[newItemIndex];
            }

            return null;
        }

        // Checks if any player touches a VirtualItem and converts it to a real world item for pickup
        public static void CheckPlayerPickups()
        {
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];

                if (player.active == false || player.dead == true || player.ghost == true)
                {
                    continue;
                }

                // Encumbering Stone prevents picking up items
                if (player.HasItem(ItemID.EncumberingStone) == true)
                {
                    continue;
                }

                // Convert player hitbox to tile bounds
                int minTileX = (int)(player.Hitbox.Left / 16f) - 1;
                int maxTileX = (int)(player.Hitbox.Right / 16f) + 1;
                int minTileY = (int)(player.Hitbox.Top / 16f) - 1;
                int maxTileY = (int)(player.Hitbox.Bottom / 16f) + 1;

                for (int x = minTileX; x <= maxTileX; x++)
                {
                    for (int y = minTileY; y <= maxTileY; y++)
                    {
                        VirtualItem vItem = GetVirtualItemAtTile(x, y);

                        if (vItem == null || vItem.active == false)
                        {
                            continue;
                        }

                        if (vItem.pickupCooldown > 0)
                        {
                            continue;
                        }

                        // Create test item to evaluate inventory space
                        Item testItem = new Item();
                        testItem.SetDefaults(vItem.itemType);
                        testItem.stack = vItem.stackSize;

                        // Check 2: Correct case-sensitive ItemSpace with ref parameter
                        if (player.ItemSpace(testItem).CanTakeItem == true)
                        {
                            Item spawnedItem = ConvertVItemsToItems(vItem);
                            spawnedItem.GetGlobalItem<VItemGlobalItem>().ConveyorImmunityTimer = 60;

                            if (spawnedItem != null)
                            {
                                spawnedItem.noGrabDelay = 0;
                            }
                        }
                    }
                }
            }
        }

        // Checks if a tile coordinate is a valid conveyor belt
        public static bool IsConveyorTile(int tileX, int tileY, out bool isClockwise)
        {
            isClockwise = true;
            if (WorldGen.InWorld(tileX, tileY) == false)
            {
                return false;
            }

            Tile tile = Main.tile[tileX, tileY];

            if (tile.HasTile == false)
            {
                return false;
            }

            if (tile.TileType == TileID.ConveyorBeltRight)
            {
                isClockwise = false;
                return true;
            }

            if (tile.TileType == TileID.ConveyorBeltLeft)
            {
                isClockwise = true;
                return true;
            }

            if (tile.TileType == ModContent.TileType<ClockwisePriorityConveyorTile>())
            {
                isClockwise = true;
                return true;
            }

            if (tile.TileType == ModContent.TileType<CounterClockwisePriorityConveyorTile>())
            {
                isClockwise = false;
                return true;
            }

            return false;
        }

        // DEBUG

        public static void DrawItemDebug(SpriteBatch spriteBatch, Content.VirtualItems.VirtualItem item)
        {
            if (item == null || item.active == false)
            {
                return;
            }

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // --- 1. TILE HIGHLIGHTS ---
            // Current Tile (Cyan)
            Vector2 currentTileScreen = new Vector2(item.currentTileX * 16, item.currentTileY * 16) - Main.screenPosition;
            Rectangle currentTileRect = new Rectangle((int)currentTileScreen.X, (int)currentTileScreen.Y, 16, 16);
            DrawBorderRectangle(spriteBatch, pixel, currentTileRect, Color.Cyan * 0.8f, 2);

            // Target Tile (Yellow)
            Vector2 targetTileScreen = new Vector2(item.targetTileX * 16, item.targetTileY * 16) - Main.screenPosition;
            Rectangle targetTileRect = new Rectangle((int)targetTileScreen.X, (int)targetTileScreen.Y, 16, 16);
            DrawBorderRectangle(spriteBatch, pixel, targetTileRect, Color.Yellow * 0.8f, 2);

            // --- 2. TRAJECTORY & WORLD POSITION ---
            Vector2 itemScreenPos = item.worldPosition - Main.screenPosition;
            Vector2 targetCenterScreen = targetTileScreen + new Vector2(8, 8);

            // Movement Trajectory Line (Orange)
            DrawLine(spriteBatch, pixel, itemScreenPos, targetCenterScreen, Color.Orange, 2f);

            // Pixel Position Center Crosshair (Red)
            Rectangle pointRect = new Rectangle((int)itemScreenPos.X - 2, (int)itemScreenPos.Y - 2, 4, 4);
            spriteBatch.Draw(pixel, pointRect, Color.Red);

            // --- 3. 3x3 NEIGHBORHOOD CONVEYOR SCAN ---
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int scanX = item.currentTileX + offsetX;
                    int scanY = item.currentTileY + offsetY;

                    if (VirtualItemSystem.IsConveyorTile(scanX, scanY, out _) == true)
                    {
                        Vector2 scanScreen = new Vector2(scanX * 16, scanY * 16) - Main.screenPosition;
                        Rectangle scanRect = new Rectangle((int)scanScreen.X, (int)scanScreen.Y, 16, 16);

                        // Fill scanned conveyor tiles with faint Purple
                        spriteBatch.Draw(pixel, scanRect, Color.Purple * 0.25f);
                    }
                }
            }

            // --- 4. ON-SCREEN TEXT READOUT ---
            float distToTarget = Vector2.Distance(item.worldPosition, targetTileScreen + Main.screenPosition + new Vector2(8, 8));
            bool isIdle = (item.currentTileX == item.targetTileX && item.currentTileY == item.targetTileY);

            string debugText = "";
            //string debugText =
            //    $"[ID: {item.itemType} x{item.stackSize}]\n" +
            //    $"State: {(isIdle ? "IDLE / SCAN" : "MOVING")}\n" +
            //    $"Current: ({item.currentTileX}, {item.currentTileY})\n" +
            //    $"Target:  ({item.targetTileX}, {item.targetTileY})\n" +
            //    $"WorldPos: ({item.worldPosition.X:F1}, {item.worldPosition.Y:F1})\n" +
            //    $"DistRemaining: {distToTarget:F2}px\n" +
            //    $"PickupCD: {item.pickupCooldown}";

            Vector2 textPos = itemScreenPos + new Vector2(-40, -90);

            // Text shadow and main text
            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch,
                FontAssets.MouseText.Value,
                debugText,
                textPos,
                Color.LimeGreen,
                0f,
                Vector2.Zero,
                new Vector2(0.7f, 0.7f)
            );
        }

        // --- DRAWING HELPERS ---
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

        private static void DrawBorderRectangle(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
        {
            // Top, Bottom, Left, Right
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            sb.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }
    }

}