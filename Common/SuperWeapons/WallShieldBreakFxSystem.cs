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
        public int MaxShards = 700;

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

        /// <summary>How many pieces each 190 x 140 px body row is broken into (random fracture lines, so the sizes and shapes differ).</summary>
        public int BodyPieces = 9;

        /// <summary>How many pieces the mouth and each eye frame is broken into.</summary>
        public int SheetPieces = 9;

        /// <summary>The smallest piece a fracture may leave, as a fraction of an even share of the area (0.2 = a fifth of the average piece).</summary>
        public float MinPieceShare = 0.2f;

        /// <summary>How long the dust curtain keeps falling down the body, in ticks.</summary>
        public int CurtainTicks = 80;

        class Shard
        {
            public Texture2D Texture;   // null = the grey stand-in block
            public Vector2[] Local;     // the polygon's corners, relative to Position, in unscaled unrotated screen space (already mirrored when the art is)
            public Vector2[] Uv;        // the same corners as pixel positions in Texture
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

            ImpactBurst(impact);

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

        // ---- Random fracture ----
        // A region is broken like glass: start with the whole rectangle and keep cutting one piece (picked by size, so big ones go first)
        // along a straight line of random angle that passes near its middle, until there are enough pieces. Every piece stays convex, so a fan
        // of triangles from its first corner fills it exactly, and the cracks are random lines instead of a grid of rows and columns.

        static float PolygonArea(Vector2[] poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Length];
                area += a.X * b.Y - b.X * a.Y;
            }

            return Math.Abs(area) * 0.5f;
        }

        static Vector2 PolygonCentre(Vector2[] poly)
        {
            float area = 0f;
            Vector2 sum = Vector2.Zero;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Length];
                float cross = a.X * b.Y - b.X * a.Y;
                area += cross;
                sum += (a + b) * cross;
            }

            if (Math.Abs(area) < 0.001f)
                return poly[0];

            return sum / (3f * area);
        }

        // Splits a convex polygon by the line through point with the given normal. A corner exactly on the line goes to both sides.
        static bool Split(Vector2[] poly, Vector2 point, Vector2 normal, out Vector2[] sideA, out Vector2[] sideB)
        {
            List<Vector2> a = new List<Vector2>();
            List<Vector2> b = new List<Vector2>();
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 current = poly[i];
                Vector2 next = poly[(i + 1) % poly.Length];
                float dc = Vector2.Dot(normal, current - point);
                float dn = Vector2.Dot(normal, next - point);

                if (dc >= 0f)
                    a.Add(current);
                if (dc <= 0f)
                    b.Add(current);

                if ((dc > 0f && dn < 0f) || (dc < 0f && dn > 0f))
                {
                    Vector2 cross = current + (next - current) * (dc / (dc - dn));
                    a.Add(cross);
                    b.Add(cross);
                }
            }

            sideA = a.Count >= 3 ? a.ToArray() : null;
            sideB = b.Count >= 3 ? b.ToArray() : null;
            return sideA != null && sideB != null;
        }

        // The pieces (corner lists, in the region's own pixels, 0..width and 0..height) the region breaks into.
        List<Vector2[]> Fracture(float width, float height, int pieces)
        {
            List<Vector2[]> list = new List<Vector2[]>
            {
                new[] { new Vector2(0f, 0f), new Vector2(width, 0f), new Vector2(width, height), new Vector2(0f, height) }
            };

            pieces = Math.Max(1, pieces);
            float minArea = width * height / pieces * Math.Max(0.02f, MinPieceShare);
            for (int attempt = 0; list.Count < pieces && attempt < pieces * 14; attempt++)
            {
                float total = 0f;
                foreach (Vector2[] piece in list)
                    total += PolygonArea(piece);

                float roll = Main.rand.NextFloat(total);
                int index = list.Count - 1;
                for (int i = 0; i < list.Count; i++)
                {
                    roll -= PolygonArea(list[i]);
                    if (roll <= 0f)
                    {
                        index = i;
                        break;
                    }
                }

                Vector2[] poly = list[index];
                Vector2 centre = PolygonCentre(poly);
                float spread = (float)Math.Sqrt(PolygonArea(poly)) * 0.3f;
                Vector2 point = centre + Main.rand.NextVector2Circular(spread, spread);
                float angle = Main.rand.NextFloat(MathHelper.Pi);
                Vector2 normal = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));

                if (!Split(poly, point, normal, out Vector2[] a, out Vector2[] b) || PolygonArea(a) < minArea || PolygonArea(b) < minArea)
                    continue;

                list[index] = a;
                list.Add(b);
            }

            return list;
        }

        // The wall's body strip, cut the way DrawBodyLikeVanilla draws it: rows of Height / 3 anchored at wofDrawAreaTop, one column as wide
        // as the texture, the frame from wofDrawFrameIndex, mirrored when spriteDirection == 1. Only the rows on screen (plus a margin) become shards.
        // Each row is fractured on its own (the art repeats every row, so a crack cannot run from one row into the next).
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
                foreach (Vector2[] poly in Fracture(width, rowHeight, BodyPieces))
                {
                    // texture x t shows at world x0 + (width - t) when the art is mirrored (same mapping DrawBodyLikeVanilla uses)
                    Vector2 centre = PolygonCentre(poly);
                    Vector2[] local = new Vector2[poly.Length];
                    Vector2[] uv = new Vector2[poly.Length];
                    for (int i = 0; i < poly.Length; i++)
                    {
                        local[i] = new Vector2((flipped ? -1f : 1f) * (poly[i].X - centre.X), poly[i].Y - centre.Y);
                        uv[i] = new Vector2(poly[i].X, frameY + poly[i].Y);
                    }

                    Vector2 world = new Vector2(x0 + (flipped ? width - centre.X : centre.X), rowY + centre.Y);
                    AddShard(tile, local, uv, world, 0f, 1f, impact, bias);
                }
            }
        }

        // The mouth or one eye: the current frame of its sheet, fractured, every piece placed where it is on the NPC (mirrored and rotated like the NPC).
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

            bool flipped = npc.spriteDirection == 1;
            float side = flipped ? -1f : 1f;
            Vector2 half = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);
            foreach (Vector2[] poly in Fracture(frame.Width, frame.Height, SheetPieces))
            {
                Vector2 centre = PolygonCentre(poly);
                Vector2[] local = new Vector2[poly.Length];
                Vector2[] uv = new Vector2[poly.Length];
                for (int i = 0; i < poly.Length; i++)
                {
                    local[i] = new Vector2(side * (poly[i].X - centre.X), poly[i].Y - centre.Y);
                    uv[i] = new Vector2(frame.X + poly[i].X, frame.Y + poly[i].Y);
                }

                Vector2 offset = new Vector2(side * (centre.X - half.X), centre.Y - half.Y) * npc.scale;
                Vector2 world = npc.Center + new Vector2(0f, npc.gfxOffY) + offset.RotatedBy(npc.rotation);
                AddShard(sheet, local, uv, world, npc.rotation, npc.scale, impact, bias);
            }
        }

        void AddShard(Texture2D texture, Vector2[] local, Vector2[] uv, Vector2 center, float rotation, float scale, Vector2 impact, Vector2 bias)
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
                if (count < 3)
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

                for (int i = 0; i < count; i++)
                {
                    Vector2 point = (shard.Local[i] * shard.Scale).RotatedBy(shard.Rotation) + screen;
                    corners[i] = new Vector3(point, 0f);
                }

                for (int i = 1; i < count - 1; i++)
                {
                    foreach (int k in new[] { 0, i, i + 1 })
                    {
                        Vector2 uv = shard.Texture != null ? new Vector2(shard.Uv[k].X / texture.Width, shard.Uv[k].Y / texture.Height) : pixelUv;
                        list.Add(new VertexPositionColorTexture(corners[k], color, uv));
                    }
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
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, batch.Value.ToArray(), 0, batch.Value.Count / 3);
                }
            }
            finally
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }
    }
}