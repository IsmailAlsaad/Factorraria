using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    // Client only (power poles patch P5). Draws every finished pole link as a straight 2px line between its two SWAYED connection points,
    // so the wire follows the swaying crossboards. Nothing here is saved or sent: it only reads the link replica (PoleLinkSystem) and the
    // pole entities, and the server never draws.
    //
    // Draw order: there is no PostDrawTiles of its own. PowerPoleCrossboardSystem.PostDrawTiles calls DrawLinks as its FIRST step (changed
    // after the P3-P6 test), so the wires land on top of the overhang canvases (MachineOverhangSystem, which sorts before it) but BEHIND the
    // crossboards, which cover the wire ends. That does not depend on the load order of ModSystems.
    //
    // Links and span checks use the RESTING points (PowerPoleTileEntityBase.GetLinkPointWorld); only the drawing uses the swayed ones.
    public class PoleWireDrawSystem : ModSystem
    {
        // Tunables (plain instance fields, so hot reload can change them).
        public float WireThickness = 2f;
        public float CullMarginPixels = 4f;
        public Color CopperColor = new Color(183, 88, 25);     // sampled from CopperWireTile.png
        public Color TinColor = new Color(85, 91, 75);      // sampled from TinWireTile.png
        public Color GoldColor = new Color(203, 179, 73);     // sampled from GoldWireTile.png
        public Color InvalidColor = new Color(255, 70, 70);    // same red as the pole placement hologram

        // Per-link data that only changes when the link graph changes (PoleLinkSystem.Version): a pixel rectangle that contains the wire
        // wherever the crossboards swing (resting points grown by each point's distance from its board's centre), so culling needs no
        // entity lookups. A link whose poles are missing (or whose slot does not exist) on this client is "unresolved": it is skipped
        // and retried every frame until the entities show up (join order, V8).
        class CachedLink
        {
            public PoleLink Link;
            public Rectangle Bounds;
            public bool Resolved;
        }

        readonly List<CachedLink> cache = new List<CachedLink>();
        int cachedVersion = -1;

        public override void Unload()
        {
            cache.Clear();
            cachedVersion = -1;
        }

        public override void OnWorldUnload()
        {
            cache.Clear();
            cachedVersion = -1;
        }

        // ---------------------------------------------------------------- the seam

        // THE one drawing method. P9 (living wire) replaces its body; the callers do not change. P6's pending-link hologram calls it too
        // (from the start point to the cursor, invalid = true for red). 'from' and 'to' are world pixels. The caller has already begun
        // spriteBatch with the game view matrix (see BeginBatch). The colour is the wire type's tint multiplied by the light at the midpoint.
        public void DrawWire(SpriteBatch spriteBatch, Vector2 from, Vector2 to, CustomWireType wire, bool invalid = false)
        {
            Vector2 edge = to - from;
            float length = edge.Length();
            if (length < 0.5f)
                return;

            Vector2 mid = (from + to) * 0.5f;
            int tileX = Math.Clamp((int)(mid.X / 16f), 0, Main.maxTilesX - 1);
            int tileY = Math.Clamp((int)(mid.Y / 16f), 0, Main.maxTilesY - 1);
            Color light = Lighting.GetColor(tileX, tileY);
            Color tint = invalid ? InvalidColor : GetWireColor(wire);
            Color color = new Color(tint.R * light.R / 255, tint.G * light.G / 255, tint.B * light.B / 255);

            // MagicPixel is not 1x1 (see the project gotchas), so the source rectangle is always the single top-left pixel.
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, from - Main.screenPosition, new Rectangle(0, 0, 1, 1), color,
                (float)Math.Atan2(edge.Y, edge.X), new Vector2(0f, 0.5f), new Vector2(length, WireThickness), SpriteEffects.None, 0f);
        }

        public Color GetWireColor(CustomWireType wire)
        {
            if (wire.HasFlag(CustomWireType.Gold))
                return GoldColor;
            if (wire.HasFlag(CustomWireType.Tin))
                return TinColor;
            if (wire.HasFlag(CustomWireType.Copper))
                return CopperColor;

            return Color.Gray;
        }

        // The same batch settings the crossboards use (world space through the game view matrix, so zoom works).
        public static void BeginBatch(SpriteBatch spriteBatch)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // ---------------------------------------------------------------- finished links

        // Called by PowerPoleCrossboardSystem.PostDrawTiles before the crossboards are drawn. Begins and ends its own batch, and only when
        // at least one wire is on screen.
        public void DrawLinks(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            PoleLinkSystem links = PoleLinkSystem.Instance;
            if (links.Links.Count == 0)
                return;

            if (cachedVersion != links.Version || cache.Count != links.Links.Count)
                RebuildCache(links);

            CustomWireSystem.GetVisibleTileRect(out int minX, out int minY, out int maxX, out int maxY);
            var visible = new Rectangle(minX * 16, minY * 16, (maxX - minX + 1) * 16, (maxY - minY + 1) * 16);

            PowerPoleCrossboardSystem sway = ModContent.GetInstance<PowerPoleCrossboardSystem>();
            PoleLink cutHover = ModContent.GetInstance<PoleLinkInputSystem>().CutHoverLink;   // P7b: the link a held cutter would cut, drawn in the invalid colour
            bool batchBegun = false;

            foreach (CachedLink entry in cache)
            {
                if (!entry.Resolved && !Resolve(entry))
                    continue;

                if (!entry.Bounds.Intersects(visible))
                    continue;

                PoleLink link = entry.Link;
                if (!TryGetDrawablePole(link.A, link.SlotA, out PowerPoleTileEntityBase poleA) || !TryGetDrawablePole(link.B, link.SlotB, out PowerPoleTileEntityBase poleB))
                    continue;

                Vector2 from = GetSwayedPoint(sway, poleA, link.SlotA);
                Vector2 to = GetSwayedPoint(sway, poleB, link.SlotB);

                if (!batchBegun)
                {
                    BeginBatch(spriteBatch);
                    batchBegun = true;
                }

                DrawWire(spriteBatch, from, to, link.Wire, cutHover != null && ReferenceEquals(link, cutHover));
            }

            if (batchBegun)
                spriteBatch.End();
        }

        public static Vector2 GetSwayedPoint(PowerPoleCrossboardSystem sway, PowerPoleTileEntityBase pole, int slot)
        {
            int boardIndex = pole.GetLinkPointBoardIndex(slot, out _);
            return pole.GetLinkPointWorldSwayed(slot, sway.GetSwayAngle(pole.Position, boardIndex, pole.SwayScale));
        }

        // The pole's entity must exist on this client, its tile must still be there (the same test the crossboard drawing uses, so a wire
        // never hangs from a board that is not drawn), and the slot must exist on it.
        static bool TryGetDrawablePole(Point16 corner, int slot, out PowerPoleTileEntityBase pole)
        {
            if (!PoleLinkSystem.TryGetPole(corner, out pole))
                return false;

            Tile tile = Framing.GetTileSafely(corner.X, corner.Y);
            return tile.HasTile && tile.TileType == pole.ValidTileType && slot < pole.LinkPointCount;
        }

        void RebuildCache(PoleLinkSystem links)
        {
            cache.Clear();
            foreach (PoleLink link in links.Links)
            {
                var entry = new CachedLink { Link = link };
                Resolve(entry);
                cache.Add(entry);
            }

            cachedVersion = links.Version;
        }

        // Fills in the culling rectangle. False (and the entry stays unresolved) while either pole has no entity here or has no such slot.
        static bool Resolve(CachedLink entry)
        {
            PoleLink link = entry.Link;
            if (!PoleLinkSystem.TryGetPole(link.A, out PowerPoleTileEntityBase poleA) || !PoleLinkSystem.TryGetPole(link.B, out PowerPoleTileEntityBase poleB))
                return false;

            if (link.SlotA >= poleA.LinkPointCount || link.SlotB >= poleB.LinkPointCount)
                return false;

            PoleWireDrawSystem self = ModContent.GetInstance<PoleWireDrawSystem>();
            Vector2 a = poleA.GetLinkPointWorld(link.SlotA);
            Vector2 b = poleB.GetLinkPointWorld(link.SlotB);
            float radiusA = GetRadius(poleA, link.SlotA);
            float radiusB = GetRadius(poleB, link.SlotB);
            float margin = self.CullMarginPixels + self.WireThickness;

            float left = Math.Min(a.X - radiusA, b.X - radiusB) - margin;
            float top = Math.Min(a.Y - radiusA, b.Y - radiusB) - margin;
            float right = Math.Max(a.X + radiusA, b.X + radiusB) + margin;
            float bottom = Math.Max(a.Y + radiusA, b.Y + radiusB) + margin;

            entry.Bounds = new Rectangle((int)Math.Floor(left), (int)Math.Floor(top), (int)Math.Ceiling(right - left) + 1, (int)Math.Ceiling(bottom - top) + 1);
            entry.Resolved = true;
            return true;
        }

        // How far a link point can move when its board rotates: its distance from the board's centre.
        static float GetRadius(PowerPoleTileEntityBase pole, int slot)
        {
            PoleBoard board = pole.Boards[pole.GetLinkPointBoardIndex(slot, out int point)];
            return (board.LinkPoints[point] - board.Pivot).Length();
        }
    }
}