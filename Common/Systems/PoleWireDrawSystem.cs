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
    // Client only (power poles patches P5 and P9). Draws every finished pole link as a hanging, pixel-snapped curve (P9: a simulated rope, see
    // PoleRope, that sags, reacts to wind and to movers, and only exists while the link is on screen) between its two SWAYED connection points,
    // so the wire follows the swaying crossboards. Nothing here is saved or sent: it only reads the link replica (PoleLinkSystem) and the
    // pole entities, and the server never draws.
    //
    // Draw order: there is no PostDrawTiles of its own. PowerPoleCrossboardSystem.PostDrawTiles calls DrawLinks as its FIRST step (changed
    // after the P3-P6 test), so the wires land on top of the overhang canvases (MachineOverhangSystem, which sorts before it) but BEHIND the
    // crossboards, which cover the wire ends. That does not depend on the load order of ModSystems.
    //
    // Links and span checks use the RESTING points (PowerPoleTileEntityBase.GetLinkPointWorld); only the drawing uses the swayed ones.
    public partial class PoleWireDrawSystem : ModSystem
    {
        // Tunables (plain instance fields, so hot reload can change them).
        public float WireThickness = 2f;
        public float CullMarginPixels = 4f;
        public Color CopperColor = new Color(183, 88, 25);     // sampled from CopperWireTile.png
        public Color TinColor = new Color(85, 91, 75);      // sampled from TinWireTile.png
        public Color GoldColor = new Color(203, 179, 73);     // sampled from GoldWireTile.png
        public Color InvalidColor = new Color(255, 70, 70);    // same red as the pole placement hologram

        // P9 living wire. Plain instance fields so hot reload can tweak them (WireThickness above is also the pixel size of the snapped curve).
        public bool Simulate = true;               // false: every wire just hangs in its resting curve (no stepping at all)
        public bool Disturbance = true;            // players and NPCs moving through a wire push it
        public float NodeSpacing = 12f;            // pixels between rope nodes
        public int MaxNodes = 64;
        public float Gravity = 0.1f;               // pixels per tick squared (low on purpose: a Verlet rope stretches more under strong gravity, and a stretched wire sags too far)
        public float Damping = 0.985f;             // velocity kept per tick
        public float WindForce = 0.01f;            // sideways pixels per tick squared at Main.windSpeedCurrent = 1
        public float GustAmount = 0.3f;            // how much the wind strength ripples along the wire (0 = steady)
        public float GustSpeed = 2.2f;             // radians per second
        public float FlutterAmount = 8f;           // vertical ripple, as a multiple of the wind push (0 = the wind only pushes sideways)
        public int ConstraintIterations = 20;      // each iteration is a forward and a backward sweep
        public float DisturbRadius = 6f;           // pixels around a mover's hitbox that still push the wire
        public float DisturbStrength = 0.25f;      // 0..1, how fast a pushed node takes the mover's velocity
        public float MaxNodeSpeed = 8f;            // pixels per tick cap on a pushed node

        // Per-link data that only changes when the link graph changes (PoleLinkSystem.Version): a pixel rectangle that contains the wire
        // wherever the crossboards swing (resting points grown by each point's distance from its board's centre), so culling needs no
        // entity lookups. A link whose poles are missing (or whose slot does not exist) on this client is "unresolved": it is skipped
        // and retried every frame until the entities show up (join order, V8).
        class CachedLink
        {
            public PoleLink Link;
            public Rectangle Bounds;        // the chord box grown by Reach (so it also holds the sag and the swing)
            public bool Resolved;
            public Vector2 RestA;           // P9: the resting link points, for the segment-against-screen test
            public Vector2 RestB;
            public float RestChord;
            public float Reach;             // radius of the wider board + cull margin + SagReach: how far the wire can be from its chord
            public PoleRope Rope;           // P9: null while the link is off screen (nothing is simulated or kept for it)
            public bool BothOn;             // P9b: both poles were on at the last tick (the sparks start when this turns true)
        }

        readonly List<CachedLink> cache = new List<CachedLink>();
        readonly Dictionary<PoleLink, CachedLink> byLink = new Dictionary<PoleLink, CachedLink>();
        readonly List<PoleDisturber> disturbers = new List<PoleDisturber>();
        Vector2[] scratch = new Vector2[0];
        int cachedVersion = -1;

        public override void Unload()
        {
            cache.Clear();
            byLink.Clear();
            disturbers.Clear();
            scratch = new Vector2[0];
            cachedVersion = -1;
        }

        public override void OnWorldUnload()
        {
            cache.Clear();
            byLink.Clear();
            cachedVersion = -1;
        }

        // ---------------------------------------------------------------- the seam

        // THE one drawing method. P9 (living wire) replaces its body; the callers do not change. P6's pending-link hologram calls it too
        // (from the start point to the cursor, invalid = true for red). 'from' and 'to' are world pixels. The caller has already begun
        // spriteBatch with the game view matrix (see BeginBatch). The colour is the wire type's tint multiplied by the light at the midpoint.
        public void DrawWire(SpriteBatch spriteBatch, Vector2 from, Vector2 to, CustomWireType wire, bool invalid = false)
        {
            float chord = Vector2.Distance(from, to);
            if (chord < 0.5f)
                return;

            // P9: the same resting hang the rope starts from (no simulation), so the hologram shows how the wire will hang.
            float length = chord + PoleLinkSystem.Instance.SlackExtra(chord);
            int count = PoleRope.NodeCount(length, NodeSpacing, MaxNodes);
            if (scratch.Length < count)
                scratch = new Vector2[Math.Max(count, MaxNodes)];

            PoleRope.FillRestCurve(scratch, count, from, to, length);
            DrawCurve(spriteBatch, scratch, count, wire, invalid);
        }

        // The wire's colour: its type's tint multiplied by the light at 'point' (a world position on the wire).
        Color GetLitColor(CustomWireType wire, bool invalid, Vector2 point)
        {
            int tileX = Math.Clamp((int)(point.X / 16f), 0, Main.maxTilesX - 1);
            int tileY = Math.Clamp((int)(point.Y / 16f), 0, Main.maxTilesY - 1);
            Color light = Lighting.GetColor(tileX, tileY);
            Color tint = invalid ? InvalidColor : GetWireColor(wire);
            return new Color(tint.R * light.R / 255, tint.G * light.G / 255, tint.B * light.B / 255);
        }

        // Draws a polyline snapped to a grid of WireThickness-sized world cells (so it looks like pixel art, and moves in whole pixels).
        // It walks the line in steps of at most 1 pixel, so consecutive cells always touch, and merges horizontal runs into one rectangle.
        // MagicPixel is not 1x1 (see the project gotchas), so the source rectangle is always the single top-left pixel.
        void DrawCurve(SpriteBatch spriteBatch, Vector2[] points, int count, CustomWireType wire, bool invalid)
        {
            if (count < 2)
                return;

            Color color = GetLitColor(wire, invalid, points[count / 2]);
            float cell = Math.Max(1f, WireThickness);
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            bool haveRun = false;
            int runY = 0, runMin = 0, runMax = 0;
            int lastX = int.MinValue, lastY = int.MinValue;

            for (int i = 0; i < count - 1; i++)
            {
                Vector2 start = points[i];
                Vector2 edge = points[i + 1] - start;
                int steps = Math.Max(1, (int)Math.Ceiling(edge.Length()));

                for (int s = i == 0 ? 0 : 1; s <= steps; s++)
                {
                    Vector2 p = start + edge * (s / (float)steps);
                    int cellX = (int)Math.Floor(p.X / cell);
                    int cellY = (int)Math.Floor(p.Y / cell);
                    if (cellX == lastX && cellY == lastY)
                        continue;

                    lastX = cellX;
                    lastY = cellY;

                    if (haveRun && cellY == runY)
                    {
                        if (cellX == runMax + 1) { runMax = cellX; continue; }
                        if (cellX == runMin - 1) { runMin = cellX; continue; }
                        if (cellX >= runMin && cellX <= runMax) continue;
                    }

                    if (haveRun)
                        DrawRun(spriteBatch, pixel, runMin, runMax, runY, cell, color);

                    haveRun = true;
                    runY = cellY;
                    runMin = cellX;
                    runMax = cellX;
                }
            }

            if (haveRun)
                DrawRun(spriteBatch, pixel, runMin, runMax, runY, cell, color);
        }

        static void DrawRun(SpriteBatch spriteBatch, Texture2D pixel, int minX, int maxX, int cellY, float cell, Color color)
        {
            spriteBatch.Draw(pixel, new Vector2(minX * cell, cellY * cell) - Main.screenPosition, new Rectangle(0, 0, 1, 1), color, 0f,
                Vector2.Zero, new Vector2((maxX - minX + 1) * cell, cell), SpriteEffects.None, 0f);
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

            EnsureCache(links);
            GetVisibleRect(out Rectangle visible);

            PowerPoleCrossboardSystem sway = ModContent.GetInstance<PowerPoleCrossboardSystem>();
            PoleLink cutHover = ModContent.GetInstance<PoleLinkInputSystem>().CutHoverLink;   // P7b: the link a held cutter would cut, drawn in the invalid colour
            bool batchBegun = false;

            foreach (CachedLink entry in cache)
            {
                // P9: the same visibility test the simulation uses. A link that fails it has no rope, so nothing is kept for it.
                if (!IsVisible(entry, visible))
                {
                    entry.Rope = null;
                    continue;
                }

                PoleLink link = entry.Link;
                if (!TryGetDrawablePole(link.A, link.SlotA, out PowerPoleTileEntityBase poleA) || !TryGetDrawablePole(link.B, link.SlotB, out PowerPoleTileEntityBase poleB))
                    continue;

                Vector2 from = GetSwayedPoint(sway, poleA, link.SlotA);
                Vector2 to = GetSwayedPoint(sway, poleB, link.SlotB);

                PoleRope rope = GetRope(entry, from, to);
                if (Simulate)
                    rope.Pin(from, to);
                else
                    rope.Rest(from, to);

                if (!batchBegun)
                {
                    BeginBatch(spriteBatch);
                    batchBegun = true;
                }

                DrawCurve(spriteBatch, rope.Pos, rope.Count, link.Wire, cutHover != null && ReferenceEquals(link, cutHover));
            }

            if (batchBegun)
                spriteBatch.End();
        }

        // ---------------------------------------------------------------- P9: simulation (client only, only for links on screen)

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu || Main.gamePaused)
                return;

            UpdateSparks();   // P9b: sparks and the zap sound, they do not depend on Simulate

            if (!Simulate)
                return;

            PoleLinkSystem links = PoleLinkSystem.Instance;
            if (links.Links.Count == 0)
                return;

            EnsureCache(links);
            GetVisibleRect(out Rectangle visible);

            PowerPoleCrossboardSystem sway = ModContent.GetInstance<PowerPoleCrossboardSystem>();
            bool collected = false;

            foreach (CachedLink entry in cache)
            {
                if (!IsVisible(entry, visible))
                {
                    entry.Rope = null;
                    continue;
                }

                PoleLink link = entry.Link;
                if (!TryGetDrawablePole(link.A, link.SlotA, out PowerPoleTileEntityBase poleA) || !TryGetDrawablePole(link.B, link.SlotB, out PowerPoleTileEntityBase poleB))
                    continue;

                if (!collected)
                {
                    CollectDisturbers(visible);
                    collected = true;
                }

                Vector2 from = GetSwayedPoint(sway, poleA, link.SlotA);
                Vector2 to = GetSwayedPoint(sway, poleB, link.SlotB);
                GetRope(entry, from, to).Step(this, from, to, disturbers);
            }
        }

        // Moving players and NPCs near the screen (everything that can push a wire this tick).
        void CollectDisturbers(Rectangle visible)
        {
            disturbers.Clear();
            if (!Disturbance)
                return;

            int grow = (int)Math.Ceiling(DisturbRadius);
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player.active && !player.dead && !player.ghost)
                    TryAddDisturber(player.Hitbox, player.velocity, visible, grow);
            }

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active)
                    TryAddDisturber(npc.Hitbox, npc.velocity, visible, grow);
            }
        }

        void TryAddDisturber(Rectangle hitbox, Vector2 velocity, Rectangle visible, int grow)
        {
            if (velocity.LengthSquared() < 0.04f || !hitbox.Intersects(visible))
                return;

            hitbox.Inflate(grow, grow);
            disturbers.Add(new PoleDisturber { Box = hitbox, Velocity = velocity });
        }

        PoleRope GetRope(CachedLink entry, Vector2 from, Vector2 to)
        {
            if (entry.Rope == null)
            {
                // Born (or reborn after being off screen) in its resting hang, so a wire coming back on screen never pops.
                float length = entry.RestChord + PoleLinkSystem.Instance.SlackExtra(entry.RestChord);
                entry.Rope = new PoleRope(PoleRope.NodeCount(length, NodeSpacing, MaxNodes), length);
                entry.Rope.Rest(from, to);
            }

            return entry.Rope;
        }

        // ---------------------------------------------------------------- P9: the one visibility test (drawing and simulation both use it)

        static void GetVisibleRect(out Rectangle visible)
        {
            CustomWireSystem.GetVisibleTileRect(out int minX, out int minY, out int maxX, out int maxY);
            visible = new Rectangle(minX * 16, minY * 16, (maxX - minX + 1) * 16, (maxY - minY + 1) * 16);
        }

        // False for an unresolved link. Otherwise: the cached box (chord grown by Reach) must touch the screen AND the chord SEGMENT, with the
        // screen grown by Reach, must cross it. The segment test is what culls a long diagonal wire whose box overlaps the screen.
        static bool IsVisible(CachedLink entry, Rectangle visible)
        {
            if (!entry.Resolved && !Resolve(entry))
                return false;

            if (!entry.Bounds.Intersects(visible))
                return false;

            Rectangle area = visible;
            int reach = (int)Math.Ceiling(entry.Reach);
            area.Inflate(reach, reach);
            return SegmentIntersectsRect(entry.RestA, entry.RestB, area);
        }

        // Liang-Barsky clipping of the segment a-b against a rectangle.
        public static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rectangle rect)
        {
            float t0 = 0f;
            float t1 = 1f;
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;

            return Clip(-dx, a.X - rect.Left, ref t0, ref t1) && Clip(dx, rect.Right - a.X, ref t0, ref t1)
                && Clip(-dy, a.Y - rect.Top, ref t0, ref t1) && Clip(dy, rect.Bottom - a.Y, ref t0, ref t1);
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Math.Abs(p) < 0.000001f)
                return q >= 0f;

            float t = q / p;
            if (p < 0f)
            {
                if (t > t1)
                    return false;
                if (t > t0)
                    t0 = t;
            }
            else
            {
                if (t < t0)
                    return false;
                if (t < t1)
                    t1 = t;
            }

            return true;
        }

        void EnsureCache(PoleLinkSystem links)
        {
            if (cachedVersion != links.Version || cache.Count != links.Links.Count)
                RebuildCache(links);
        }

        // P9: how far the cursor is from the wire as it is DRAWN (the live rope when the link is on screen, else its resting hang), for the cutter.
        // restA and restB are the resting link points (what the link logic uses).
        public float DistanceToWire(PoleLink link, Vector2 point, Vector2 restA, Vector2 restB)
        {
            EnsureCache(PoleLinkSystem.Instance);
            if (byLink.TryGetValue(link, out CachedLink entry) && entry.Rope != null)
                return PoleRope.DistanceToPolyline(entry.Rope.Pos, entry.Rope.Count, point);

            float chord = Vector2.Distance(restA, restB);
            float length = chord + PoleLinkSystem.Instance.SlackExtra(chord);
            int count = PoleRope.NodeCount(length, NodeSpacing, MaxNodes);
            if (scratch.Length < count)
                scratch = new Vector2[Math.Max(count, MaxNodes)];

            PoleRope.FillRestCurve(scratch, count, restA, restB, length);
            return PoleRope.DistanceToPolyline(scratch, count, point);
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
            // P9: ropes survive a rebuild (matched by the link object), so cutting or adding one link does not make every other wire snap back.
            var previous = new Dictionary<PoleLink, PoleRope>();
            var wasOn = new HashSet<(Point16, byte, Point16, byte)>();   // P9b: matched by value, so a resynced link object does not spark again
            foreach (CachedLink old in cache)
            {
                if (old.Rope != null)
                    previous[old.Link] = old.Rope;

                if (old.BothOn)
                    wasOn.Add((old.Link.A, old.Link.SlotA, old.Link.B, old.Link.SlotB));
            }

            cache.Clear();
            byLink.Clear();
            foreach (PoleLink link in links.Links)
            {
                var entry = new CachedLink { Link = link };
                if (previous.TryGetValue(link, out PoleRope rope))
                    entry.Rope = rope;

                entry.BothOn = wasOn.Contains((link.A, link.SlotA, link.B, link.SlotB));

                Resolve(entry);
                cache.Add(entry);
                byLink[link] = entry;
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
            float radius = Math.Max(GetRadius(poleA, link.SlotA), GetRadius(poleB, link.SlotB));
            float chord = Vector2.Distance(a, b);
            float reach = radius + self.CullMarginPixels + self.WireThickness + PoleLinkSystem.Instance.SagReach(chord);

            float left = Math.Min(a.X, b.X) - reach;
            float top = Math.Min(a.Y, b.Y) - reach;
            float right = Math.Max(a.X, b.X) + reach;
            float bottom = Math.Max(a.Y, b.Y) + reach;

            entry.Bounds = new Rectangle((int)Math.Floor(left), (int)Math.Floor(top), (int)Math.Ceiling(right - left) + 1, (int)Math.Ceiling(bottom - top) + 1);
            entry.RestA = a;
            entry.RestB = b;
            entry.RestChord = chord;
            entry.Reach = reach;
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