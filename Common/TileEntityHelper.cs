using Factorraria.Common.Systems;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Factorraria.Common
{
    public static class TileEntityHelper
    {
        public static bool TryGetEntityFromTile(int i,int j, out TileEntity entity, out Point16 cornerPosition)
        {
            entity = null;
            cornerPosition = new Point16(i,j);

            Tile tile = Main.tile[i, j];

            if (!tile.HasTile)
            {
                return false;
            }

            Point16 position = new Point16(i, j);
            if(TileEntity.ByPosition.TryGetValue(position, out entity))
            {
                cornerPosition = position;
                return true;
            }

            TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);
            if(data == null)
            {
                return false;
            }

            int cellWidth = data.CoordinateWidth + data.CoordinatePadding;
            int subX = (tile.TileFrameX % data.CoordinateFullWidth) / cellWidth;

            // Calculate local Y offset (Row) by matching frame Y against variable row heights
            int frameY = tile.TileFrameY % data.CoordinateFullHeight;
            int subY = 0;
            int accumulatedHeight = 0;

            for (int k = 0; k < data.Height; k++)
            {
                int rowHeight = data.CoordinateHeights[k] + data.CoordinatePadding;
                if (frameY < accumulatedHeight + rowHeight)
                {
                    subY = k;
                    break;
                }
                accumulatedHeight += rowHeight;
            }

            int originX = i - subX;
            int originY = j - subY;

            Point16 corner = new Point16(originX, originY);
            cornerPosition = corner;
            return TileEntity.ByPosition.TryGetValue(corner, out entity);
        }

        public static T GetOrCreateEntity<T>(int i,int j) where T : ModTileEntity 
        {
            if(TryGetEntityFromTile(i , j, out TileEntity existingEntity, out _))
            {
                if (existingEntity is T tileEntity) 
                {
                    return tileEntity;
                }
            }

            T instance = ModContent.GetInstance<T>();

            int placedID = instance.Place(i, j);
            if (placedID != -1 && TileEntity.ByID.TryGetValue(placedID, out TileEntity newEntity))
            {
                if(newEntity is T tileEntity)
                {
                    PowerGridSystem.RegisterMachineToMasterList(tileEntity);

                    return tileEntity;
                }
            }

            return null;
        }

        public static void AnimateTileEntity(SpriteBatch spriteBatch, Texture2D targetTexture, int i,int j)
        {
            Tile thisTile = Framing.GetTileSafely(i, j);
            TileObjectData data = TileObjectData.GetTileData(thisTile.TileType, 0);
            if (data == null)
            {
                //
                Main.NewText("TileEntityheler is facing issues lmao");
                //
                return;
            }

            int frameY = thisTile.TileFrameY % data.CoordinateFullHeight;
            int subY = -1;
            int frameHeight = 0;

            for (int k = 0; k < data.Height; k++)
            {
                int rowHeight = data.CoordinateHeights[k] + data.CoordinatePadding;
                if (frameY < frameHeight + rowHeight && subY == -1)
                {
                    subY = k;
                }
                frameHeight += rowHeight;
            }

            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange);
            Vector2 drawPosition = new Vector2(i, j) * 16 - Main.screenPosition + offset + new Vector2(0, 2);
            int frameCount = targetTexture.Height / frameHeight;

            //
            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            int animationSpeed = config.animationSpeedDivider;
            //

            TryGetEntityFromTile(i, j, out _, out Point16 topLeft);
            int frameSpatialOffset = 0;
            int frameYOffset = 0;

            if (frameCount != 1)
            { 
                frameSpatialOffset = (topLeft.X * 3) + (topLeft.Y * 7);
                frameYOffset = (int)((Main.GameUpdateCount / (long)animationSpeed + (long)frameSpatialOffset) % frameCount);
            }

            Rectangle textureSlice = new Rectangle(thisTile.TileFrameX, thisTile.TileFrameY + frameHeight * frameYOffset, 16, data.CoordinateHeights[subY]);
            Color lightingColor = Lighting.GetColor(i, j);
            spriteBatch.Draw(targetTexture, drawPosition, textureSlice, lightingColor);
        }
    }
}
