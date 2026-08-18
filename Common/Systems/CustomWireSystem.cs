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

            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.GameViewMatrix.ZoomMatrix
            );

            foreach (var (pose, wireType) in WireGrid)
            {
                Vector2 drawPosition = pose.ToVector2() * 16f - Main.screenPosition;

                if (wireType.HasFlag(CustomWireType.Copper))
                {
                    int frameIndex = GetFrameIndexFromNeighbors(pose.X, pose.Y, CustomWireType.Copper);
                    Rectangle spriteSlice = new Rectangle(frameIndex * 18, 0, 16, 16);

                    Main.spriteBatch.Draw(CopperWireTileTexture.Value, drawPosition, spriteSlice, Color.White);

                    //
                    //Main.NewText("Placed Copper Wire");
                    //
                }

                if (wireType.HasFlag(CustomWireType.Tin))
                {
                    int frameIndex = GetFrameIndexFromNeighbors(pose.X, pose.Y, CustomWireType.Tin);
                    Rectangle spriteSlice = new Rectangle(frameIndex * 18, 0, 16, 16);

                    Main.spriteBatch.Draw(TinWireTileTexture.Value, drawPosition, spriteSlice, Color.White);

                    //
                    //Main.NewText("Placed Tin Wire");
                    //
                }
            }

            Main.spriteBatch.End();
        }

        int GetFrameIndexFromNeighbors(int x,int y,CustomWireType wireType)
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
