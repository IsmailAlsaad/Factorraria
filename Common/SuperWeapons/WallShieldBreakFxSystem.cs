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

        /// <summary>How many columns and sub-rows each 140 px body row is cut into.</summary>
        public int BodyCols = 2;
        public int BodySubRows = 2;

        /// <summary>How many columns and rows the mouth and each eye frame is cut into.</summary>
        public int SheetCols = 2;
        public int SheetRows = 2;

        /// <summary>How long the dust curtain keeps falling down the body, in ticks.</summary>
        public int CurtainTicks = 80;

        class Shard
        {
            public Texture2D Texture;   // null = the grey stand-in block
            public Rectangle Source;
            public Vector2 Position;    // centre, world pixels
            public Vector2 Velocity;
            public float Rotation;
            public float Spin;
            public float Scale;
            public SpriteEffects Effects;
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

        // Jittered cut positions that split a length into n pieces of about the same size: result[0] = 0, result[n] = total.
        static int[] Cuts(int total, int n)
        {
            n = Math.Max(1, n);
            int[] cuts = new int[n + 1];
            cuts[n] = total;
            float step = total / (float)n;
            for (int i = 1; i < n; i++)
            {
                int cut = (int)(step * i + Main.rand.NextFloat(-0.3f, 0.3f) * step);
                cuts[i] = Math.Max(cuts[i - 1] + 2, Math.Min(cut, total - 2 * (n - i)));
            }

            return cuts;
        }

        // The wall's body strip, cut the way DrawBodyLikeVanilla draws it: rows of Height / 3 anchored at wofDrawAreaTop, one column as wide
        // as the texture, the frame from wofDrawFrameIndex, mirrored when spriteDirection == 1. Only the rows on screen (plus a margin) become shards.
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
            SpriteEffects effects = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            int frameY = tile != null ? Math.Min(Math.Max(0, Main.wofDrawFrameIndex) / 6, 2) * rowHeight : 0;
            int subRows = Math.Max(1, BodySubRows);

            float firstRow = top + (float)Math.Floor((visibleTop - top) / rowHeight) * rowHeight;
            for (float rowY = firstRow; rowY < visibleBottom; rowY += rowHeight)
            {
                int subHeight = rowHeight / subRows;
                for (int s = 0; s < subRows; s++)
                {
                    int y0 = s * subHeight;
                    int height = s == subRows - 1 ? rowHeight - y0 : subHeight;
                    float cellTop = rowY + y0;
                    if (cellTop + height < visibleTop || cellTop > visibleBottom)
                        continue;

                    int[] cuts = Cuts(width, BodyCols);
                    for (int c = 0; c < cuts.Length - 1; c++)
                    {
                        int a = cuts[c];
                        int b = cuts[c + 1];
                        int w = b - a;
                        if (w <= 0)
                            continue;

                        // world column x in [a, b] shows texture x in [width - b, width - a] when the art is mirrored
                        Rectangle source = new Rectangle(flipped ? width - b : a, frameY + y0, w, height);
                        Vector2 center = new Vector2(x0 + a + w * 0.5f, cellTop + height * 0.5f);
                        AddShard(tile, source, center, 0f, 1f, effects, impact, bias);
                    }
                }
            }
        }

        // The mouth or one eye: the current frame of its sheet, cut into a grid, every piece placed where it is on the NPC (mirrored and rotated like the NPC).
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
            SpriteEffects effects = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            int[] xs = Cuts(frame.Width, SheetCols);
            int[] ys = Cuts(frame.Height, SheetRows);
            for (int r = 0; r < ys.Length - 1; r++)
            {
                for (int c = 0; c < xs.Length - 1; c++)
                {
                    int w = xs[c + 1] - xs[c];
                    int h = ys[r + 1] - ys[r];
                    if (w <= 0 || h <= 0)
                        continue;

                    Rectangle source = new Rectangle(frame.X + xs[c], frame.Y + ys[r], w, h);
                    Vector2 local = new Vector2(xs[c] + w * 0.5f - frame.Width * 0.5f, ys[r] + h * 0.5f - frame.Height * 0.5f) * npc.scale;
                    if (flipped)
                        local.X = -local.X;

                    Vector2 center = npc.Center + new Vector2(0f, npc.gfxOffY) + local.RotatedBy(npc.rotation);
                    AddShard(sheet, source, center, npc.rotation, npc.scale, effects, impact, bias);
                }
            }
        }

        void AddShard(Texture2D texture, Rectangle source, Vector2 center, float rotation, float scale, SpriteEffects effects, Vector2 impact, Vector2 bias)
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
                Source = source,
                Position = center,
                Velocity = away * speed + bias * Main.rand.NextFloat(0.5f, 1.2f) + Main.rand.NextVector2Circular(1.5f, 1.5f),
                Rotation = rotation,
                Spin = Main.rand.NextFloat(-MaxSpin, MaxSpin),
                Scale = scale,
                Effects = effects,
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

        void DrawShards(SpriteBatch spriteBatch)
        {
            WallShieldDrawSystem art = ModContent.GetInstance<WallShieldDrawSystem>();
            Texture2D pixel = TextureAssets.MagicPixel.Value;   // not 1x1: always drawn with a 1x1 source
            Rectangle one = new Rectangle(0, 0, 1, 1);

            foreach (Shard shard in shards)
            {
                Vector2 screen = shard.Position - Main.screenPosition;
                if (screen.X < -200f || screen.Y < -200f || screen.X > Main.screenWidth + 200f || screen.Y > Main.screenHeight + 200f)
                    continue;

                float alpha = Math.Min(1f, (shard.Life - shard.Age) / Math.Max(1f, shard.Life * 0.3f));
                Color light = Lighting.GetColor((int)(shard.Position.X / 16f), (int)(shard.Position.Y / 16f)) * alpha;

                if (shard.Texture != null)
                {
                    spriteBatch.Draw(shard.Texture, screen, shard.Source, light, shard.Rotation, new Vector2(shard.Source.Width * 0.5f, shard.Source.Height * 0.5f), shard.Scale, shard.Effects, 0f);
                }
                else
                {
                    Color block = art.FallbackColor.MultiplyRGBA(light);
                    spriteBatch.Draw(pixel, screen, one, block, shard.Rotation, new Vector2(0.5f, 0.5f), new Vector2(shard.Source.Width, shard.Source.Height) * shard.Scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}