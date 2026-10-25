using Factorraria.Common.Machines;
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
        public float MaxSwayRadians = 0.08f;      // lean at full wind
        public float WobbleRadians = 0.015f;      // gentle extra swing, scaled up by the wind
        public float WobbleSpeed = 1.6f;          // radians per second

        readonly Dictionary<string, Asset<Texture2D>> textures = new();
        readonly HashSet<string> sizeChecked = new();

        public override void Unload()
        {
            textures.Clear();
            sizeChecked.Clear();
        }

        // The angle of board number 'boardIndex' on the pole whose entity sits at 'pole'. Every board gets its own phase, so a row of
        // poles (and the two boards of one platinum pole) do not swing in lockstep.
        public float GetSwayAngle(Point16 pole, int boardIndex)
        {
            float wind = MathHelper.Clamp(Main.WindForVisuals, -1f, 1f);
            float phase = pole.X * 0.37f + pole.Y * 0.11f + boardIndex * 1.9f;
            float wobble = (float)Math.Sin(Main.GlobalTimeWrappedHourly * WobbleSpeed + phase) * WobbleRadians * (0.3f + Math.Abs(wind));
            return wind * MaxSwayRadians + wobble;
        }

        // null while the PNG does not exist (a warning is logged once, and that board is simply not drawn).
        Texture2D GetTexture(PoleBoard board)
        {
            string path = board.TexturePath;
            if (!textures.TryGetValue(path, out Asset<Texture2D> asset))
            {
                if (!ModContent.RequestIfExists<Texture2D>(path, out asset, AssetRequestMode.ImmediateLoad))
                {
                    asset = null;
                    Mod.Logger.Warn("Power pole crossboard texture '" + path + "' not found, that board is not drawn.");
                }

                textures[path] = asset;
            }

            Texture2D texture = asset?.Value;
            if (texture != null && sizeChecked.Add(path) && (texture.Width != (int)board.Size.X || texture.Height != (int)board.Size.Y))
                Mod.Logger.Warn("Power pole crossboard '" + path + "' is " + texture.Width + "x" + texture.Height + " but its PoleBoard says " + board.Size.X + "x" + board.Size.Y + ". Update the Size (it sets the pivot and the link point math).");

            return texture;
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool batchBegun = false;

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
                    spriteBatch.Draw(texture, attach - Main.screenPosition, null, color, GetSwayAngle(corner, b), board.Pivot, 1f, SpriteEffects.None, 0f);
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

                    spriteBatch.Draw(texture, holoTopLeft + board.AttachOffset - Main.screenPosition, null, tint, 0f, board.Pivot, 1f, SpriteEffects.None, 0f);
                }
            }

            if (batchBegun)
                spriteBatch.End();
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