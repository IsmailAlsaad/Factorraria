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
    // Client only (power poles patches P5 and P9). The server owns the real link graph; this system only reads the local replica and pole
    // entities. Rope nodes, actor pushes, drawing and culling are visual state: no packets, no save data and no gameplay decisions.
    // Links, span checks and cut hit-tests continue to use RESTING points. Only these curves use the SWAYED connection points.
    //
    // Draw order: PowerPoleCrossboardSystem.PostDrawTiles calls DrawLinks FIRST, so wires remain behind the crossboards. P9 uses its own
    // pixel-snapped SpriteBatch curve, not Luminance, so there is no extra render layer and no one-frame delay.
    public class PoleWireDrawSystem : ModSystem
    {
        // Tunables (plain instance fields, so hot reload can change them).
        public float PixelSize = 2f;                  // world pixels per snapped wire cell; 2 gives the chosen chunky look
        public float CullMarginPixels = 4f;
        public float PixelsPerNode = 24f;             // rope resolution follows span
        public int MinNodes = 8;
        public int MaxNodes = 48;
        public float MinSagPixels = 6f;
        public float MaxSagPixels = 72f;
        public float SagFraction = 0.065f;            // resting sag as a fraction of endpoint distance
        public float RopeSlack = 0.025f;              // extra segment length on top of the rest curve
        public float MaxNodeOffsetPixels = 48f;       // hard envelope around the rest curve; also used by culling
        public float RopeDamping = 0.94f;
        public float GravityPixelsPerSecondSquared = 65f;
        public float WindPixelsPerSecondSquared = 150f;
        public float GustStrength = 0.3f;
        public float GustSpeed = 2.1f;
        public int ConstraintIterations = 6;
        public float ActorPushRadiusPixels = 28f;
        public float ActorPushStrength = 0.45f;
        public float MaxActorSpeedPixelsPerTick = 12f;
        public Color CopperColor = new Color(183, 88, 25);       // sampled from CopperWireTile.png
        public Color TinColor = new Color(85, 91, 75);           // sampled from TinWireTile.png
        public Color GoldColor = new Color(203, 179, 73);        // sampled from GoldWireTile.png
        public Color InvalidColor = new Color(255, 70, 70);      // same red as the pole placement hologram

        // Per-link visual data. RestA/RestB and the swing radii let the shared culling helper run before any swayed-point work.
        class CachedLink
        {
            public PoleLink Link;
            public Vector2 RestA;
            public Vector2 RestB;
            public float RadiusA;
            public float RadiusB;
            public float Sag;
            public bool Resolved;
            public PoleWireRope Rope;
        }

        readonly List<CachedLink> cache = new List<CachedLink>();
        readonly List<PoleWireActorPush> actors = new List<PoleWireActorPush>();
        int cachedVersion = -1;
        Vector2[] previewNodes = new Vector2[0];

        public override void Unload()
        {
            ClearVisualState();
        }

        public override void OnWorldUnload()
        {
            ClearVisualState();
        }

        void ClearVisualState()
        {
            cache.Clear();
            actors.Clear();
            cachedVersion = -1;
            previewNodes = new Vector2[0];
        }

        // ---------------------------------------------------------------- simulation

        // Simulation runs in the update, never in SpriteBatch drawing. Exactly the same visibility helper is used here and in DrawLinks.
        // An off-screen entry has its rope discarded: no stepping, no actor work and no per-frame allocation. Re-entry creates a rest rope.
        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            PoleLinkSystem links = PoleLinkSystem.Instance;
            if (links.Links.Count == 0)
            {
                cache.Clear();
                actors.Clear();
                cachedVersion = links.Version;
                return;
            }

            EnsureCache(links);
            Rectangle visible = GetVisibleWorldRect();
            CollectActors(visible);
            PowerPoleCrossboardSystem sway = ModContent.GetInstance<PowerPoleCrossboardSystem>();

            foreach (CachedLink entry in cache)
            {
                if (!entry.Resolved && !Resolve(entry))
                    continue;

                if (!IsLinkVisible(entry, visible))
                {
                    entry.Rope = null;
                    continue;
                }

                PoleLink link = entry.Link;
                if (!TryGetDrawablePole(link.A, link.SlotA, out PowerPoleTileEntityBase poleA)
                    || !TryGetDrawablePole(link.B, link.SlotB, out PowerPoleTileEntityBase poleB))
                {
                    entry.Rope = null;
                    continue;
                }

                Vector2 from = GetSwayedPoint(sway, poleA, link.SlotA);
                Vector2 to = GetSwayedPoint(sway, poleB, link.SlotB);
                int nodeCount = GetNodeCount(Vector2.Distance(from, to));
                if (entry.Rope == null || entry.Rope.NodeCount != nodeCount)
                    entry.Rope = new PoleWireRope(from, to, nodeCount, entry.Sag, RopeSlack);

                float phase = link.A.X * 0.73f + link.A.Y * 0.41f + link.B.X * 0.19f;
                float gust = 1f + GustStrength * (float)Math.Sin(Main.GlobalTimeWrappedHourly * GustSpeed + phase);
                float wind = Main.windSpeedCurrent * WindPixelsPerSecondSquared * gust;
                entry.Rope.Step(from, to, entry.Sag, 1f / 60f, RopeDamping, GravityPixelsPerSecondSquared, wind,
                    MaxNodeOffsetPixels, ConstraintIterations, actors, ActorPushRadiusPixels, ActorPushStrength);
            }
        }

        void CollectActors(Rectangle visible)
        {
            actors.Clear();
            int margin = (int)Math.Ceiling(ActorPushRadiusPixels + MaxSagPixels + MaxNodeOffsetPixels + 64f);
            Rectangle nearby = visible;
            nearby.Inflate(margin, margin);

            Player player = Main.LocalPlayer;
            if (player != null && player.active && !player.dead && nearby.Contains((int)player.Center.X, (int)player.Center.Y))
                AddActor(player.Center, player.velocity);

            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && nearby.Contains((int)npc.Center.X, (int)npc.Center.Y))
                    AddActor(npc.Center, npc.velocity);
            }
        }

        void AddActor(Vector2 center, Vector2 velocity)
        {
            float speed = velocity.Length();
            float cap = Math.Max(0.1f, MaxActorSpeedPixelsPerTick);
            if (speed > cap)
                velocity *= cap / speed;

            actors.Add(new PoleWireActorPush(center, velocity));
        }

        // ---------------------------------------------------------------- drawing

        // The pending-link hologram uses the same pixel renderer as a finished rope, with a static rest curve and no simulation.
        // 'from' and 'to' are world pixels; the caller has begun spriteBatch with the game view matrix (see BeginBatch).
        public void DrawWire(SpriteBatch spriteBatch, Vector2 from, Vector2 to, CustomWireType wire, bool invalid = false)
        {
            float length = Vector2.Distance(from, to);
            if (length < 0.5f)
                return;

            int nodeCount = GetNodeCount(length);
            if (previewNodes.Length != nodeCount)
                previewNodes = new Vector2[nodeCount];

            float sag = GetSag(length);
            for (int i = 0; i < nodeCount; i++)
                previewNodes[i] = PoleWireRope.RestPosition(from, to, sag, i, nodeCount);

            DrawNodes(spriteBatch, previewNodes, wire, invalid);
        }

        void DrawNodes(SpriteBatch spriteBatch, IReadOnlyList<Vector2> nodes, CustomWireType wire, bool invalid)
        {
            for (int i = 0; i < nodes.Count - 1; i++)
                DrawPixelSegment(spriteBatch, nodes[i], nodes[i + 1], wire, invalid);
        }

        void DrawPixelSegment(SpriteBatch spriteBatch, Vector2 from, Vector2 to, CustomWireType wire, bool invalid)
        {
            float grid = Math.Max(1f, PixelSize);
            Vector2 edge = to - from;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(edge.X), Math.Abs(edge.Y)) / grid));
            Vector2 previousCell = new Vector2(float.NaN, float.NaN);
            Color color = GetLitWireColor((from + to) * 0.5f, wire, invalid);

            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = from + edge * (i / (float)steps);
                Vector2 cell = new Vector2((float)Math.Floor(point.X / grid) * grid, (float)Math.Floor(point.Y / grid) * grid);
                if (cell == previousCell)
                    continue;

                previousCell = cell;
                // MagicPixel is not 1x1 (see the project gotchas), so always sample its single top-left pixel.
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, cell - Main.screenPosition, new Rectangle(0, 0, 1, 1), color,
                    0f, Vector2.Zero, new Vector2(grid, grid), SpriteEffects.None, 0f);
            }
        }

        Color GetLitWireColor(Vector2 world, CustomWireType wire, bool invalid)
        {
            int tileX = Math.Clamp((int)(world.X / 16f), 0, Main.maxTilesX - 1);
            int tileY = Math.Clamp((int)(world.Y / 16f), 0, Main.maxTilesY - 1);
            Color light = Lighting.GetColor(tileX, tileY);
            Color tint = invalid ? InvalidColor : GetWireColor(wire);
            return new Color(tint.R * light.R / 255, tint.G * light.G / 255, tint.B * light.B / 255);
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

        // Called by PowerPoleCrossboardSystem.PostDrawTiles before the crossboards are drawn. No simulation happens here.
        public void DrawLinks(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            PoleLinkSystem links = PoleLinkSystem.Instance;
            if (links.Links.Count == 0)
                return;

            EnsureCache(links);
            Rectangle visible = GetVisibleWorldRect();
            PowerPoleCrossboardSystem sway = ModContent.GetInstance<PowerPoleCrossboardSystem>();
            PoleLink cutHover = ModContent.GetInstance<PoleLinkInputSystem>().CutHoverLink;
            bool batchBegun = false;

            foreach (CachedLink entry in cache)
            {
                if (!entry.Resolved && !Resolve(entry))
                    continue;

                if (!IsLinkVisible(entry, visible))
                {
                    entry.Rope = null;
                    continue;
                }

                PoleLink link = entry.Link;
                if (!TryGetDrawablePole(link.A, link.SlotA, out PowerPoleTileEntityBase poleA)
                    || !TryGetDrawablePole(link.B, link.SlotB, out PowerPoleTileEntityBase poleB))
                {
                    entry.Rope = null;
                    continue;
                }

                Vector2 from = GetSwayedPoint(sway, poleA, link.SlotA);
                Vector2 to = GetSwayedPoint(sway, poleB, link.SlotB);
                int nodeCount = GetNodeCount(Vector2.Distance(from, to));
                if (entry.Rope == null || entry.Rope.NodeCount != nodeCount)
                    entry.Rope = new PoleWireRope(from, to, nodeCount, entry.Sag, RopeSlack);
                else
                    entry.Rope.PinEndpoints(from, to);

                if (!batchBegun)
                {
                    BeginBatch(spriteBatch);
                    batchBegun = true;
                }

                DrawNodes(spriteBatch, entry.Rope.Nodes, link.Wire, cutHover != null && ReferenceEquals(link, cutHover));
            }

            if (batchBegun)
                spriteBatch.End();
        }

        public static Vector2 GetSwayedPoint(PowerPoleCrossboardSystem sway, PowerPoleTileEntityBase pole, int slot)
        {
            int boardIndex = pole.GetLinkPointBoardIndex(slot, out _);
            return pole.GetLinkPointWorldSwayed(slot, sway.GetSwayAngle(pole.Position, boardIndex, pole.SwayScale));
        }

        // ---------------------------------------------------------------- shared culling and cache

        static Rectangle GetVisibleWorldRect()
        {
            CustomWireSystem.GetVisibleTileRect(out int minX, out int minY, out int maxX, out int maxY);
            return new Rectangle(minX * 16, minY * 16, (maxX - minX + 1) * 16, (maxY - minY + 1) * 16);
        }

        // ONE visibility rule for simulation and drawing. A box-only test keeps long diagonal wires alive when their box touches the
        // screen, so this also clips the endpoint segment against the visible rectangle. The rectangle is grown by every possible visual
        // displacement (board swing, rest sag, actor/wind offset and the pixel cell), making the test conservative around a sagging rope.
        bool IsLinkVisible(CachedLink entry, Rectangle visible)
        {
            float swing = Math.Max(entry.RadiusA, entry.RadiusB);
            int margin = (int)Math.Ceiling(CullMarginPixels + Math.Max(1f, PixelSize) + swing + entry.Sag + MaxNodeOffsetPixels);
            Rectangle expanded = visible;
            expanded.Inflate(margin, margin);
            return SegmentIntersectsRect(entry.RestA, entry.RestB, expanded);
        }

        static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rectangle rect)
        {
            float left = rect.Left;
            float right = rect.Right;
            float top = rect.Top;
            float bottom = rect.Bottom;
            float start = 0f;
            float end = 1f;
            Vector2 edge = b - a;
            return ClipSegment(-edge.X, a.X - left, ref start, ref end)
                && ClipSegment(edge.X, right - a.X, ref start, ref end)
                && ClipSegment(-edge.Y, a.Y - top, ref start, ref end)
                && ClipSegment(edge.Y, bottom - a.Y, ref start, ref end);
        }

        // Liang-Barsky segment/AABB clipping, with no allocations.
        static bool ClipSegment(float direction, float distance, ref float start, ref float end)
        {
            if (Math.Abs(direction) < 0.000001f)
                return distance >= 0f;

            float t = distance / direction;
            if (direction < 0f)
            {
                if (t > end)
                    return false;
                if (t > start)
                    start = t;
            }
            else
            {
                if (t < start)
                    return false;
                if (t < end)
                    end = t;
            }

            return start <= end;
        }

        int GetNodeCount(float length)
        {
            int minimum = Math.Max(2, MinNodes);
            int maximum = Math.Max(minimum, MaxNodes);
            int count = (int)Math.Ceiling(length / Math.Max(1f, PixelsPerNode)) + 1;
            return Math.Clamp(count, minimum, maximum);
        }

        float GetSag(float length)
        {
            return Math.Min(MaxSagPixels, Math.Max(MinSagPixels, length * SagFraction));
        }

        static bool TryGetDrawablePole(Point16 corner, int slot, out PowerPoleTileEntityBase pole)
        {
            if (!PoleLinkSystem.TryGetPole(corner, out pole))
                return false;

            Tile tile = Framing.GetTileSafely(corner.X, corner.Y);
            return tile.HasTile && tile.TileType == pole.ValidTileType && slot < pole.LinkPointCount;
        }

        void EnsureCache(PoleLinkSystem links)
        {
            if (cachedVersion != links.Version || cache.Count != links.Links.Count)
                RebuildCache(links);
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

        static bool Resolve(CachedLink entry)
        {
            PoleLink link = entry.Link;
            if (!PoleLinkSystem.TryGetPole(link.A, out PowerPoleTileEntityBase poleA)
                || !PoleLinkSystem.TryGetPole(link.B, out PowerPoleTileEntityBase poleB))
                return false;

            if (link.SlotA >= poleA.LinkPointCount || link.SlotB >= poleB.LinkPointCount)
                return false;

            PoleWireDrawSystem self = ModContent.GetInstance<PoleWireDrawSystem>();
            entry.RestA = poleA.GetLinkPointWorld(link.SlotA);
            entry.RestB = poleB.GetLinkPointWorld(link.SlotB);
            entry.RadiusA = GetRadius(poleA, link.SlotA);
            entry.RadiusB = GetRadius(poleB, link.SlotB);
            entry.Sag = self.GetSag(Vector2.Distance(entry.RestA, entry.RestB));
            entry.Resolved = true;
            return true;
        }

        static float GetRadius(PowerPoleTileEntityBase pole, int slot)
        {
            PoleBoard board = pole.Boards[pole.GetLinkPointBoardIndex(slot, out int point)];
            return (board.LinkPoints[point] - board.Pivot).Length();
        }
    }
}
