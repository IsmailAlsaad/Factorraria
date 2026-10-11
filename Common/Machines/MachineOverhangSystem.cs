using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Machines
{
    // Draws the canvas (On or Off texture) of every machine registered with MachineVisualRegistry.RegisterOverhang once per machine,
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
                if (entity is not BaseMachine machine)
                    continue;

                Point16 corner = machine.Position;
                Tile tile = Framing.GetTileSafely(corner.X, corner.Y);
                if (!tile.HasTile || !MachineVisualRegistry.Definitions.TryGetValue(tile.TileType, out var def) || def.Canvas == null)
                    continue;

                TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);
                if (data == null)
                    continue;

                MachineCanvas canvas = def.Canvas;
                bool wasResolved = canvas.Resolved;
                if (!canvas.Resolve(def.OnTexture.Value, def.OffTexture.Value))
                {
                    if (!wasResolved)
                        Mod.Logger.Warn($"Overhang canvas for tile {tile.TileType} is invalid: {canvas.Error}");
                    continue;
                }

                if (canvas.FootprintWidth != data.Width || canvas.FootprintHeight != data.Height)
                {
                    if (!canvas.Resolved || canvas.Error == null)
                    {
                        canvas.Error = $"registered footprint {canvas.FootprintWidth}x{canvas.FootprintHeight} but the tile is {data.Width}x{data.Height}.";
                        Mod.Logger.Warn($"Overhang canvas for tile {tile.TileType} is invalid: {canvas.Error}");
                    }
                    continue;
                }

                Texture2D texture = machine.isOn && machine.isWorking ? def.OnTexture.Value : def.OffTexture.Value;
                int canvasWidth = canvas.CanvasWidth * 16;
                int canvasHeight = canvas.CanvasHeight * 16;

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