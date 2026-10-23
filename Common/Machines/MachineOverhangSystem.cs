using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Factorraria.Common.Liquids;
using System;
using System.Collections.Generic;
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
        public override void Load()
        {
            if (Main.dedServ) return; // no screen on a dedicated server: the draw detour is never needed there
            On_TileObject.DrawPreview += HideVanillaPreview;
        }

        public override void Unload()
        {
            if (Main.dedServ) return; // matches Load()
            On_TileObject.DrawPreview -= HideVanillaPreview;
        }

        private static void HideVanillaPreview(On_TileObject.orig_DrawPreview orig, SpriteBatch sb, TileObjectPreviewData op, Vector2 position)
        {
            if (MachineVisualRegistry.Definitions.TryGetValue(op.Type, out var def) && def.Canvas != null)
                return;

            orig(sb, op, position);
        }

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

                bool working = machine.isOn && machine.isWorking;
                Texture2D texture;
                int frame;
                MachineTransition transition;
                bool drawLiquid = true;
                if (!def.TryGetActiveTransition(machine, out texture, out frame, out transition))
                {
                    texture = working ? def.OnTexture.Value : def.OffTexture.Value;
                    frame = TileEntityHelper.GetAnimationFrame(corner, canvas.FrameCount(working));
                    drawLiquid = working;   // the On liquid layers only draw while running
                }
                int canvasWidth = canvas.CanvasWidth * 16;
                int canvasHeight = canvas.CanvasHeight * 16;

                Vector2 worldTopLeft = new Vector2(corner.X * 16 - canvas.OriginX * 16f, corner.Y * 16 - canvas.OriginY * 16f + canvas.YOffset);
                Vector2 canvasCenter = worldTopLeft + new Vector2(canvasWidth, canvasHeight) / 2f;
                if (!BaseMachine.IsOnScreen(canvasCenter, Math.Max(canvasWidth, canvasHeight) / 2f))
                    continue;

                int frameY = frame * canvas.FrameStride;
                Color color = Lighting.GetColor(corner.X + data.Width / 2, corner.Y + data.Height / 2);

                if (!batchBegun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    batchBegun = true;
                }

                DrawCanvasCells(spriteBatch, texture, canvas, worldTopLeft, frameY, color);
                if (drawLiquid)
                    DrawCanvasLiquidOverlays(spriteBatch, def.LiquidOverlays, transition, machine, canvas, worldTopLeft, frameY, color);
            }

            if (TryGetHologram(out MachineVisualDefinition holoDef, out Vector2 holoTopLeft, out bool holoValid))
            {
                if (!batchBegun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    batchBegun = true;
                }

                Color tint = holoValid ? Color.White * 0.55f : new Color(255, 70, 70) * 0.6f;
                DrawCanvasCells(spriteBatch, holoDef.OffTexture.Value, holoDef.Canvas, holoTopLeft, 0, tint);
            }

            if (batchBegun)
                spriteBatch.End();
        }

        // Recolored liquid layers (same LiquidTextureCache as normal machines) drawn over a canvas frame. kind picks each layer's On /
        // startup / shutdown texture (a layer without that one is skipped); it has to share the frame layout of the canvas texture it goes with.
        private static void DrawCanvasLiquidOverlays(SpriteBatch spriteBatch, List<LiquidOverlayLayer> layers, MachineTransition kind, BaseMachine machine, MachineCanvas canvas, Vector2 worldTopLeft, int frameY, Color color)
        {
            foreach (LiquidOverlayLayer layer in layers)
            {
                if (!layer.TryGetTexture(kind, out Texture2D source, out string cacheKey))
                    continue;

                LiquidStack stack = layer.GetLiquidStack(machine);
                if (stack == null || stack.IsEmpty)
                    continue;

                LiquidTypeDefinition liquidDef = LiquidTypeRegistry.Get(stack.LiquidType);
                Texture2D recolored = LiquidTextureCache.GetOrCreate(cacheKey, source, liquidDef);
                DrawCanvasCells(spriteBatch, recolored, canvas, worldTopLeft, frameY, color);
            }
        }

        // One 16x16 source cell per tile of the canvas, so the padding between cells in the texture is never drawn.
        private static void DrawCanvasCells(SpriteBatch spriteBatch, Texture2D texture, MachineCanvas canvas, Vector2 worldTopLeft, int frameY, Color color)
        {
            for (int cx = 0; cx < canvas.CanvasWidth; cx++)
            {
                for (int cy = 0; cy < canvas.CanvasHeight; cy++)
                {
                    Rectangle source = new Rectangle(cx * canvas.CellSize, frameY + cy * canvas.CellSize, 16, 16);
                    spriteBatch.Draw(texture, worldTopLeft + new Vector2(cx * 16, cy * 16) - Main.screenPosition, source, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                }
            }
        }

        // Placement preview while the held item places an overhang machine: the whole sprite (overhang included) faded,
        // or all red when it can't go there. Vanilla's own preview only draws the footprint, so it is skipped for these
        // machines (HideVanillaPreview) and this replaces it.
        private static bool TryGetHologram(out MachineVisualDefinition def, out Vector2 topLeft, out bool valid)
        {
            def = null;
            topLeft = Vector2.Zero;
            valid = false;

            if (Main.gameMenu)
                return false;

            Player player = Main.LocalPlayer;
            Item held = player.HeldItem;
            if (player.dead || player.mouseInterface || held.IsAir || held.createTile < 0)
                return false;

            if (!MachineVisualRegistry.Definitions.TryGetValue(held.createTile, out def) || def.Canvas == null)
                return false;

            TileObjectData data = TileObjectData.GetTileData(held.createTile, held.placeStyle);
            if (data == null || !def.Canvas.Resolve(def.OnTexture.Value, def.OffTexture.Value))
                return false;

            MachineCanvas canvas = def.Canvas;
            int cornerX = Player.tileTargetX - data.Origin.X;
            int cornerY = Player.tileTargetY - data.Origin.Y;
            topLeft = new Vector2(cornerX * 16 - canvas.OriginX * 16f, cornerY * 16 - canvas.OriginY * 16f + canvas.YOffset);

            valid = player.IsTargetTileInItemRange(held)
                && TileObject.CanPlace(Player.tileTargetX, Player.tileTargetY, held.createTile, held.placeStyle, player.direction, out _, true);
            return true;
        }
    }
}