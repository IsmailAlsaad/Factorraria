using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.SuperWeapons
{
    // One super weapon part whose art is bigger than its tile footprint (the drill tip, the thruster's rear plate).
    public class OverhangPart
    {
        public int TileType;
        public int FootprintTilesX;
        public int FootprintTilesY;
        public int ArtWidthPx;
        public int ArtHeightPx;
        public int ArtOffsetXPx;          // left edge of the art, relative to the footprint's left edge, when facing right (can be negative)
        public int ArtOffsetYPx;          // top edge of the art, relative to the footprint's top edge
        public Asset<Texture2D> Texture;  // null while the PNG does not exist: a translucent block is drawn instead
    }

    // Draws super weapon parts whose art spills outside their tile footprint (super weapons patch S2.1). CLIENT ONLY: nothing here is saved or
    // sent. The tile itself (placed, mined and synced by vanilla tile code) is the only state; the facing is read from its frame.
    //
    // V5 (drawing outside the footprint): ModTile.PostDraw only runs for tiles whose own cell is on screen, and it runs inside the tile pass,
    // so later tiles paint over the overhang and a tip would pop in at the screen edge. So the parts' own tile draw is switched off (PreDraw
    // returns false) and this system draws every part after all tiles (PostDrawTiles), scanning the visible area plus a padding of tiles.
    // The parts have no TileEntity (assembly in S3 is anchored on the tank), so IMachineDynamicDraw, which walks TileEntity.ByPosition, does not
    // fit; the scan costs one tile lookup per visible tile.
    //
    // V6 (facing): style 0 is facing right (Direction PlaceRight), style 1 facing left (alternate PlaceLeft, addAlternate(1)), StyleHorizontal with
    // StyleMultiplier 1. A placed tile's facing is TileFrameX / (footprint width * 18): 0 = right, 1 = left. TileObject.CanPlace and the vanilla
    // placement pick the alternate from Player.direction, so the hologram uses the same.
    //
    // Art is drawn facing right only; facing left is the same art mirrored around the footprint's centre (D9).
    public class OverhangDrawSystem : ModSystem
    {
        // Padding around the visible area, in tiles, so a part whose footprint is just off screen still draws its overhang.
        public int ScanPaddingTiles = 8;
        public Color FallbackColor = new Color(96, 98, 106);
        public Color FallbackOverhangColor = new Color(150, 160, 190);
        public float FallbackAlpha = 0.65f;

        readonly Dictionary<int, OverhangPart> parts = new Dictionary<int, OverhangPart>();

        public override void Load()
        {
            if (Main.dedServ)
                return;

            On_TileObject.DrawPreview += HideVanillaPreview;
        }

        public override void Unload()
        {
            parts.Clear();
            if (Main.dedServ)
                return;

            On_TileObject.DrawPreview -= HideVanillaPreview;
        }

        /// <summary>Call from the part tile's SetStaticDefaults. Does nothing on a dedicated server (no screen).</summary>
        public void Register(int tileType, string texturePath, int footprintTilesX, int footprintTilesY, int artWidthPx, int artHeightPx, int artOffsetXPx, int artOffsetYPx)
        {
            if (Main.dedServ)
                return;

            Asset<Texture2D> texture = null;
            if (ModContent.HasAsset(texturePath))
                texture = ModContent.Request<Texture2D>(texturePath);

            parts[tileType] = new OverhangPart
            {
                TileType = tileType,
                FootprintTilesX = footprintTilesX,
                FootprintTilesY = footprintTilesY,
                ArtWidthPx = artWidthPx,
                ArtHeightPx = artHeightPx,
                ArtOffsetXPx = artOffsetXPx,
                ArtOffsetYPx = artOffsetYPx,
                Texture = texture
            };
        }

        /// <summary>+1 facing right, -1 facing left, for any cell of a placed part (style 0 = right, style 1 = left).</summary>
        public static int GetFacing(Tile tile, int footprintTilesX)
        {
            return tile.TileFrameX / (footprintTilesX * 18) == 0 ? 1 : -1;
        }


        // Vanilla's own preview only draws the footprint, so it is skipped for these parts and the hologram below replaces it.
        void HideVanillaPreview(On_TileObject.orig_DrawPreview orig, SpriteBatch sb, TileObjectPreviewData op, Vector2 position)
        {
            if (parts.ContainsKey(op.Type))
                return;

            orig(sb, op, position);
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu || parts.Count == 0)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool batchBegun = false;

            float zoom = Main.GameViewMatrix.Zoom.X;
            if (zoom < 0.01f)
                zoom = 1f;
            float centerX = Main.screenPosition.X + Main.screenWidth * 0.5f;
            float centerY = Main.screenPosition.Y + Main.screenHeight * 0.5f;
            float halfWidth = Main.screenWidth / zoom * 0.5f;
            float halfHeight = Main.screenHeight / zoom * 0.5f;
            int left = Math.Max(0, (int)((centerX - halfWidth) / 16f) - ScanPaddingTiles);
            int right = Math.Min(Main.maxTilesX - 1, (int)((centerX + halfWidth) / 16f) + ScanPaddingTiles);
            int top = Math.Max(0, (int)((centerY - halfHeight) / 16f) - ScanPaddingTiles);
            int bottom = Math.Min(Main.maxTilesY - 1, (int)((centerY + halfHeight) / 16f) + ScanPaddingTiles);

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile || !parts.TryGetValue(tile.TileType, out OverhangPart part))
                        continue;

                    // only the top-left cell of the footprint draws the part
                    if (tile.TileFrameY != 0 || tile.TileFrameX % (part.FootprintTilesX * 18) != 0)
                        continue;

                    if (!batchBegun)
                    {
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                        batchBegun = true;
                    }

                    Color light = Lighting.GetColor(x + part.FootprintTilesX / 2, y + part.FootprintTilesY / 2);
                    DrawPart(spriteBatch, part, new Vector2(x * 16, y * 16), GetFacing(tile, part.FootprintTilesX), light, false);
                }
            }

            if (TryGetHologram(out OverhangPart holoPart, out Vector2 holoTopLeft, out bool holoValid))
            {
                if (!batchBegun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    batchBegun = true;
                }

                Color tint = holoValid ? Color.White * 0.55f : new Color(255, 70, 70) * 0.6f;
                DrawPart(spriteBatch, holoPart, holoTopLeft, Main.LocalPlayer.direction >= 0 ? 1 : -1, tint, true);
            }

            if (batchBegun)
                spriteBatch.End();
        }

        // footprintTopLeft is in world pixels. The art is mirrored around the footprint's centre for a left-facing part.
        void DrawPart(SpriteBatch spriteBatch, OverhangPart part, Vector2 footprintTopLeft, int facing, Color color, bool hologram)
        {
            int footprintWidth = part.FootprintTilesX * 16;
            int footprintHeight = part.FootprintTilesY * 16;
            float artX = facing > 0 ? part.ArtOffsetXPx : footprintWidth - (part.ArtOffsetXPx + part.ArtWidthPx);
            Vector2 artTopLeft = footprintTopLeft + new Vector2(artX, part.ArtOffsetYPx);

            if (part.Texture != null)
            {
                Texture2D texture = part.Texture.Value;
                Vector2 half = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
                SpriteEffects effects = facing > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                spriteBatch.Draw(texture, artTopLeft + half - Main.screenPosition, null, color, 0f, half, 1f, effects, 0f);
                return;
            }

            // no art yet: the whole art rectangle in the overhang colour, the footprint on top in the base colour (hologram: tinted by the placement colour)
            float alpha = FallbackAlpha * (hologram ? 0.85f : 1f);
            Color overhang = hologram ? FallbackOverhangColor.MultiplyRGBA(color) : FallbackOverhangColor * alpha;
            Color body = hologram ? FallbackColor.MultiplyRGBA(color) : FallbackColor * alpha;
            Rectangle pixel = new Rectangle(0, 0, 1, 1);
            Rectangle art = new Rectangle((int)(artTopLeft.X - Main.screenPosition.X), (int)(artTopLeft.Y - Main.screenPosition.Y), part.ArtWidthPx, part.ArtHeightPx);
            Rectangle footprint = new Rectangle((int)(footprintTopLeft.X - Main.screenPosition.X), (int)(footprintTopLeft.Y - Main.screenPosition.Y), footprintWidth, footprintHeight);
            spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, art, pixel, overhang);
            spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, footprint, pixel, body);
        }

        // Placement preview while the held item places a part: the whole sprite (overhang included) faded, or red when it can't go there.
        bool TryGetHologram(out OverhangPart part, out Vector2 topLeft, out bool valid)
        {
            part = null;
            topLeft = Vector2.Zero;
            valid = false;

            Player player = Main.LocalPlayer;
            Item held = player.HeldItem;
            if (player.dead || player.mouseInterface || held.IsAir || held.createTile < 0)
                return false;

            if (!parts.TryGetValue(held.createTile, out part))
                return false;

            TileObjectData data = TileObjectData.GetTileData(held.createTile, held.placeStyle);
            if (data == null)
                return false;

            int cornerX = Player.tileTargetX - data.Origin.X;
            int cornerY = Player.tileTargetY - data.Origin.Y;
            topLeft = new Vector2(cornerX * 16, cornerY * 16);

            valid = player.IsTargetTileInItemRange(held)
                && TileObject.CanPlace(Player.tileTargetX, Player.tileTargetY, held.createTile, held.placeStyle, player.direction, out _, true);
            return true;
        }
    }
}