using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    // Picks which cell of the overdraw sheet a placed tile draws. Return false to draw nothing for this tile (e.g. a machine whose entity
    // has not reached this client yet). rotation is in radians, around the tile's centre (0 = the art as drawn).
    public delegate bool TileOverdrawSelector(int i, int j, Tile tile, out int cell, out float rotation);

    public class TileOverdrawEntry
    {
        public int TileType;
        public Asset<Texture2D> Texture;   // null while the PNG does not exist: nothing is drawn
        public int MarginPx;
        public bool InFront = true;        // true: drawn after all tiles (over them); false: drawn before any tile (behind them)
        public TileOverdrawSelector Select;
        public TileOverdrawSelector PreviewSelect;   // cell for the placement hologram (the tile is not placed yet); null = Select
        public bool Warned;
    }

    // Draws extra art AROUND a normal 1x1 tile (pixels outside its 16x16 cell) for tiles that register with it. CLIENT ONLY: nothing here is
    // saved or sent; it only reads what every client already has (the tile frame, or the machine entity's state).
    //
    // InFront (per registration, default true): true draws after all tiles, so the overhang covers neighbouring tiles. false draws in the
    // behind-tiles pass (the DrawNPCs(behindTiles: true) call vanilla makes before any tile), so every tile, the owner included, is drawn over
    // it and the overhang only shows where nothing solid is in front of it. That pass draws into the SpriteBatch vanilla already has open. The entry's InFront field can also be flipped at runtime.
    //
    // Placement hologram: while the held item places a registered tile, its overdraw is drawn at the target tile, faded (or red when it can't
    // go there), in the same pass as the tile's InFront. The cell comes from the registration's previewSelector (the tile is not placed yet).
    //
    // Why a system and not ModTile.PostDraw: PostDraw runs inside the tile pass, so tiles drawn later paint over the overhang (and it would
    // pop out at the screen edge). Everything is drawn here after all tiles instead, over the visible area plus a small padding.
    //
    // Sheet layout: cells side by side in ONE row, each cell (16 + 2 * margin) px square, no padding between cells, the tile's own 16x16
    // sitting in the middle of every cell. Draw ONLY the extra pixels: leave the middle 16x16 transparent, the tile itself is still drawn
    // by vanilla / the machine visual code.
    public class TileOverdrawSystem : ModSystem
    {
        public int ScanPaddingTiles = 2;
        public int DefaultMarginPx = 8;

        readonly Dictionary<int, TileOverdrawEntry> entries = new Dictionary<int, TileOverdrawEntry>();

        public override void Load()
        {
            if (Main.dedServ) return; // no screen on a dedicated server: the draw detour is never needed there
            On_Main.DrawNPCs += DrawBehindTilesPass;
        }

        public override void Unload()
        {
            entries.Clear();
            if (Main.dedServ) return; // matches Load()
            On_Main.DrawNPCs -= DrawBehindTilesPass;
        }

        // Vanilla calls DrawNPCs(behindTiles: true) before any tile is drawn (same ride as MachineDynamicDrawSystem).
        void DrawBehindTilesPass(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles)
        {
            if (behindTiles)
                DrawPass(false);

            orig(self, behindTiles);
        }

        /// <summary>Call from the tile's SetStaticDefaults. Does nothing on a dedicated server (no screen). marginPx 0 = DefaultMarginPx. inFront false = draw behind tiles.</summary>
        public void Register(int tileType, string texturePath, TileOverdrawSelector selector, int marginPx = 0, bool inFront = true, TileOverdrawSelector previewSelector = null)
        {
            if (Main.dedServ)
                return;

            ModContent.RequestIfExists(texturePath, out Asset<Texture2D> texture);

            entries[tileType] = new TileOverdrawEntry
            {
                TileType = tileType,
                Texture = texture,
                MarginPx = marginPx > 0 ? marginPx : DefaultMarginPx,
                InFront = inFront,
                Select = selector,
                PreviewSelect = previewSelector
            };
        }

        public override void PostDrawTiles()
        {
            DrawPass(true);
        }

        // front true = the after-tiles pass, false = the behind-tiles pass. Only entries whose InFront matches are drawn.
        void DrawPass(bool front)
        {
            if (Main.dedServ || Main.gameMenu || entries.Count == 0)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool batchBegun = false;
            bool ownBatch = false;

            // The behind pass runs inside DoDraw_DrawNPCsBehindTiles, where vanilla already has a SpriteBatch open (a Begin here throws
            // every frame), so it draws straight into that one. The after-tiles pass has none open and begins its own.
            void BeginBatchIfNeeded()
            {
                if (batchBegun)
                    return;

                batchBegun = true;
                if (front)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    ownBatch = true;
                }
            }

            VisibleTileScan.GetRange(ScanPaddingTiles, out int left, out int right, out int top, out int bottom);

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile || !entries.TryGetValue(tile.TileType, out TileOverdrawEntry entry) || entry.Texture == null || entry.InFront != front)
                        continue;

                    if (!entry.Select(x, y, tile, out int cell, out float rotation) || cell < 0)
                        continue;

                    if (!TryGetSource(entry, cell, out Texture2D texture, out Rectangle source))
                        continue;

                    BeginBatchIfNeeded();
                    DrawCell(spriteBatch, texture, source, x, y, rotation, Lighting.GetColor(x, y));
                }
            }

            if (TryGetHologram(front, out TileOverdrawEntry holoEntry, out int holoX, out int holoY, out int holoCell, out float holoRotation, out bool holoValid)
                && TryGetSource(holoEntry, holoCell, out Texture2D holoTexture, out Rectangle holoSource))
            {
                BeginBatchIfNeeded();
                Color tint = holoValid ? Color.White * 0.55f : new Color(255, 70, 70) * 0.6f;
                DrawCell(spriteBatch, holoTexture, holoSource, holoX, holoY, holoRotation, tint);
            }

            if (ownBatch)
                spriteBatch.End();
        }

        // The texture and the source rectangle of one cell. False (and one warning per tile) when the sheet is too small for that cell.
        bool TryGetSource(TileOverdrawEntry entry, int cell, out Texture2D texture, out Rectangle source)
        {
            texture = entry.Texture.Value;
            int cellSize = 16 + entry.MarginPx * 2;
            source = new Rectangle(cell * cellSize, 0, cellSize, cellSize);
            if (source.Right <= texture.Width && source.Bottom <= texture.Height)
                return true;

            if (!entry.Warned)
            {
                entry.Warned = true;
                Mod.Logger.Warn($"Overdraw sheet for tile {entry.TileType} is {texture.Width}x{texture.Height}, too small for cell {cell} at {cellSize}px cells (margin {entry.MarginPx}).");
            }
            return false;
        }

        // The cell is centred on tile (x, y) and rotated around that centre.
        static void DrawCell(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, int x, int y, float rotation, Color color)
        {
            Vector2 center = new Vector2(x * 16 + 8, y * 16 + 8) - Main.screenPosition;
            Vector2 origin = new Vector2(source.Width * 0.5f, source.Height * 0.5f);
            spriteBatch.Draw(texture, center, source, color, rotation, origin, 1f, SpriteEffects.None, 0f);
        }

        // Placement hologram: the held item places a registered tile of this pass. valid = in reach and placeable there.
        bool TryGetHologram(bool front, out TileOverdrawEntry entry, out int x, out int y, out int cell, out float rotation, out bool valid)
        {
            entry = null;
            x = 0;
            y = 0;
            cell = 0;
            rotation = 0f;
            valid = false;

            if (Main.gameMenu)
                return false;

            Player player = Main.LocalPlayer;
            Item held = player.HeldItem;
            if (player.dead || player.mouseInterface || held.IsAir || held.createTile < 0)
                return false;

            if (!entries.TryGetValue(held.createTile, out entry) || entry.Texture == null || entry.InFront != front)
                return false;

            x = Player.tileTargetX;
            y = Player.tileTargetY;
            TileOverdrawSelector previewOrMain = entry.PreviewSelect ?? entry.Select;
            if (previewOrMain == null || !previewOrMain(x, y, Framing.GetTileSafely(x, y), out cell, out rotation) || cell < 0)
                return false;

            valid = player.IsTargetTileInItemRange(held)
                && TileObject.CanPlace(x, y, held.createTile, held.placeStyle, player.direction, out _, true);
            return true;
        }
    }
}