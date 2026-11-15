using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.SuperWeapons
{
    // The stone shield breaking apart (super weapons patch S6.2). CLIENT ONLY and cosmetic: SuperWeaponNet.PlayLocal (ShieldBreak) calls Start on
    // every client (and in single-player); the server never runs it, and there is no new packet.
    //
    // "Dynamic gore": Terraria's Gore needs textures registered at load time, so it cannot show a texture chopped up at runtime. Instead this
    // system cuts the real shield art (WallShield_Body / _Mouth / _Eye, with the frame the wall is showing at that moment) into jittered
    // rectangular pieces, and every piece becomes a shard that is thrown away from the impact point, spins, falls, trails stone dust and fades.
    // A missing PNG still works: the pieces are then cut from the grey stand-in block. On top of the shards: a burst of stone dust and smoke at
    // the impact, and a "curtain" of dust that keeps falling down the wall's body for a while.
    //
    // The tunables are instance fields so hot reload can change them.
    public class WallShieldBreakFxSystem : ModSystem
    {
        /// <summary>Most shards alive at once (the body is cut into one batch per visible row, so this caps the cost).</summary>
        public int MaxShards = 1200;

        /// <summary>Shard life, in ticks (random between the two).</summary>
        public int ShardLifeMin = 80;
        public int ShardLifeMax = 150;

        /// <summary>Downward pull per tick, in pixels per tick squared.</summary>
        public float ShardGravity = 0.3f;

        /// <summary>Launch speed away from the impact, in pixels per tick (random between the two; far pieces get a bit less).</summary>
        public float ShardSpeedMin = 5f;
        public float ShardSpeedMax = 14f;

        /// <summary>Extra push away from the side the wall faces (the side the missile came from), in pixels per tick.</summary>
        public float BackwardBias = 3.5f;

        /// <summary>Largest spin, in radians per tick.</summary>
        public float MaxSpin = 0.22f;

        /// <summary>How many cracks run out from the impact point (the "spokes" of the spider web).</summary>
        public int CrackSpokes = 28;

        /// <summary>How many rings of cracks circle the impact point, the radius of the first one, and how much bigger each next ring is (pixels, factor).</summary>
        public int CrackRings = 12;
        public float CrackFirstRing = 30f;
        public float CrackRingGrowth = 1.38f;

        /// <summary>How crooked every crack is: the longest sideways kink (pixels) and the distance between kinks.</summary>
        public float CrackJag = 6f;
        public float CrackJagStep = 8f;

        /// <summary>Where a piece meets the edge of the art (the body's sides and the seams between its rows, the sides of the mouth and eyes): the deepest bite (pixels) and the distance between bites.</summary>
        public float EdgeJag = 5f;
        public float EdgeJagStep = 8f;

        /// <summary>How long the dust curtain keeps falling down the body, in ticks.</summary>
        public int CurtainTicks = 80;

        class Shard
        {
            public Texture2D Texture;   // null = the grey stand-in block
            public Vector2[] Local;     // the polygon's corners, relative to Position, in unscaled unrotated screen space (already mirrored when the art is)
            public Vector2[] Uv;        // the same corners as pixel positions in Texture
            public int[] Triangles;     // the piece cut into triangles (indices into Local / Uv), so a jagged outline can be concave
            public Vector2 Position;    // the polygon's centre, world pixels
            public Vector2 Velocity;
            public float Rotation;
            public float Spin;
            public float Scale;
            public int Age;
            public int Life;
        }

        class Curtain
        {
            public float X;
            public float Width;
            public int Age;
        }

        readonly List<Shard> shards = new List<Shard>();
        readonly List<Curtain> curtains = new List<Curtain>();

        public override void OnWorldLoad() => Clear();

        public override void OnWorldUnload() => Clear();

        void Clear()
        {
            shards.Clear();
            curtains.Clear();
        }

        /// <summary>Client / single-player: the shield broke at this world position (the mouth's centre).</summary>
        public void Start(Vector2 impact)
        {
            if (Main.dedServ)
                return;

            jagSeed = Main.rand.Next();
            ImpactBurst(impact);
            BuildWeb(impact);

            NPC mouth = FindMouth(impact);
            if (mouth == null)
                return;

            WallShieldDrawSystem art = ModContent.GetInstance<WallShieldDrawSystem>();
            float facing = mouth.direction >= 0 ? 1f : -1f;
            Vector2 bias = new Vector2(-facing * BackwardBias, -1.5f);

            SpawnBody(art.BodyArt, mouth, impact, bias);
            SpawnSheet(art.MouthArt, mouth, impact, bias);
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC eye = Main.npc[i];
                if (eye.active && eye.type == NPCID.WallofFleshEye)
                    SpawnSheet(art.EyeArt, eye, impact, bias);
            }

            float x = mouth.position.X;
            if (mouth.direction > 0)
                x -= 80f;

            curtains.Add(new Curtain { X = x, Width = art.BodyArt != null ? art.BodyArt.Width : 190f });
        }

        static NPC FindMouth(Vector2 near)
        {
            NPC best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != NPCID.WallofFlesh)
                    continue;

                float distance = Vector2.DistanceSquared(npc.Center, near);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            return best;
        }

        // The cloud that goes up the moment the stone cracks: a ring of stone dust and a lot of smoke from the mouth.
        void ImpactBurst(Vector2 at)
        {
            for (int i = 0; i < 160; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(4f, 18f);
                Dust stone = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(60f, 60f), DustID.Stone, velocity, 0, default, Main.rand.NextFloat(1.6f, 3f));
                stone.noGravity = Main.rand.NextBool(3);
            }

            for (int i = 0; i < 70; i++)
            {
                Dust smoke = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(80f, 80f), DustID.Smoke, Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(1f, 8f), 120, default, Main.rand.NextFloat(2f, 3.4f));
                smoke.noGravity = true;
            }
        }

        // ---- Glass-style fracture ----
        // Real glass hit at one spot breaks into a spider web: cracks run straight out from the impact (radial) and other cracks circle it
        // (rings), pieces are tiny near the impact and grow with distance, and every crack is crooked. BuildWeb makes that web once, in world
        // space, centred on the impact; every region (a body row, the mouth, an eye) then clips the same web to its own art, so the cracks line
        // up from one part of the wall to the next. Every piece is a polygon drawn as a fan of triangles.

        List<Vector2[]> web = new List<Vector2[]>();
        int jagSeed;    // new for every break, so the edge bites along the body seams differ each time but match between neighbouring rows

        static float PolygonArea(List<Vector2> poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Count];
                area += a.X * b.Y - b.X * a.Y;
            }

            return Math.Abs(area) * 0.5f;
        }

        static Vector2 PolygonCentre(List<Vector2> poly)
        {
            float area = 0f;
            Vector2 sum = Vector2.Zero;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Count];
                float cross = a.X * b.Y - b.X * a.Y;
                area += cross;
                sum += (a + b) * cross;
            }

            if (Math.Abs(area) < 0.001f)
                return poly[0];

            return sum / (3f * area);
        }

        // A crooked line from a to b: a saw tooth of points pushed sideways. Both neighbours of a crack use the SAME line, so no gaps.
        // Every crack longer than a few pixels gets at least one kink, and long ones get one every `step` pixels (up to 14), so no piece
        // edge stays a straight line. `amount` is the deepest kink; it is also kept under 60% of the spacing between kinks.
        static Vector2[] Jagged(Vector2 a, Vector2 b, float step, float amount)
        {
            float length = Vector2.Distance(a, b);
            if (length < 0.01f)
                return new[] { a };

            int inner = (int)(length / Math.Max(2f, step));
            if (length > 4f)
                inner = Math.Max(inner, 1);
            inner = Math.Min(inner, 14);
            Vector2[] line = new Vector2[inner + 2];
            line[0] = a;
            line[inner + 1] = b;
            if (inner == 0)
                return line;

            Vector2 direction = (b - a) / length;
            Vector2 normal = new Vector2(-direction.Y, direction.X);
            float spacing = length / (inner + 1);
            float max = Math.Min(amount, spacing * 0.6f);
            float sign = Main.rand.NextBool() ? 1f : -1f;
            for (int k = 1; k <= inner; k++)
            {
                float t = (k + Main.rand.NextFloat(-0.25f, 0.25f)) / (inner + 1);
                sign = Main.rand.NextBool(5) ? sign : -sign;   // mostly alternating sides, now and then not
                line[k] = a + (b - a) * t + normal * (sign * max * Main.rand.NextFloat(0.4f, 1f));
            }

            return line;
        }

        // Adds a crack to a piece's outline, leaving out its last point (the next crack starts there).
        static void AddChain(List<Vector2> outline, Vector2[] chain, bool reversed)
        {
            if (reversed)
            {
                for (int i = chain.Length - 1; i > 0; i--)
                    outline.Add(chain[i]);
            }
            else
            {
                for (int i = 0; i < chain.Length - 1; i++)
                    outline.Add(chain[i]);
            }
        }

        void BuildWeb(Vector2 impact)
        {
            web.Clear();
            int spokes = Math.Max(5, CrackSpokes);
            int rings = Math.Max(2, CrackRings);
            float stepAngle = MathHelper.TwoPi / spokes;

            float[] spokeAngle = new float[spokes];
            for (int s = 0; s < spokes; s++)
                spokeAngle[s] = (s + Main.rand.NextFloat(-0.3f, 0.3f)) * stepAngle;

            // corners of the web: ring 0 is the impact itself, ring r sits at FirstRing * Growth^(r-1), each corner wobbled
            Vector2[][] corner = new Vector2[rings + 1][];
            for (int r = 0; r <= rings; r++)
            {
                corner[r] = new Vector2[spokes];
                float radius = r == 0 ? 0f : CrackFirstRing * (float)Math.Pow(CrackRingGrowth, r - 1);
                for (int s = 0; s < spokes; s++)
                {
                    if (r == 0)
                    {
                        corner[r][s] = impact;
                        continue;
                    }

                    float angle = spokeAngle[s] + Main.rand.NextFloat(-0.08f, 0.08f) * stepAngle;
                    corner[r][s] = impact + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius * Main.rand.NextFloat(0.85f, 1.15f);
                }
            }

            // the cracks: radial[r][s] goes outward from corner[r][s] to corner[r + 1][s], ring[r][s] goes round from corner[r][s] to corner[r][s + 1]
            Vector2[][][] radial = new Vector2[rings][][];
            for (int r = 0; r < rings; r++)
            {
                radial[r] = new Vector2[spokes][];
                for (int s = 0; s < spokes; s++)
                    radial[r][s] = Jagged(corner[r][s], corner[r + 1][s], CrackJagStep, Math.Min(CrackJag, 0.22f * Vector2.Distance(corner[r + 1][s], corner[r + 1][(s + 1) % spokes])));
            }

            Vector2[][][] ring = new Vector2[rings + 1][][];
            for (int r = 0; r <= rings; r++)
            {
                ring[r] = new Vector2[spokes][];
                for (int s = 0; s < spokes; s++)
                    ring[r][s] = r == 0 ? new[] { impact } : Jagged(corner[r][s], corner[r][(s + 1) % spokes], CrackJagStep, Math.Min(CrackJag, 0.22f * Vector2.Distance(corner[r][s], corner[r < rings ? r + 1 : r - 1][s])));
            }

            // a piece is the area between two neighbouring spokes and two neighbouring rings (the innermost ones are wedges)
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < spokes; s++)
                {
                    int next = (s + 1) % spokes;
                    List<Vector2> outline = new List<Vector2>();
                    AddChain(outline, ring[r][s], false);
                    AddChain(outline, radial[r][next], false);
                    AddChain(outline, ring[r + 1][s], true);
                    AddChain(outline, radial[r][s], true);
                    if (outline.Count >= 3)
                        web.Add(outline.ToArray());
                }
            }
        }

        // Sutherland-Hodgman: keeps the part of a polygon on one side of an axis-aligned line.
        static List<Vector2> ClipAxis(List<Vector2> input, bool xAxis, float limit, bool keepAbove)
        {
            List<Vector2> output = new List<Vector2>();
            for (int i = 0; i < input.Count; i++)
            {
                Vector2 current = input[i];
                Vector2 previous = input[(i + input.Count - 1) % input.Count];
                float dc = (xAxis ? current.X : current.Y) - limit;
                float dp = (xAxis ? previous.X : previous.Y) - limit;
                if (!keepAbove)
                {
                    dc = -dc;
                    dp = -dp;
                }

                bool currentIn = dc >= 0f;
                bool previousIn = dp >= 0f;
                if (currentIn != previousIn)
                    output.Add(previous + (current - previous) * (dp / (dp - dc)));
                if (currentIn)
                    output.Add(current);
            }

            return output;
        }

        static float Hash01(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u + (uint)c * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        // Which edge of the art rectangle the polygon edge a-b lies on: 0 left, 1 right, 2 top, 3 bottom, -1 none (a crack).
        static int BoundarySide(Vector2 a, Vector2 b, float width, float height)
        {
            if (Math.Abs(a.X) < 0.05f && Math.Abs(b.X) < 0.05f)
                return 0;
            if (Math.Abs(a.X - width) < 0.05f && Math.Abs(b.X - width) < 0.05f)
                return 1;
            if (Math.Abs(a.Y) < 0.05f && Math.Abs(b.Y) < 0.05f)
                return 2;
            if (Math.Abs(a.Y - height) < 0.05f && Math.Abs(b.Y - height) < 0.05f)
                return 3;
            return -1;
        }

        // The cracks are jagged, but a piece that reaches the edge of the art was cut by a straight line there (the body's sides, the 140 px
        // seams between its rows, the sides of the mouth and eyes). This turns every such straight edge into a jagged one. The bites sit on a
        // fixed grid along the edge and depend only on the position, so two pieces that meet there agree.
        //  - sides, and every edge of the mouth and eyes: the bites only go inward (the art has nothing outside its rectangle)
        //  - the body seams (seams = true): the bites go both ways. The row above shows the bottom of the art's frame past its seam and the
        //    row below shows its top (the art repeats every row), SplitAtRows cuts the Uv for that. topKey / bottomKey name the seam
        //    (its world Y), so the row above and the row below get the same line.
        List<Vector2> JagBoundary(List<Vector2> poly, float width, float height, bool seams, int topKey, int bottomKey)
        {
            float step = Math.Max(2f, EdgeJagStep);
            List<Vector2> result = new List<Vector2>(poly.Count + 16);
            List<Vector2> chain = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Count];
                result.Add(a);

                int edge = BoundarySide(a, b, width, height);
                if (edge < 0)
                    continue;

                bool alongX = edge >= 2;
                float ta = alongX ? a.X : a.Y;
                float tb = alongX ? b.X : b.Y;
                float lo = Math.Min(ta, tb);
                float hi = Math.Max(ta, tb);
                float length = hi - lo;
                if (length < step * 0.8f)
                    continue;

                bool seam = seams && alongX;
                float amp = Math.Min(EdgeJag, length * 0.25f);
                int key = seam ? (edge == 2 ? topKey : bottomKey) : edge;
                int salt = seam ? 2 : edge;
                chain.Clear();
                for (int k = (int)Math.Floor(lo / step) + 1; k * step < hi; k++)
                {
                    float t = k * step;
                    if (t - lo < step * 0.3f || hi - t < step * 0.3f)
                        continue;

                    float noise = Hash01(jagSeed + salt, key, k);
                    float d = seam ? (noise * 2f - 1f) * amp : noise * amp;
                    Vector2 p;
                    if (edge == 0)
                        p = new Vector2(d, t);
                    else if (edge == 1)
                        p = new Vector2(width - d, t);
                    else if (edge == 2)
                        p = new Vector2(t, d);
                    else
                        p = new Vector2(t, seam ? height + d : height - d);
                    chain.Add(p);
                }

                if (tb < ta)
                    chain.Reverse();
                result.AddRange(chain);
            }

            return result;
        }

        // Cuts a piece at the top and bottom of its body row: the part inside keeps its Uv, the part above the row (a seam bite) shows the
        // bottom of the frame, so its Uv shifts down by one row; the part below shows the top, so its Uv shifts up.
        static List<(List<Vector2> Poly, float ShiftY)> SplitAtRows(List<Vector2> poly, float height)
        {
            List<(List<Vector2> Poly, float ShiftY)> parts = new List<(List<Vector2> Poly, float ShiftY)>();
            parts.Add((ClipAxis(ClipAxis(poly, false, 0f, true), false, height, false), 0f));
            parts.Add((ClipAxis(poly, false, 0f, false), height));
            parts.Add((ClipAxis(poly, false, height, true), -height));
            return parts;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
            float d2 = (p.X - c.X) * (b.Y - c.Y) - (b.X - c.X) * (p.Y - c.Y);
            float d3 = (p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        // Ear clipping: cuts a polygon (convex or not) into triangles, written as indices into its points (plus baseIndex). A jagged outline is
        // not star-shaped, so the old triangle fan from the centre would draw wrong triangles over it. If the outline is degenerate (it crosses
        // itself) and no ear can be found, the flattest corner is dropped, so this always ends.
        static void Triangulate(List<Vector2> poly, List<int> output, int baseIndex)
        {
            int n = poly.Count;
            if (n < 3)
                return;

            float signed = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = poly[i];
                Vector2 q = poly[(i + 1) % n];
                signed += p.X * q.Y - q.X * p.Y;
            }

            bool ccw = signed > 0f;
            List<int> ring = new List<int>(n);
            for (int i = 0; i < n; i++)
                ring.Add(i);

            int start = 0;
            int guard = n * n + 16;
            while (ring.Count > 3 && guard-- > 0)
            {
                int count = ring.Count;
                bool clipped = false;
                for (int tried = 0; tried < count; tried++)
                {
                    int i = (start + tried) % count;
                    int ia = ring[(i + count - 1) % count];
                    int ib = ring[i];
                    int ic = ring[(i + 1) % count];
                    Vector2 a = poly[ia];
                    Vector2 b = poly[ib];
                    Vector2 c = poly[ic];
                    float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                    if (ccw ? cross <= 0.0001f : cross >= -0.0001f)
                        continue;   // a reflex or flat corner is not an ear

                    bool blocked = false;
                    for (int j = 0; j < count; j++)
                    {
                        int k = ring[j];
                        if (k == ia || k == ib || k == ic)
                            continue;

                        Vector2 p = poly[k];
                        if (p == a || p == b || p == c)
                            continue;

                        if (PointInTriangle(p, a, b, c))
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (blocked)
                        continue;

                    output.Add(baseIndex + ia);
                    output.Add(baseIndex + ib);
                    output.Add(baseIndex + ic);
                    ring.RemoveAt(i);
                    start = i % ring.Count;
                    clipped = true;
                    break;
                }

                if (!clipped)
                {
                    int flattest = 0;
                    float least = float.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 a = poly[ring[(i + count - 1) % count]];
                        Vector2 b = poly[ring[i]];
                        Vector2 c = poly[ring[(i + 1) % count]];
                        float flat = Math.Abs((b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X));
                        if (flat < least)
                        {
                            least = flat;
                            flattest = i;
                        }
                    }

                    ring.RemoveAt(flattest);
                    start = 0;
                }
            }

            if (ring.Count == 3)
            {
                output.Add(baseIndex + ring[0]);
                output.Add(baseIndex + ring[1]);
                output.Add(baseIndex + ring[2]);
            }
        }

        // Adds one polygon (in the art's own pixels) to a shard's point lists and triangulates it. shiftY moves its Uv by a whole row (SplitAtRows).
        static void AddPolygon(List<Vector2> poly, float shiftY, Vector2 centre, float side, float frameX, float frameY, List<Vector2> local, List<Vector2> uv, List<int> triangles)
        {
            if (poly.Count < 3 || PolygonArea(poly) < 0.3f)
                return;

            int baseIndex = local.Count;
            for (int i = 0; i < poly.Count; i++)
            {
                local.Add(new Vector2(side * (poly[i].X - centre.X), poly[i].Y - centre.Y));
                uv.Add(new Vector2(frameX + poly[i].X, frameY + poly[i].Y + shiftY));
            }

            Triangulate(poly, triangles, baseIndex);
        }

        // Turns the web into shards for one piece of art. width x height is the art's rectangle in its own pixels, toTex maps a world point into
        // it, toWorld maps back, side is -1 when the art is drawn mirrored, (frameX, frameY) is where the rectangle starts inside the texture.
        // seams is true for a body row: its top and bottom edges are seams shared with the next rows (topKey / bottomKey name them).
        void SpawnPieces(Texture2D texture, float width, float height, Func<Vector2, Vector2> toTex, Func<Vector2, Vector2> toWorld, float side, float frameX, float frameY, float rotation, float scale, Vector2 impact, Vector2 bias, bool seams = false, int topKey = 0, int bottomKey = 0)
        {
            foreach (Vector2[] cell in web)
            {
                List<Vector2> poly = new List<Vector2>(cell.Length);
                foreach (Vector2 point in cell)
                    poly.Add(toTex(point));

                poly = ClipAxis(poly, true, 0f, true);
                if (poly.Count >= 3)
                    poly = ClipAxis(poly, true, width, false);
                if (poly.Count >= 3)
                    poly = ClipAxis(poly, false, 0f, true);
                if (poly.Count >= 3)
                    poly = ClipAxis(poly, false, height, false);

                if (poly.Count < 3 || PolygonArea(poly) < 2f)
                    continue;

                // the straight cuts made by the art's rectangle become jagged too
                poly = JagBoundary(poly, width, height, seams, topKey, bottomKey);
                Vector2 centre = PolygonCentre(poly);

                List<Vector2> local = new List<Vector2>();
                List<Vector2> uv = new List<Vector2>();
                List<int> triangles = new List<int>();
                if (seams)
                {
                    foreach (var part in SplitAtRows(poly, height))
                        AddPolygon(part.Poly, part.ShiftY, centre, side, frameX, frameY, local, uv, triangles);
                }
                else
                {
                    AddPolygon(poly, 0f, centre, side, frameX, frameY, local, uv, triangles);
                }

                if (triangles.Count < 3)
                    continue;

                AddShard(texture, local.ToArray(), uv.ToArray(), triangles.ToArray(), toWorld(centre), rotation, scale, impact, bias);
            }
        }

        // The wall's body strip, cut the way DrawBodyLikeVanilla draws it: rows of Height / 3 anchored at wofDrawAreaTop, one column as wide
        // as the texture, the frame from wofDrawFrameIndex, mirrored when spriteDirection == 1. Only the rows on screen (plus a margin) become shards.
        // The art repeats every row, so a crack that reaches a row's edge simply ends there (the next row's pieces carry on from the same web).
        void SpawnBody(Texture2D tile, NPC mouth, Vector2 impact, Vector2 bias)
        {
            float top = Main.wofDrawAreaTop;
            if (top < 0f)
                return;

            int width = tile != null ? tile.Width : 190;
            int rowHeight = tile != null ? tile.Height / 3 : 140;
            float zoom = Main.GameViewMatrix.Zoom.X;
            if (zoom < 0.01f)
                zoom = 1f;

            float visibleTop = Main.screenPosition.Y + Main.screenHeight * 0.5f - Main.screenHeight / zoom * 0.5f - 160f;
            float visibleBottom = Main.screenPosition.Y + Main.screenHeight * 0.5f + Main.screenHeight / zoom * 0.5f + 160f;
            float x0 = mouth.position.X;
            if (mouth.direction > 0)
                x0 -= 80f;

            bool flipped = mouth.spriteDirection == 1;
            int frameY = tile != null ? Math.Min(Math.Max(0, Main.wofDrawFrameIndex) / 6, 2) * rowHeight : 0;

            float firstRow = top + (float)Math.Floor((visibleTop - top) / rowHeight) * rowHeight;
            for (float rowY = firstRow; rowY < visibleBottom; rowY += rowHeight)
            {
                float y = rowY;
                // texture x t shows at world x0 + (width - t) when the art is mirrored (same mapping DrawBodyLikeVanilla uses)
                Func<Vector2, Vector2> toTex = p => new Vector2(flipped ? width - (p.X - x0) : p.X - x0, p.Y - y);
                Func<Vector2, Vector2> toWorld = t => new Vector2(x0 + (flipped ? width - t.X : t.X), y + t.Y);
                SpawnPieces(tile, width, rowHeight, toTex, toWorld, flipped ? -1f : 1f, 0f, frameY, 0f, 1f, impact, bias, true, (int)Math.Round(y), (int)Math.Round(y + rowHeight));
            }
        }

        // The mouth or one eye: the current frame of its sheet, every piece placed where it is on the NPC (mirrored and rotated like the NPC).
        void SpawnSheet(Texture2D sheet, NPC npc, Vector2 impact, Vector2 bias)
        {
            Rectangle frame = npc.frame;
            if (sheet != null)
            {
                if (frame.Width <= 0 || frame.Height <= 0 || frame.Right > sheet.Width || frame.Bottom > sheet.Height)
                    frame = new Rectangle(0, 0, sheet.Width, sheet.Height);
            }
            else if (frame.Width <= 0 || frame.Height <= 0)
            {
                frame = new Rectangle(0, 0, npc.width, npc.height);
            }

            float side = npc.spriteDirection == 1 ? -1f : 1f;
            float scale = npc.scale < 0.01f ? 1f : npc.scale;
            float rotation = npc.rotation;
            Vector2 half = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);
            Vector2 origin = npc.Center + new Vector2(0f, npc.gfxOffY);

            Func<Vector2, Vector2> toTex = p =>
            {
                Vector2 l = (p - origin).RotatedBy(-rotation) / scale;
                return new Vector2(half.X + side * l.X, half.Y + l.Y);
            };
            Func<Vector2, Vector2> toWorld = t => origin + (new Vector2(side * (t.X - half.X), t.Y - half.Y) * scale).RotatedBy(rotation);
            SpawnPieces(sheet, frame.Width, frame.Height, toTex, toWorld, side, frame.X, frame.Y, rotation, scale, impact, bias);
        }

        void AddShard(Texture2D texture, Vector2[] local, Vector2[] uv, int[] triangles, Vector2 center, float rotation, float scale, Vector2 impact, Vector2 bias)
        {
            if (shards.Count >= MaxShards)
                return;

            Vector2 away = center - impact;
            float distance = away.Length();
            away = distance < 8f ? Main.rand.NextVector2CircularEdge(1f, 1f) : away / distance;
            float speed = Main.rand.NextFloat(ShardSpeedMin, ShardSpeedMax) * (1f - 0.35f * MathHelper.Clamp(distance / 1400f, 0f, 1f));

            shards.Add(new Shard
            {
                Texture = texture,
                Local = local,
                Uv = uv,
                Triangles = triangles,
                Position = center,
                Velocity = away * speed + bias * Main.rand.NextFloat(0.5f, 1.2f) + Main.rand.NextVector2Circular(1.5f, 1.5f),
                Rotation = rotation,
                Spin = Main.rand.NextFloat(-MaxSpin, MaxSpin),
                Scale = scale,
                Life = Main.rand.Next(Math.Min(ShardLifeMin, ShardLifeMax), Math.Max(ShardLifeMin, ShardLifeMax) + 1)
            });
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || (shards.Count == 0 && curtains.Count == 0))
                return;

            for (int i = shards.Count - 1; i >= 0; i--)
            {
                Shard shard = shards[i];
                shard.Age++;
                if (shard.Age >= shard.Life)
                {
                    shards.RemoveAt(i);
                    continue;
                }

                shard.Velocity.Y += ShardGravity;
                shard.Velocity *= 0.995f;
                shard.Position += shard.Velocity;
                shard.Rotation += shard.Spin;
                shard.Spin *= 0.996f;

                // a few specks of stone fall off while it is still flying
                if (shard.Age < shard.Life * 0.6f && Main.rand.NextBool(8))
                {
                    Dust speck = Dust.NewDustPerfect(shard.Position, DustID.Stone, -shard.Velocity * 0.1f + Main.rand.NextVector2Circular(1f, 1f), 0, default, Main.rand.NextFloat(0.9f, 1.5f));
                    speck.noGravity = false;
                }
            }

            float zoom = Main.GameViewMatrix.Zoom.X;
            if (zoom < 0.01f)
                zoom = 1f;

            float visibleTop = Main.screenPosition.Y + Main.screenHeight * 0.5f - Main.screenHeight / zoom * 0.5f;
            float visibleHeight = Main.screenHeight / zoom;
            for (int i = curtains.Count - 1; i >= 0; i--)
            {
                Curtain curtain = curtains[i];
                curtain.Age++;
                if (curtain.Age >= CurtainTicks)
                {
                    curtains.RemoveAt(i);
                    continue;
                }

                // dust keeps coming off the body, falling and drifting, thinning out over time
                float fade = 1f - curtain.Age / (float)Math.Max(1, CurtainTicks);
                int count = 2 + (int)(10f * fade);
                for (int k = 0; k < count; k++)
                {
                    Vector2 at = new Vector2(curtain.X + Main.rand.NextFloat(curtain.Width), visibleTop + Main.rand.NextFloat(visibleHeight));
                    Dust stone = Dust.NewDustPerfect(at, DustID.Stone, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0f, 2f)), 0, default, Main.rand.NextFloat(1.1f, 2f));
                    stone.noGravity = false;

                    if (k % 2 == 0)
                    {
                        Dust smoke = Dust.NewDustPerfect(at, DustID.Smoke, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-0.5f, 1f)), 130, default, Main.rand.NextFloat(1.6f, 2.6f));
                        smoke.noGravity = true;
                    }
                }
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (shards.Count == 0)
                return;

            layers.Insert(0, new LegacyGameInterfaceLayer("Factorraria: Shield Shards", delegate
            {
                DrawShards(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.Game));
        }

        // The pieces are polygons, which SpriteBatch cannot draw, so they go through a BasicEffect as textured triangles (one draw per texture).
        // The interface layer's SpriteBatch is ended for that and begun again with the state the Game-scale layers use.
        BasicEffect effect;
        VertexPositionColorTexture[] vertexScratch = new VertexPositionColorTexture[0];

        public override void Unload()
        {
            BasicEffect old = effect;
            effect = null;
            if (old != null && !Main.dedServ)
                Main.QueueMainThreadAction(old.Dispose);
        }

        void DrawShards(SpriteBatch spriteBatch)
        {
            WallShieldDrawSystem art = ModContent.GetInstance<WallShieldDrawSystem>();
            Texture2D pixel = TextureAssets.MagicPixel.Value;   // not 1x1: every stand-in corner samples the middle of texel (0, 0)
            Vector2 pixelUv = new Vector2(0.5f / pixel.Width, 0.5f / pixel.Height);

            Dictionary<Texture2D, List<VertexPositionColorTexture>> batches = new Dictionary<Texture2D, List<VertexPositionColorTexture>>();
            Vector3[] corners = new Vector3[16];

            foreach (Shard shard in shards)
            {
                Vector2 screen = shard.Position - Main.screenPosition;
                if (screen.X < -200f || screen.Y < -200f || screen.X > Main.screenWidth + 200f || screen.Y > Main.screenHeight + 200f)
                    continue;

                int count = shard.Local.Length;
                if (count < 3 || shard.Triangles == null || shard.Triangles.Length < 3)
                    continue;

                if (corners.Length < count)
                    corners = new Vector3[count];

                float alpha = Math.Min(1f, (shard.Life - shard.Age) / Math.Max(1f, shard.Life * 0.3f));
                Color light = Lighting.GetColor((int)(shard.Position.X / 16f), (int)(shard.Position.Y / 16f)) * alpha;
                Texture2D texture = shard.Texture ?? pixel;
                Color color = shard.Texture != null ? light : art.FallbackColor.MultiplyRGBA(light);

                if (!batches.TryGetValue(texture, out List<VertexPositionColorTexture> list))
                {
                    list = new List<VertexPositionColorTexture>();
                    batches[texture] = list;
                }

                // every piece is a list of triangles (ear-clipped, so the jagged outline can be concave)
                for (int i = 0; i < count; i++)
                    corners[i] = new Vector3((shard.Local[i] * shard.Scale).RotatedBy(shard.Rotation) + screen, 0f);

                for (int t = 0; t < shard.Triangles.Length; t++)
                {
                    int v = shard.Triangles[t];
                    Vector2 uvPoint = shard.Texture != null ? new Vector2(shard.Uv[v].X / texture.Width, shard.Uv[v].Y / texture.Height) : pixelUv;
                    list.Add(new VertexPositionColorTexture(corners[v], color, uvPoint));
                }
            }

            if (batches.Count == 0)
                return;

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            spriteBatch.End();
            try
            {
                if (effect == null)
                    effect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true };

                effect.World = Matrix.Identity;
                effect.View = Main.GameViewMatrix.TransformationMatrix;
                effect.Projection = Matrix.CreateOrthographicOffCenter(0f, device.Viewport.Width, device.Viewport.Height, 0f, 0f, 1f);

                device.BlendState = BlendState.AlphaBlend;
                device.DepthStencilState = DepthStencilState.None;
                device.RasterizerState = RasterizerState.CullNone;
                device.SamplerStates[0] = Main.DefaultSamplerState;

                foreach (KeyValuePair<Texture2D, List<VertexPositionColorTexture>> batch in batches)
                {
                    effect.Texture = batch.Key;
                    effect.CurrentTechnique.Passes[0].Apply();
                    if (vertexScratch.Length < batch.Value.Count)
                        vertexScratch = new VertexPositionColorTexture[batch.Value.Count + 4096];
                    batch.Value.CopyTo(vertexScratch);
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, vertexScratch, 0, batch.Value.Count / 3);
                }
            }
            finally
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }
    }
}