using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace Factorraria.Common.Systems
{
    // The tile rectangle that is on screen (zoom-aware) plus a padding, clamped to the world. Shared by every system that scans the visible
    // area in PostDrawTiles instead of walking TileEntity.ByPosition (OverhangDrawSystem, TileOverdrawSystem).
    public static class VisibleTileScan
    {
        public static void GetRange(int paddingTiles, out int left, out int right, out int top, out int bottom)
        {
            float zoom = Main.GameViewMatrix.Zoom.X;
            if (zoom < 0.01f)
                zoom = 1f;
            float centerX = Main.screenPosition.X + Main.screenWidth * 0.5f;
            float centerY = Main.screenPosition.Y + Main.screenHeight * 0.5f;
            float halfWidth = Main.screenWidth / zoom * 0.5f;
            float halfHeight = Main.screenHeight / zoom * 0.5f;
            left = Math.Max(0, (int)((centerX - halfWidth) / 16f) - paddingTiles);
            right = Math.Min(Main.maxTilesX - 1, (int)((centerX + halfWidth) / 16f) + paddingTiles);
            top = Math.Max(0, (int)((centerY - halfHeight) / 16f) - paddingTiles);
            bottom = Math.Min(Main.maxTilesY - 1, (int)((centerY + halfHeight) / 16f) + paddingTiles);
        }
    }
}