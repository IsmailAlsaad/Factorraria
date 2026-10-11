using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Machines
{
    // Draws the On canvas of every machine registered with MachineVisualRegistry.RegisterOverhang once per machine,
    // after all tiles are drawn, so the art can spill past the footprint without neighbouring tiles painting over it.
    public class MachineOverhangSystem : ModSystem
    {
        public override void PostDrawTiles()
        {
            if (Main.dedServ || MachineVisualRegistry.Definitions.Count == 0)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool batchBegun = false;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not BaseMachine machine || !machine.isOn || !machine.isWorking)
                    continue;

                Point16 corner = machine.Position;
                Tile tile = Framing.GetTileSafely(corner.X, corner.Y);
                if (!tile.HasTile || !MachineVisualRegistry.Definitions.TryGetValue(tile.TileType, out var def) || def.OnCanvas == null)
                    continue;

                TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);
                if (data == null)
                    continue;

                MachineOnCanvas canvas = def.OnCanvas;
                Texture2D texture = def.OnTexture.Value;
                int canvasWidth = canvas.CanvasWidth * 16;
                int canvasHeight = canvas.CanvasHeight * 16;

                if (texture.Width != canvasWidth || texture.Height < canvasHeight || texture.Height % canvasHeight != 0
                    || canvas.FootprintWidth != data.Width || canvas.FootprintHeight != data.Height)
                {
                    if (!canvas.Warned)
                    {
                        canvas.Warned = true;
                        Mod.Logger.Warn($"On texture {texture.Name} is {texture.Width}x{texture.Height}, expected {canvasWidth} wide and a multiple of {canvasHeight} tall " +
                            $"({canvas.CanvasWidth}x{canvas.CanvasHeight} tile canvas, frames stacked), footprint {canvas.FootprintWidth}x{canvas.FootprintHeight} (tile is {data.Width}x{data.Height}).");
                    }
                    continue;
                }

                Vector2 worldTopLeft = new Vector2(corner.X * 16 - canvas.OriginX * 16f, corner.Y * 16 - canvas.OriginY * 16f);
                Vector2 canvasCenter = worldTopLeft + new Vector2(canvasWidth, canvasHeight) / 2f;
                if (!BaseMachine.IsOnScreen(canvasCenter, Math.Max(canvasWidth, canvasHeight) / 2f))
                    continue;

                int frame = TileEntityHelper.GetAnimationFrame(corner, canvas.FrameCount(texture));
                Rectangle source = new Rectangle(0, frame * canvasHeight, canvasWidth, canvasHeight);
                Color color = Lighting.GetColor(corner.X + data.Width / 2, corner.Y + data.Height / 2);

                if (!batchBegun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    batchBegun = true;
                }

                spriteBatch.Draw(texture, worldTopLeft - Main.screenPosition, source, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            if (batchBegun)
                spriteBatch.End();
        }
    }
}