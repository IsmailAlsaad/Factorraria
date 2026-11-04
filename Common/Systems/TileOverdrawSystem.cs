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
        public bool Warned;
    }

    // Draws extra art AROUND a normal 1x1 tile (pixels outside its 16x16 cell) for tiles that register with it. CLIENT ONLY: nothing here is
    // saved or sent; it only reads what every client already has (the tile frame, or the machine entity's state).
    //
    // InFront (per registration, default true): true draws after all tiles, so the overhang covers neighbouring tiles. false draws in the
    // behind-tiles pass (the DrawNPCs(behindTiles: true) call vanilla makes before any tile), so every tile, the owner included, is drawn over
    // it and the overhang only shows where nothing solid is in front of it. That pass draws into the SpriteBatch vanilla already has open. The entry's InFront field can also be flipped at runtime.
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
        public void Register(int tileType, string texturePath, TileOverdrawSelector selector, int marginPx = 0, bool inFront = true)
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
                Select = selector
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

                    Texture2D texture = entry.Texture.Value;
                    int cellSize = 16 + entry.MarginPx * 2;
                    Rectangle source = new Rectangle(cell * cellSize, 0, cellSize, cellSize);
                    if (source.Right > texture.Width || source.Bottom > texture.Height)
                    {
                        if (!entry.Warned)
                        {
                            entry.Warned = true;
                            Mod.Logger.Warn($"Overdraw sheet for tile {entry.TileType} is {texture.Width}x{texture.Height}, too small for cell {cell} at {cellSize}px cells (margin {entry.MarginPx}).");
                        }
                        continue;
                    }

                    if (!batchBegun)
                    {
                        batchBegun = true;

                        // The behind pass runs inside DoDraw_DrawNPCsBehindTiles, where vanilla already has a SpriteBatch open (a Begin here throws
                        // every frame), so it draws straight into that one. The after-tiles pass has none open and begins its own.
                        if (front)
                        {
                            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                            ownBatch = true;
                        }
                    }

                    Vector2 center = new Vector2(x * 16 + 8, y * 16 + 8) - Main.screenPosition;
                    Vector2 origin = new Vector2(cellSize * 0.5f);
                    spriteBatch.Draw(texture, center, source, Lighting.GetColor(x, y), rotation, origin, 1f, SpriteEffects.None, 0f);
                }
            }

            if (ownBatch)
                spriteBatch.End();
        }
    }
}