using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    // Client only. Draws every pole's crossboards (see PoleBoard) on top of its footprint, after all tiles and after the overhang canvases,
    // each rotated around its own centre by a wind sway angle, and draws the boards of the placement hologram while the player holds a
    // pole item (the footprint part of the hologram comes from vanilla's preview or MachineOverhangSystem). Nothing here is saved or sent:
    // every client derives the sway from its own wind, and the server never draws.
    // The link logic (P3 onward) must NOT use the sway: it uses PowerPoleTileEntityBase.GetLinkPointWorld (resting position).
    public class PowerPoleCrossboardSystem : ModSystem
    {
        // Tunables (plain instance fields, so hot reload can change them).
        public float MphPerWindUnit = 50f;        // Main.windSpeedCurrent * 50 = the mph the weather radio shows
        public float CalmMph = 20f;               // at or below this wind speed (either direction) the boards do not move at all
        public float FullSwayMph = 60f;           // at this wind speed (the game's maximum) the sway is at 100%; it ramps up linearly from CalmMph
        public float LeanRadians = 0.12f;         // steady lean in the wind's direction at 100% sway
        public float SwingRadians = 0.08f;        // back-and-forth swing amplitude at 100% sway
        public float SwingSpeed = 1.6f;           // swing speed, radians per second

        // Sheet layout (0-indexed column and row, see PoleBoard). Plain instance fields so hot reload can change them.
        public int BaseColumn = 0;                // row 0: the base board, always drawn
        public int BaseRow = 0;
        public int HighlightRow = 0;              // row 0: the hover highlights
        public int ValidRightColumn = 1;
        public int ValidLeftColumn = 2;
        public int InvalidRightColumn = 3;
        public int InvalidLeftColumn = 4;
        public int WireBothColumn = 0;            // rows 1 to 3: wire on both points / only the right point / only the left point
        public int WireRightColumn = 1;
        public int WireLeftColumn = 2;
        public int CopperRow = 1;
        public int TinRow = 2;
        public int GoldRow = 3;

        readonly Dictionary<string, Asset<Texture2D>> textures = new();
        readonly HashSet<string> sizeChecked = new();
        readonly HashSet<string> badSheets = new();     // sheets whose size does not fit their PoleBoard: not drawn (a source rectangle outside the texture would draw garbage)

        public override void Unload()
        {
            textures.Clear();
            sizeChecked.Clear();
            badSheets.Clear();
        }

        // The angle of board number 'boardIndex' on the pole whose entity sits at 'pole'. swayScale is the pole's own factor
        // (PowerPoleTileEntityBase.SwayScale: wooden 1, platinum 0.5). No sway at all at or below CalmMph, then it ramps up linearly
        // with the wind speed to full at FullSwayMph. The swing phase comes from the pole's tile position (and the board index), so
        // neighbouring poles, and the two boards of one platinum pole, do not move in lockstep.
        public float GetSwayAngle(Point16 pole, int boardIndex, float swayScale)
        {
            float mph = Math.Abs(Main.windSpeedCurrent) * MphPerWindUnit;
            float strength = MathHelper.Clamp((mph - CalmMph) / Math.Max(1f, FullSwayMph - CalmMph), 0f, 1f);
            if (strength <= 0f)
                return 0f;

            float direction = Math.Sign(Main.windSpeedCurrent);
            float phase = pole.X * 2.4f + pole.Y * 1.7f + boardIndex * 1.9f;
            float swing = (float)Math.Sin(Main.GlobalTimeWrappedHourly * SwingSpeed + phase) * SwingRadians;
            return strength * swayScale * (direction * LeanRadians + swing);
        }

        // The board's sheet. null while the PNG does not exist (a warning is logged once, and that board is simply not drawn), and also null
        // when the sheet is not the size its PoleBoard says (wrong width, or fewer rows than the PoleBoard has): logged once, not drawn.
        Texture2D GetTexture(PoleBoard board)
        {
            string path = board.TexturePath;
            if (badSheets.Contains(path))
                return null;

            if (!textures.TryGetValue(path, out Asset<Texture2D> asset))
            {
                if (!ModContent.RequestIfExists<Texture2D>(path, out asset, AssetRequestMode.ImmediateLoad))
                {
                    asset = null;
                    Mod.Logger.Warn("Power pole crossboard sheet '" + path + "' not found, that board is not drawn.");
                }

                textures[path] = asset;
            }

            Texture2D texture = asset?.Value;
            if (texture != null && sizeChecked.Add(path))
            {
                int wantWidth = (int)board.SheetSize.X;
                int wantHeight = (int)board.SheetSize.Y;
                if (texture.Width != wantWidth || texture.Height != wantHeight)
                    Mod.Logger.Warn("Power pole crossboard sheet '" + path + "' is " + texture.Width + "x" + texture.Height + " but its PoleBoard says " + wantWidth + "x" + wantHeight + " (" + board.SheetColumns + " x " + board.SheetRows + " cells of " + board.Size.X + "x" + board.Size.Y + "). Update the PoleBoard (cell size, rows) or the PNG.");

                if (texture.Width != wantWidth || texture.Height < wantHeight)
                {
                    badSheets.Add(path);
                    Mod.Logger.Warn("Power pole crossboard sheet '" + path + "' is too small or has the wrong width, that board is not drawn.");
                }
            }

            return badSheets.Contains(path) ? null : texture;
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool batchBegun = false;
            PoleLinkSystem links = PoleLinkSystem.Instance;
            PoleLinkInputSystem input = ModContent.GetInstance<PoleLinkInputSystem>();

            // Hanging wires FIRST (own batch, only when a wire is on screen), so they hang BEHIND the crossboards drawn below and the
            // crossarm tips cover the wire ends. They still land on top of the overhang canvases (MachineOverhangSystem sorts before this).
            ModContent.GetInstance<PoleWireDrawSystem>().DrawLinks(spriteBatch);

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not PowerPoleTileEntityBase pole)
                    continue;

                Point16 corner = pole.Position;
                Tile tile = Framing.GetTileSafely(corner.X, corner.Y);
                if (!tile.HasTile || tile.TileType != pole.ValidTileType)
                    continue;

                PoleBoard[] boards = pole.Boards;
                for (int b = 0; b < boards.Length; b++)
                {
                    PoleBoard board = boards[b];
                    Texture2D texture = GetTexture(board);
                    if (texture == null)
                        continue;

                    Vector2 attach = corner.ToVector2() * 16f + board.AttachOffset;
                    if (!BaseMachine.IsOnScreen(attach, Math.Max(board.Size.X, board.Size.Y)))
                        continue;

                    if (!batchBegun)
                    {
                        BeginBatch(spriteBatch);
                        batchBegun = true;
                    }

                    Color color = Lighting.GetColor((int)(attach.X / 16f), (int)(attach.Y / 16f));
                    DrawBoardLayers(spriteBatch, texture, pole, b, links, input, attach - Main.screenPosition, color, GetSwayAngle(corner, b, pole.SwayScale));
                }
            }

            if (TryGetHologram(out PowerPoleTileEntityBase prototype, out Vector2 holoTopLeft, out bool holoValid))
            {
                Color tint = holoValid ? Color.White * 0.55f : new Color(255, 70, 70) * 0.6f;
                foreach (PoleBoard board in prototype.Boards)
                {
                    Texture2D texture = GetTexture(board);
                    if (texture == null)
                        continue;

                    if (!batchBegun)
                    {
                        BeginBatch(spriteBatch);
                        batchBegun = true;
                    }

                    spriteBatch.Draw(texture, holoTopLeft + board.AttachOffset - Main.screenPosition, board.Cell(BaseColumn, BaseRow), tint, 0f, board.Pivot, 1f, SpriteEffects.None, 0f);
                }
            }

            if (batchBegun)
                spriteBatch.End();

            // Pending-link hologram and connection point markers (P6), on top of the finished wires.
            ModContent.GetInstance<PoleLinkInputSystem>().DrawWorld(spriteBatch);
        }

        // One board, up to three layers, each its own Draw call of a different cell of the SAME sheet at the SAME position, rotation and light, so the
        // layers stay aligned while the board sways. Order: 1 the base board, 2 the hover highlights on top of it, 3 the wire connection cells on top.
        // The hanging wire itself is drawn earlier (PoleWireDrawSystem.DrawLinks), behind all of this.
        void DrawBoardLayers(SpriteBatch spriteBatch, Texture2D texture, PowerPoleTileEntityBase pole, int boardIndex, PoleLinkSystem links, PoleLinkInputSystem input, Vector2 position, Color color, float angle)
        {
            PoleBoard board = pole.Boards[boardIndex];
            int slotBase = 0;
            for (int i = 0; i < boardIndex; i++)
                slotBase += pole.Boards[i].LinkPoints.Length;

            // 1. base board
            spriteBatch.Draw(texture, position, board.Cell(BaseColumn, BaseRow), color, angle, board.Pivot, 1f, SpriteEffects.None, 0f);

            // 2. hover highlights: one cell per highlighted point, valid or invalid, right or left (the point's slot is slotBase + its index in the board)
            for (int i = 0; i < board.LinkPoints.Length; i++)
            {
                PoleLinkInputSystem.PointHighlight highlight = input.GetPointHighlight(pole, slotBase + i);
                if (highlight == PoleLinkInputSystem.PointHighlight.None)
                    continue;

                bool right = board.IsRightPoint(i);
                int column = highlight == PoleLinkInputSystem.PointHighlight.Valid
                    ? (right ? ValidRightColumn : ValidLeftColumn)
                    : (right ? InvalidRightColumn : InvalidLeftColumn);
                spriteBatch.Draw(texture, position, board.Cell(column, HighlightRow), color, angle, board.Pivot, 1f, SpriteEffects.None, 0f);
            }

            // 3. wire connection cells: which points of this board carry a link, and of which wire type
            bool rightUsed = false;
            bool leftUsed = false;
            CustomWireType rightWire = default;
            CustomWireType leftWire = default;
            for (int i = 0; i < board.LinkPoints.Length; i++)
            {
                PoleLink link = links.GetLink(pole.Position, slotBase + i);
                if (link == null)
                    continue;

                if (board.IsRightPoint(i))
                {
                    rightUsed = true;
                    rightWire = link.Wire;
                }
                else
                {
                    leftUsed = true;
                    leftWire = link.Wire;
                }
            }

            if (rightUsed && leftUsed && rightWire == leftWire)
            {
                DrawWireCell(spriteBatch, texture, board, rightWire, WireBothColumn, position, color, angle);
                return;
            }

            // Two different wire types (or one point only): the right point's cell with its type's right variant, the left point's with its type's left variant.
            if (rightUsed)
                DrawWireCell(spriteBatch, texture, board, rightWire, WireRightColumn, position, color, angle);
            if (leftUsed)
                DrawWireCell(spriteBatch, texture, board, leftWire, WireLeftColumn, position, color, angle);
        }

        void DrawWireCell(SpriteBatch spriteBatch, Texture2D texture, PoleBoard board, CustomWireType wire, int column, Vector2 position, Color color, float angle)
        {
            int row = wire.HasFlag(CustomWireType.Gold) ? GoldRow : wire.HasFlag(CustomWireType.Tin) ? TinRow : wire.HasFlag(CustomWireType.Copper) ? CopperRow : -1;
            if (row < 0)
                return;

            spriteBatch.Draw(texture, position, board.Cell(column, row), color, angle, board.Pivot, 1f, SpriteEffects.None, 0f);
        }

        static void BeginBatch(SpriteBatch spriteBatch)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Same rules as the overhang machines' hologram: the held item places a pole, the cursor is not over UI, and red when it cannot go there.
        // topLeft is the footprint's top-left corner in world pixels.
        static bool TryGetHologram(out PowerPoleTileEntityBase prototype, out Vector2 topLeft, out bool valid)
        {
            prototype = null;
            topLeft = Vector2.Zero;
            valid = false;

            Player player = Main.LocalPlayer;
            Item held = player.HeldItem;
            if (player.dead || player.mouseInterface || held.IsAir || held.createTile < 0)
                return false;

            if (ModContent.GetModTile(held.createTile) is not PowerPoleTileBase poleTile)
                return false;

            TileObjectData data = TileObjectData.GetTileData(held.createTile, held.placeStyle);
            if (data == null)
                return false;

            prototype = poleTile.PlacementEntity;
            int cornerX = Player.tileTargetX - data.Origin.X;
            int cornerY = Player.tileTargetY - data.Origin.Y;
            topLeft = new Vector2(cornerX * 16f, cornerY * 16f);

            valid = player.IsTargetTileInItemRange(held)
                && TileObject.CanPlace(Player.tileTargetX, Player.tileTargetY, held.createTile, held.placeStyle, player.direction, out _, true);
            return true;
        }
    }
}