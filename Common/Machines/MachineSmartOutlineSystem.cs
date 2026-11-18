using Factorraria.Common.Carts;
using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Machines
{
    // Smart cursor outlines for right-clickable machines (client-only, purely visual: nothing is synced).
    // While Main.SmartCursorIsUsed, every registered machine the local player can right-click right now (same reach rule as
    // MachineVisualOverride.RightClick) gets an outline just outside its sprite: white = can be used, gold = the machine the smart cursor
    // is targeting (the one under the cursor, or else the nearest one within SmartRadiusPixels; right-click then opens that one).
    // The outline is computed from the pixels that are actually drawn (same texture / frame choice as MachineVisualOverride.PreDraw and
    // MachineOverhangSystem) on a grid of OutlineScale x OutlineScale world pixels (Terraria art pixels are 2x2), cached per machine, and
    // drawn in the behind-tiles pass (same hook as MachineOverhangSystem), so tiles cover it.
    // Not outlined: the moving parts drawn by IMachineDynamicDraw (arm, wheel, ...) and the liquid layers; only the base sprite.
    public class MachineSmartOutlineSystem : ModSystem
    {
        // TUNING: instance fields (not const/static) so they can be tweaked with hot reload.
        public Color HighlightColor = new Color(125,125,125);               // outline of a machine you can use (R, G, B, A)
        public Color SelectedColor = new Color(252, 252, 84);    // outline of the machine the smart cursor is targeting
        public bool OutlineInside = true;       // true: the outline is the sprite's own border pixels (like vanilla); false: an extra layer just outside the sprite
        public bool OutlineInFront = true;      // true: drawn over the tiles (PostDrawTiles); false: drawn under every tile (behind-tiles pass)
        public int OutlineScale = 2;            // size of one outline pixel in world pixels (Terraria art pixels are 2x2)
        public float SmartRadiusPixels = 60f * 16f;   // how far from the cursor a machine can be and still be targeted (16 = one tile)
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

        // The machine the smart cursor targets this tick (client only). Updated in PostUpdateInput, read by the draw pass.
        bool hasTarget;
        bool targetDirect;      // the cursor is directly over the machine's footprint
        Point16 targetPosition;

        public override void Load()
        {
            if (Main.dedServ) return; // no screen on a dedicated server
            On_Main.DrawNPCs += DrawBehindTilesPass;
            On_Player.TileInteractionsUse += RedirectClickToTarget;
        }

        public override void OnWorldUnload()
        {
            outlineCache.Clear();
            hasTarget = false;
        }

        public override void Unload()
        {
            if (!Main.dedServ)
            {
                On_Main.DrawNPCs -= DrawBehindTilesPass;
                On_Player.TileInteractionsUse -= RedirectClickToTarget;
            }

            outlineCache.Clear();
            alphaCache.Clear();
            scratchCells.Clear();
        }

        public override void PostUpdateInput()
        {
            HandleNearbyRightClick();
        }

        // The target is chosen at the end of the tick, after vanilla has chosen its own smart-interact target for this tick, so the two can be
        // compared by distance (see UpdateTarget). The click code and the draw pass read the result.
        public override void PostUpdateEverything()
        {
            UpdateTarget();
        }

        bool TryGetMachineInfo(BaseMachine machine, out Tile tile, out MachineVisualDefinition def, out TileObjectData data)
        {
            def = null;
            data = null;
            Point16 corner = machine.Position;
            tile = Framing.GetTileSafely(corner.X, corner.Y);
            if (!tile.HasTile || !MachineVisualRegistry.Definitions.TryGetValue(tile.TileType, out def))
                return false;

            data = TileObjectData.GetTileData(tile.TileType, 0);
            return data != null;
        }

        // Closest machine to the cursor (distance 0 = the cursor is over it) that is within SmartRadiusPixels and that the player can reach.
        void UpdateTarget()
        {
            hasTarget = false;
            targetDirect = false;

            if (Main.dedServ || Main.gameMenu || Main.mapFullscreen || !Main.SmartCursorIsUsed || MachineVisualRegistry.Definitions.Count == 0)
                return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead || player.mouseInterface)
                return;

            Vector2 mouse = CartSystem.GetZoomCorrectedMouseWorld();
            float bestDistance = SmartRadiusPixels;
            bool found = false;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not BaseMachine machine)
                    continue;

                if (!TryGetMachineInfo(machine, out _, out _, out TileObjectData data))
                    continue;

                Point16 corner = machine.Position;
                float left = corner.X * 16f;
                float top = corner.Y * 16f;
                float dx = Math.Max(Math.Max(left - mouse.X, 0f), mouse.X - (left + data.Width * 16f));
                float dy = Math.Max(Math.Max(top - mouse.Y, 0f), mouse.Y - (top + data.Height * 16f));
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                if (distance > bestDistance || (found && distance >= bestDistance))
                    continue;

                if (!MachineDetailSystem.WithinReach(player, machine, 0f))
                    continue;

                bestDistance = distance;
                targetPosition = corner;
                found = true;
            }

            if (found)
            {
                // Vanilla found its own target (chest, crystal ball, NPC...): the closer one to the cursor wins, a tie goes to vanilla.
                if (GetVanillaTargetDistance(mouse) <= bestDistance)
                {
                    found = false;
                }
                else if (Main.SmartInteractShowingGenuine)
                {
                    // The machine wins: hide vanilla's own highlight so only one target is shown.
                    Main.SmartInteractShowingGenuine = false;
                    Main.SmartInteractTileCoords.Clear();
                }
            }

            hasTarget = found;
            targetDirect = found && bestDistance <= 0f;
        }

        // Distance from the cursor to the thing vanilla's smart interact currently targets (float.MaxValue = vanilla has no target).
        static float GetVanillaTargetDistance(Vector2 mouse)
        {
            if (!Main.SmartInteractShowingGenuine)
                return float.MaxValue;

            float best = float.MaxValue;

            int npcIndex = Main.SmartInteractNPC;
            if (npcIndex >= 0 && npcIndex < Main.maxNPCs && Main.npc[npcIndex].active)
                best = DistanceToRect(mouse, Main.npc[npcIndex].Hitbox);

            foreach (Point coord in Main.SmartInteractTileCoords)
                best = Math.Min(best, DistanceToRect(mouse, new Rectangle(coord.X * 16, coord.Y * 16, 16, 16)));

            if (best == float.MaxValue && Main.SmartInteractX >= 0 && Main.SmartInteractY >= 0)
                best = DistanceToRect(mouse, new Rectangle(Main.SmartInteractX * 16, Main.SmartInteractY * 16, 16, 16));

            return best == float.MaxValue ? 0f : best;   // a target we cannot measure (a projectile): vanilla keeps it
        }

        static float DistanceToRect(Vector2 point, Rectangle rect)
        {
            float dx = Math.Max(Math.Max(rect.Left - point.X, 0f), point.X - rect.Right);
            float dy = Math.Max(Math.Max(rect.Top - point.Y, 0f), point.Y - rect.Bottom);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // Right-click while the target is not directly under the cursor: open it ourselves. A cursor that is directly over a machine is left to
        // the normal tile right-click (MachineVisualOverride.RightClick), and so is anything vanilla's own smart interact picked.
        void HandleNearbyRightClick()
        {
            if (!hasTarget || targetDirect || !PlayerInput.Triggers.JustPressed.MouseRight)
                return;

            Player player = Main.LocalPlayer;
            if (player.mouseInterface || Main.SmartInteractShowingGenuine)
                return;

            Vector2 mouse = CartSystem.GetZoomCorrectedMouseWorld();
            Tile under = Framing.GetTileSafely((int)Math.Floor(mouse.X / 16f), (int)Math.Floor(mouse.Y / 16f));
            if (under.HasTile && TileID.Sets.HasOutlines[under.TileType])
                return;

            Tile tile = Framing.GetTileSafely(targetPosition.X, targetPosition.Y);
            if (!tile.HasTile)
                return;

            fallbackHandledTick = Main.GameUpdateCount;
            ModContent.GetInstance<MachineVisualOverride>().RightClick(targetPosition.X, targetPosition.Y, tile.TileType);
        }

        uint fallbackHandledTick = uint.MaxValue;   // the tick HandleNearbyRightClick already used this press

        // Vanilla's own smart interact only knows its fixed list of tiles (crystal ball, chests, ...). Machines are vanilla tile types, so
        // they are never in that list, and tModLoader only offers the smart-interact hooks to ModTiles. When vanilla has picked something
        // else (Main.SmartInteractShowingGenuine) it calls TileInteractionsUse with THAT tile, so the click went to the crystal ball no
        // matter which machine we had highlighted. Here the click is pointed at our target machine instead, which then goes through the
        // normal tile right-click (MachineVisualOverride.RightClick). Exception: the cursor is directly over some other interactable
        // vanilla tile, then that tile wins (you are clearly pointing at it).
        private void RedirectClickToTarget(On_Player.orig_TileInteractionsUse orig, Player self, int myX, int myY)
        {
            if (!Main.dedServ && self.whoAmI == Main.myPlayer && hasTarget && Main.SmartCursorIsUsed && !self.mouseInterface)
            {
                if (fallbackHandledTick == Main.GameUpdateCount)
                {
                    // PostUpdateInput already opened the machine for this press: consume it so vanilla does not also use the crystal ball.
                    self.tileInteractionHappened = true;
                    if (self.controlUseTile && self.releaseUseTile)
                    {
                        self.tileInteractAttempted = true;
                        self.releaseUseTile = false;
                    }
                    return;
                }

                bool redirect = targetDirect;
                if (!redirect)
                {
                    Vector2 mouse = CartSystem.GetZoomCorrectedMouseWorld();
                    Tile under = Framing.GetTileSafely((int)Math.Floor(mouse.X / 16f), (int)Math.Floor(mouse.Y / 16f));
                    redirect = !(under.HasTile && TileID.Sets.HasOutlines[under.TileType]);
                }

                if (redirect && Framing.GetTileSafely(targetPosition.X, targetPosition.Y).HasTile)
                {
                    myX = targetPosition.X;
                    myY = targetPosition.Y;
                }
            }

            orig(self, myX, myY);
        }

        // Vanilla calls DrawNPCs(behindTiles: true) after the walls and before any tile, with a SpriteBatch already open (a Begin there
        // throws), so this draws straight into it. Drawing before orig puts the outline under every tile.
        private void DrawBehindTilesPass(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles)
        {
            if (behindTiles && !OutlineInFront)
                DrawOutlines();

            orig(self, behindTiles);
        }

        // OutlineInFront: drawn after all tiles. This pass owns its own SpriteBatch (the behind-tiles pass above draws into vanilla's).
        public override void PostDrawTiles()
        {
            if (!OutlineInFront || Main.dedServ || Main.gameMenu || !Main.SmartCursorIsUsed || MachineVisualRegistry.Definitions.Count == 0)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            DrawOutlines();
            spriteBatch.End();
        }

        void DrawOutlines()
        {
            if (Main.dedServ || Main.gameMenu || !Main.SmartCursorIsUsed || MachineVisualRegistry.Definitions.Count == 0)
                return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not BaseMachine machine)
                    continue;

                if (!TryGetMachineInfo(machine, out Tile tile, out MachineVisualDefinition def, out TileObjectData data))
                    continue;

                Point16 corner = machine.Position;
                Vector2 center = new Vector2(corner.X * 16 + data.Width * 8, corner.Y * 16 + data.Height * 8);
                if (!BaseMachine.IsOnScreen(center, Math.Max(data.Width, data.Height) * 8f + ScreenMarginPixels))
                    continue;

                // Same reach rule the right-click uses: if the click would be ignored, there is nothing to highlight.
                if (!MachineDetailSystem.WithinReach(player, machine, 0f))
                    continue;

                Outline outline = GetOutline(machine, tile, def, data);
                if (outline == null || outline.Runs.Count == 0)
                    continue;

                bool selected = hasTarget && targetPosition == corner;
                DrawOutline(spriteBatch, outline, selected ? SelectedColor : HighlightColor);
            }
        }

        // MagicPixel is probably not 1x1, so the source is always an explicit 1x1 rectangle.
        static void DrawOutline(SpriteBatch spriteBatch, Outline outline, Color color)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new Rectangle(0, 0, 1, 1);
            foreach (Rectangle run in outline.Runs)
            {
                Vector2 position = new Vector2(outline.OriginX + run.X, outline.OriginY + run.Y) - Main.screenPosition;
                spriteBatch.Draw(pixel, position, source, color, 0f, Vector2.Zero, new Vector2(run.Width, run.Height), SpriteEffects.None, 0f);
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
            signature = Mix(signature, OutlineScale);
            signature = Mix(signature, OutlineInside ? 1 : 0);
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

        static int FloorTo(int value, int step) => (int)Math.Floor(value / (double)step) * step;
        static int CeilTo(int value, int step) => (int)Math.Ceiling(value / (double)step) * step;

        // Solid mask of all cells on a grid of OutlineScale x OutlineScale world pixels (a grid cell is solid if any pixel in it is), then every
        // empty grid cell that touches a solid one (up, down, left, right) is an outline cell, drawn as one OutlineScale-sized block.
        // The grid is aligned to world coordinates, so the blocks line up with the 2x2 art pixels.
        Outline BuildOutline(List<SpriteCell> cells)
        {
            int scale = Math.Max(1, OutlineScale);
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (SpriteCell cell in cells)
            {
                minX = Math.Min(minX, cell.X);
                minY = Math.Min(minY, cell.Y);
                maxX = Math.Max(maxX, cell.X + cell.Source.Width);
                maxY = Math.Max(maxY, cell.Y + cell.Source.Height);
            }

            minX = FloorTo(minX, scale) - scale;   // one grid cell of room for the outline
            minY = FloorTo(minY, scale) - scale;
            maxX = CeilTo(maxX, scale) + scale;
            maxY = CeilTo(maxY, scale) + scale;
            int width = (maxX - minX) / scale;     // in grid cells
            int height = (maxY - minY) / scale;
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

                        int gridX = (cell.X + dx - minX) / scale;
                        int gridY = (cell.Y + dy - minY) / scale;
                        solid[gridY * width + gridX] = true;
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
                        bool nLeft = x > 0 && solid[index - 1];
                        bool nRight = x < width - 1 && solid[index + 1];
                        bool nUp = y > 0 && solid[index - width];
                        bool nDown = y < height - 1 && solid[index + width];

                        // Inside: a solid cell with at least one empty neighbour (the sprite's own border). Outside: an empty cell touching a solid one.
                        edge = OutlineInside
                            ? solid[index] && !(nLeft && nRight && nUp && nDown)
                            : !solid[index] && (nLeft || nRight || nUp || nDown);
                    }

                    if (edge)
                    {
                        if (runStart < 0)
                            runStart = x;
                    }
                    else if (runStart >= 0)
                    {
                        outline.Runs.Add(new Rectangle(runStart * scale, y * scale, (x - runStart) * scale, scale));
                        runStart = -1;
                    }
                }
            }

            return outline;
        }
    }
}