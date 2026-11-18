using Factorraria.Common.Carts;
using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Machines
{
    // Smart cursor outlines for right-clickable machines (client-only, purely visual: nothing is synced).
    // While Main.SmartCursorIsUsed, every registered machine the local player can right-click right now (same reach rule as
    // MachineVisualOverride.RightClick) gets a 1px outline just outside its sprite: white = can be used, gold = the cursor is over it.
    // The outline is computed from the pixels that are actually drawn (same texture / frame choice as MachineVisualOverride.PreDraw and
    // MachineOverhangSystem), cached per machine, and drawn after the tiles so it sits on top of the machine.
    // Not outlined: the moving parts drawn by IMachineDynamicDraw (arm, wheel, ...) and the liquid layers; only the base sprite.
    public class MachineSmartOutlineSystem : ModSystem
    {
        // Instance fields (not const/static) so they can be tweaked with hot reload.
        public Color HighlightColor = Color.White;
        public Color SelectedColor = new Color(255, 200, 40);
        public int AlphaThreshold = 8;          // a sprite pixel counts as solid above this alpha
        public int MaxCachedOutlines = 512;
        public float ScreenMarginPixels = 192f; // extra cull margin so overhang art still counts as on screen

        struct SpriteCell
        {
            public Texture2D Texture;
            public Rectangle Source;
            public int X;        // world pixel of the cell's top-left corner
            public int Y;
            public int Quarter;  // clockwise quarter turns (motors), 0 for everything else
        }

        class Outline
        {
            public int OriginX;
            public int OriginY;
            public List<Rectangle> Runs = new List<Rectangle>(); // 1px high runs, relative to the origin
        }

        class CachedOutline
        {
            public ulong Signature;
            public Outline Data;
        }

        readonly Dictionary<Point16, CachedOutline> outlineCache = new Dictionary<Point16, CachedOutline>();
        readonly Dictionary<Texture2D, byte[]> alphaCache = new Dictionary<Texture2D, byte[]>();
        readonly List<SpriteCell> scratchCells = new List<SpriteCell>();

        public override void OnWorldUnload()
        {
            outlineCache.Clear();
        }

        public override void Unload()
        {
            outlineCache.Clear();
            alphaCache.Clear();
            scratchCells.Clear();
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu || !Main.SmartCursorIsUsed || MachineVisualRegistry.Definitions.Count == 0)
                return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead)
                return;

            Vector2 mouse = CartSystem.GetZoomCorrectedMouseWorld();
            int mouseTileX = (int)Math.Floor(mouse.X / 16f);
            int mouseTileY = (int)Math.Floor(mouse.Y / 16f);
            bool cursorFree = !player.mouseInterface;
            bool selectedTaken = false;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool batchBegun = false;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not BaseMachine machine)
                    continue;

                Point16 corner = machine.Position;
                Tile tile = Framing.GetTileSafely(corner.X, corner.Y);
                if (!tile.HasTile || !MachineVisualRegistry.Definitions.TryGetValue(tile.TileType, out var def))
                    continue;

                TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);
                if (data == null)
                    continue;

                Vector2 center = new Vector2(corner.X * 16 + data.Width * 8, corner.Y * 16 + data.Height * 8);
                if (!BaseMachine.IsOnScreen(center, Math.Max(data.Width, data.Height) * 8f + ScreenMarginPixels))
                    continue;

                // Same reach rule the right-click uses: if the click would be ignored, there is nothing to highlight.
                if (!MachineDetailSystem.WithinReach(player, machine, 0f))
                    continue;

                Outline outline = GetOutline(machine, tile, def, data);
                if (outline == null || outline.Runs.Count == 0)
                    continue;

                bool hovered = cursorFree && !selectedTaken
                    && mouseTileX >= corner.X && mouseTileX < corner.X + data.Width
                    && mouseTileY >= corner.Y && mouseTileY < corner.Y + data.Height;
                if (hovered)
                    selectedTaken = true;

                if (!batchBegun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    batchBegun = true;
                }

                DrawOutline(spriteBatch, outline, hovered ? SelectedColor : HighlightColor);
            }

            if (batchBegun)
                spriteBatch.End();
        }

        // MagicPixel is probably not 1x1, so the source is always an explicit 1x1 rectangle.
        static void DrawOutline(SpriteBatch spriteBatch, Outline outline, Color color)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new Rectangle(0, 0, 1, 1);
            foreach (Rectangle run in outline.Runs)
            {
                Vector2 position = new Vector2(outline.OriginX + run.X, outline.OriginY + run.Y) - Main.screenPosition;
                spriteBatch.Draw(pixel, position, source, color, 0f, Vector2.Zero, new Vector2(run.Width, 1f), SpriteEffects.None, 0f);
            }
        }

        Outline GetOutline(BaseMachine machine, Tile tile, MachineVisualDefinition def, TileObjectData data)
        {
            scratchCells.Clear();
            if (!CollectCells(machine, tile, def, data, scratchCells) || scratchCells.Count == 0)
                return null;

            ulong signature = 14695981039346656037UL;
            signature = Mix(signature, tile.TileType);
            signature = Mix(signature, AlphaThreshold);
            foreach (SpriteCell cell in scratchCells)
            {
                signature = Mix(signature, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(cell.Texture));
                signature = Mix(signature, cell.Source.X);
                signature = Mix(signature, cell.Source.Y);
                signature = Mix(signature, cell.Source.Width);
                signature = Mix(signature, cell.Source.Height);
                signature = Mix(signature, cell.X);
                signature = Mix(signature, cell.Y);
                signature = Mix(signature, cell.Quarter);
            }

            if (outlineCache.TryGetValue(machine.Position, out CachedOutline cached) && cached.Signature == signature)
                return cached.Data;

            if (outlineCache.Count >= MaxCachedOutlines)
                outlineCache.Clear();

            Outline built = BuildOutline(scratchCells);
            outlineCache[machine.Position] = new CachedOutline { Signature = signature, Data = built };
            return built;
        }

        static ulong Mix(ulong hash, int value)
        {
            hash ^= (uint)value;
            hash *= 1099511628211UL;
            return hash;
        }

        // Works out which sprite cells the machine is drawn with right now. Mirrors MachineVisualOverride.PreDraw (per-tile slices),
        // MachineOverhangSystem.DrawPass (canvas machines) and the plain vanilla tile for DrawNormalTile machines.
        bool CollectCells(BaseMachine machine, Tile tile, MachineVisualDefinition def, TileObjectData data, List<SpriteCell> cells)
        {
            Point16 corner = machine.Position;

            if (def.Canvas != null && !def.DrawNormalTile)
            {
                MachineCanvas canvas = def.Canvas;
                if (!canvas.Resolve(def.OnTexture.Value, def.OffTexture.Value)
                    || canvas.FootprintWidth != data.Width || canvas.FootprintHeight != data.Height)
                    return false;

                Texture2D texture;
                int frame;
                if (!def.TryGetActiveTransition(machine, out texture, out frame, out _))
                {
                    texture = def.GetCanvasTexture(machine, machine.IsRunning, out int stateFrames);
                    frame = TileEntityHelper.GetAnimationFrame(corner, stateFrames);
                }

                if (texture == null)
                    return false;

                int baseX = (int)Math.Round(corner.X * 16 - canvas.OriginX * 16f);
                int baseY = (int)Math.Round(corner.Y * 16 - canvas.OriginY * 16f + canvas.YOffset);
                int frameY = frame * canvas.FrameStride;
                int styleColumn = canvas.ClampStyle(machine.VisualStyle) * canvas.CanvasWidth;

                for (int cx = 0; cx < canvas.CanvasWidth; cx++)
                {
                    for (int cy = 0; cy < canvas.CanvasHeight; cy++)
                    {
                        Rectangle source = new Rectangle((styleColumn + cx) * canvas.CellSize, frameY + cy * canvas.CellSize, 16, 16);
                        if (!FitsTexture(texture, source))
                            continue;

                        cells.Add(new SpriteCell { Texture = texture, Source = source, X = baseX + cx * 16, Y = baseY + cy * 16 });
                    }
                }
                return true;
            }

            // Per-tile slices.
            Texture2D tileTexture = null;
            int forcedFrame = -1;
            bool plainTile = def.DrawNormalTile;
            float rotation = 0f;

            if (plainTile)
            {
                Main.instance.LoadTiles(tile.TileType);
                tileTexture = TextureAssets.Tile[tile.TileType].Value;
            }
            else if (def.TryGetActiveTransition(machine, out Texture2D transitionTexture, out int transitionFrame, out _))
            {
                tileTexture = transitionTexture;
                forcedFrame = transitionFrame;
            }
            else
            {
                tileTexture = machine.IsRunning ? def.GetOnAsset(machine)?.Value : def.OffTexture?.Value;
            }

            if (tileTexture == null)
                return false;

            if (!plainTile && machine is MotorTileEntityBase motor)
                rotation = MachineVisualOverride.GetMotorRotation(motor.Facing);
            int quarter = ((int)Math.Round(rotation / MathHelper.PiOver2) % 4 + 4) % 4;

            for (int x = corner.X; x < corner.X + data.Width; x++)
            {
                for (int y = corner.Y; y < corner.Y + data.Height; y++)
                {
                    Tile t = Framing.GetTileSafely(x, y);
                    if (!t.HasTile || t.TileType != tile.TileType)
                        continue;

                    // Same row maths as TileEntityHelper.AnimateTileEntity.
                    int frameY = t.TileFrameY % data.CoordinateFullHeight;
                    int subY = -1;
                    int frameHeight = 0;
                    for (int k = 0; k < data.Height; k++)
                    {
                        int rowHeight = data.CoordinateHeights[k] + data.CoordinatePadding;
                        if (frameY < frameHeight + rowHeight && subY == -1)
                            subY = k;
                        frameHeight += rowHeight;
                    }
                    if (subY == -1 || frameHeight <= 0)
                        continue;

                    int sliceHeight = data.CoordinateHeights[subY];
                    int sourceY = t.TileFrameY;
                    if (!plainTile)
                    {
                        int frameCount = Math.Max(1, tileTexture.Height / frameHeight);
                        int frame = forcedFrame >= 0 ? Math.Min(forcedFrame, frameCount - 1) : TileEntityHelper.GetAnimationFrame(corner, frameCount);
                        sourceY = frameY + frameHeight * frame;
                    }

                    Rectangle source = new Rectangle(t.TileFrameX, sourceY, 16, sliceHeight);
                    if (!FitsTexture(tileTexture, source))
                        continue;

                    cells.Add(new SpriteCell
                    {
                        Texture = tileTexture,
                        Source = source,
                        X = x * 16,
                        Y = y * 16 + data.DrawYOffset,
                        Quarter = quarter
                    });
                }
            }

            return true;
        }

        static bool FitsTexture(Texture2D texture, Rectangle source)
        {
            return texture != null && !texture.IsDisposed
                && source.X >= 0 && source.Y >= 0 && source.Right <= texture.Width && source.Bottom <= texture.Height;
        }

        // Alpha of every pixel of a texture, read from the GPU once and kept.
        byte[] GetAlpha(Texture2D texture)
        {
            if (texture == null || texture.IsDisposed)
                return null;

            if (alphaCache.TryGetValue(texture, out byte[] alpha))
                return alpha;

            Color[] pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
            alpha = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                alpha[i] = pixels[i].A;

            alphaCache[texture] = alpha;
            return alpha;
        }

        // Solid mask of all cells, then every empty pixel that touches a solid one (up, down, left, right) is an outline pixel.
        Outline BuildOutline(List<SpriteCell> cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (SpriteCell cell in cells)
            {
                minX = Math.Min(minX, cell.X);
                minY = Math.Min(minY, cell.Y);
                maxX = Math.Max(maxX, cell.X + cell.Source.Width);
                maxY = Math.Max(maxY, cell.Y + cell.Source.Height);
            }

            minX--; minY--; maxX++; maxY++;   // one pixel of room for the outline
            int width = maxX - minX;
            int height = maxY - minY;
            bool[] solid = new bool[width * height];

            foreach (SpriteCell cell in cells)
            {
                byte[] alpha = GetAlpha(cell.Texture);
                if (alpha == null)
                    continue;

                int textureWidth = cell.Texture.Width;
                int sw = cell.Source.Width;
                int sh = cell.Source.Height;
                int quarter = sw == sh ? cell.Quarter : 0;

                for (int sy = 0; sy < sh; sy++)
                {
                    int rowStart = (cell.Source.Y + sy) * textureWidth + cell.Source.X;
                    for (int sx = 0; sx < sw; sx++)
                    {
                        if (alpha[rowStart + sx] <= AlphaThreshold)
                            continue;

                        int dx, dy;
                        switch (quarter)
                        {
                            case 1: dx = sh - 1 - sy; dy = sx; break;
                            case 2: dx = sw - 1 - sx; dy = sh - 1 - sy; break;
                            case 3: dx = sy; dy = sw - 1 - sx; break;
                            default: dx = sx; dy = sy; break;
                        }

                        solid[(cell.Y + dy - minY) * width + (cell.X + dx - minX)] = true;
                    }
                }
            }

            Outline outline = new Outline { OriginX = minX, OriginY = minY };
            for (int y = 0; y < height; y++)
            {
                int runStart = -1;
                for (int x = 0; x <= width; x++)
                {
                    bool edge = false;
                    if (x < width)
                    {
                        int index = y * width + x;
                        edge = !solid[index]
                            && ((x > 0 && solid[index - 1])
                                || (x < width - 1 && solid[index + 1])
                                || (y > 0 && solid[index - width])
                                || (y < height - 1 && solid[index + width]));
                    }

                    if (edge)
                    {
                        if (runStart < 0)
                            runStart = x;
                    }
                    else if (runStart >= 0)
                    {
                        outline.Runs.Add(new Rectangle(runStart, y, x - runStart, 1));
                        runStart = -1;
                    }
                }
            }

            return outline;
        }
    }
}