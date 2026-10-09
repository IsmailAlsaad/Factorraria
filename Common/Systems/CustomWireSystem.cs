using Factorraria.Content.Items.Wires;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Systems
{
    [Flags]
    public enum CustomWireType : byte
    {
        None = 0,        // 0000 0000
        Copper = 1 << 0, // 0000 0001
        Tin = 1 << 1     // 0000 0010
    }

    public class CustomWireSystem : ModSystem
    {
        public static readonly CustomWireType[] AllWireTypes = new CustomWireType[]
        {
            CustomWireType.Copper,
            CustomWireType.Tin
        };

        Asset<Texture2D> CopperWireTileTexture;
        Asset<Texture2D> TinWireTileTexture;

        public static Dictionary<Point16,CustomWireType> WireGrid = new Dictionary<Point16,CustomWireType>();

        // Sprite frame (0-15, the 4 neighbour bits) of every wire tile, per wire type. Kept in step with WireGrid by
        // AddWire / RemoveWire / LoadWorldData, so drawing never has to look at neighbours.
        private static readonly Dictionary<Point16, (byte Copper, byte Tin)> frameCache = new Dictionary<Point16, (byte Copper, byte Tin)>();

        private static void RefreshFrame(Point16 position)
        {
            if (!WireGrid.TryGetValue(position, out CustomWireType wireType))
            {
                frameCache.Remove(position);
                return;
            }

            byte copper = wireType.HasFlag(CustomWireType.Copper) ? (byte)GetFrameIndexFromNeighbors(position.X, position.Y, CustomWireType.Copper) : (byte)0;
            byte tin = wireType.HasFlag(CustomWireType.Tin) ? (byte)GetFrameIndexFromNeighbors(position.X, position.Y, CustomWireType.Tin) : (byte)0;
            frameCache[position] = (copper, tin);
        }

        /// <summary>A wire changed at (x, y): its own frame and its 4 neighbours' frames may change.</summary>
        private static void RefreshFramesAround(int x, int y)
        {
            RefreshFrame(new Point16(x, y));
            RefreshFrame(new Point16(x, y - 1));
            RefreshFrame(new Point16(x + 1, y));
            RefreshFrame(new Point16(x, y + 1));
            RefreshFrame(new Point16(x - 1, y));
        }

        private static void RebuildAllFrames()
        {
            frameCache.Clear();

            foreach (Point16 position in WireGrid.Keys)
            {
                RefreshFrame(position);
            }
        }

        /// <summary>The tile rectangle the camera can currently see (a little generous), zoom included.</summary>
        private static void GetVisibleTileRect(out int minX, out int minY, out int maxX, out int maxY)
        {
            // The zoom matrix scales around the screen centre: zoomed in shows less than the screen, zoomed out more.
            float zoom = Math.Max(0.1f, Math.Min(1f, Main.GameViewMatrix.ZoomMatrix.M11));
            float halfWidth = Main.screenWidth / 2f / zoom;
            float halfHeight = Main.screenHeight / 2f / zoom;
            float centerX = Main.screenPosition.X + Main.screenWidth / 2f;
            float centerY = Main.screenPosition.Y + Main.screenHeight / 2f;

            minX = (int)((centerX - halfWidth) / 16f) - 2;
            maxX = (int)((centerX + halfWidth) / 16f) + 2;
            minY = (int)((centerY - halfHeight) / 16f) - 2;
            maxY = (int)((centerY + halfHeight) / 16f) + 2;
        }

        private void DrawWireTile(Point16 position, CustomWireType wireType)
        {
            if (!frameCache.TryGetValue(position, out (byte Copper, byte Tin) frames))
            {
                RefreshFrame(position); // should not happen, but never draw a wrong frame
                frameCache.TryGetValue(position, out frames);
            }

            Vector2 drawPosition = position.ToVector2() * 16f - Main.screenPosition;
            Color lighting = Lighting.GetColor(position.X, position.Y);

            if (wireType.HasFlag(CustomWireType.Copper))
            {
                Main.spriteBatch.Draw(CopperWireTileTexture.Value, drawPosition, new Rectangle(frames.Copper * 18, 0, 16, 16), lighting);
            }

            if (wireType.HasFlag(CustomWireType.Tin))
            {
                Main.spriteBatch.Draw(TinWireTileTexture.Value, drawPosition, new Rectangle(frames.Tin * 18, 0, 16, 16), lighting);
            }
        }

        public override void SaveWorldData(TagCompound tag)
        {
            List<Point16> positions = new List<Point16>();
            List<byte> types = new List<byte>();

            foreach (var (pose, wireType) in WireGrid)
            {
                positions.Add(pose);
                types.Add((byte)wireType);
            }

            tag["WirePositions"] = positions;
            tag["WireTypes"] = types;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            WireGrid.Clear();
            frameCache.Clear();

            if(!tag.ContainsKey("WirePositions") || !tag.ContainsKey("WireTypes"))
            {
                return;
            }

            List<Point16> positions = tag.Get<List<Point16>>("WirePositions");
            List<byte> types = tag.Get<List<byte>>("WireTypes");

            for (int i = 0; i < positions.Count; i++)
            {
                WireGrid.Add(positions[i], (CustomWireType)types[i]);
            }

            RebuildAllFrames();
        }

        public override void Load()
        {
            if (Main.dedServ)
            {
                return;
            }

            CopperWireTileTexture = ModContent.Request<Texture2D>("Factorraria/Content/Items/Wires/CopperWireTile");
            TinWireTileTexture = ModContent.Request<Texture2D>("Factorraria/Content/Items/Wires/TinWireTile");
        }

        public override void Unload()
        {
            WireGrid.Clear();
            frameCache.Clear();
        }

        public override void OnWorldUnload()
        {
            // Wires of the world that was just left must not leak into the next one.
            WireGrid.Clear();
            frameCache.Clear();
        }

        public static bool HasWire(int x, int y, CustomWireType wireType)
        {
            Point16 position = new Point16(x, y);
            if(WireGrid.TryGetValue(position, out CustomWireType type))
            {
               return type.HasFlag(wireType);
            }

            return false;
        }

        public static void AddWire(int x,int y, CustomWireType wireType) 
        {
            Point16 position = new Point16(x, y);
            if (!WireGrid.ContainsKey(position))
            {
                WireGrid.Add(position, CustomWireType.None);
            }

            WireGrid[position] |= wireType;
            RefreshFramesAround(x, y);

            PowerGridSystem.gridNeedsRebuilding = true;
        }

        public static bool RemoveWire(int x,int y, CustomWireType wireType)
        {
            Point16 position = new Point16(x, y);
            if (WireGrid.TryGetValue(position, out CustomWireType existingWire) && existingWire.HasFlag(wireType))
            {
                existingWire &= ~wireType;

                if(existingWire == CustomWireType.None)
                {
                    WireGrid.Remove(position);
                }
                else
                {
                    WireGrid[position] = existingWire;
                }

                RefreshFramesAround(x, y);
                PowerGridSystem.gridNeedsRebuilding = true;

                return true;
            }

            return false;
        }

        public override void PostDrawTiles()
        {
            if (WireGrid.Count == 0) 
            {
                return;
            }

            if (!PlayerCanSeeWires())
            {
                return;
            }

            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.GameViewMatrix.ZoomMatrix
            );

            GetVisibleTileRect(out int minX, out int minY, out int maxX, out int maxY);
            long visibleTiles = (long)(maxX - minX + 1) * (maxY - minY + 1);

            if (WireGrid.Count <= visibleTiles)
            {
                // Fewer wires than screen tiles: walk the wires and skip the off-screen ones.
                foreach (var pair in WireGrid)
                {
                    Point16 position = pair.Key;

                    if (position.X < minX || position.X > maxX || position.Y < minY || position.Y > maxY)
                    {
                        continue;
                    }

                    DrawWireTile(position, pair.Value);
                }
            }
            else
            {
                // More wires than screen tiles: look up just the tiles on screen.
                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        Point16 position = new Point16(x, y);

                        if (WireGrid.TryGetValue(position, out CustomWireType wireType))
                        {
                            DrawWireTile(position, wireType);
                        }
                    }
                }
            }

            Main.spriteBatch.End();
        }

        bool PlayerCanSeeWires()
        {
            int heldItemType = Main.LocalPlayer.HeldItem.type;

            if (heldItemType == ModContent.ItemType<LeadCutter>())
            {
                return true;
            }
            if (heldItemType == ModContent.ItemType<IronCutter>())
            {
                return true;
            }
            if (heldItemType == ModContent.ItemType<TinWire>())
            {
                return true;
            }
            if (heldItemType == ModContent.ItemType<CopperWire>())
            {
                return true;
            }

            return false;
        }

        static int GetFrameIndexFromNeighbors(int x,int y,CustomWireType wireType)
        {
            Point16 position = new Point16(x, y);
            int frameIndex = 0;

            if (WireGrid.TryGetValue(position + new Point16(0, -1), out CustomWireType neighborWireU) && neighborWireU.HasFlag(wireType)) { frameIndex += 1; }
            if (WireGrid.TryGetValue(position + new Point16(1, 0), out CustomWireType neighborWireR) && neighborWireR.HasFlag(wireType)) { frameIndex += 2; }
            if (WireGrid.TryGetValue(position + new Point16(0, 1), out CustomWireType neighborWireD) && neighborWireD.HasFlag(wireType)) { frameIndex += 4; }
            if (WireGrid.TryGetValue(position + new Point16(-1, 0), out CustomWireType neighborWireL) && neighborWireL.HasFlag(wireType)) { frameIndex += 8; }

            return frameIndex;
        }
    }
}
