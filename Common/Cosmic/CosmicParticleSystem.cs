using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // What an event packet decodes into on a multiplayer client. A network handler may run off the main thread, so it only fills the
    // two queues in CosmicParticleSystem; PostUpdateEverything (main thread) applies them.
    public struct CosmicClientRay
    {
        public byte Color;
        public byte End;
        public float X, Y, DirX, DirY, Length, Speed;
    }

    public static class CosmicEventKind
    {
        public const byte Absorb = 0;
        public const byte Ionize = 1;
        public const byte Discharge = 2;
        public const byte Strike = 3;
        public const byte Spit = 4;
    }

    public struct CosmicClientEvent
    {
        public byte Kind;
        public byte Color;
        public byte Tier;
        public ushort Life;
        public float AX, AY, BX, BY;   // Absorb: tile in AX/AY. Discharge: center in AX/AY. Strike: A to B. Spit: A and the direction in BX/BY.
        public short[] Tiles;          // x, y pairs (Ionize and Discharge)
    }

    // Client-side drawing of the cosmic radiation (roadmap patch C6). Authority: NONE here. The server (or single-player) owns every
    // ray, ionized block and discharge; this system only draws what it is told, keeps nothing that is saved or sent back, and a dedicated
    // server never runs any of it.
    //
    // Where the events come from: in single-player (and on a host that plays) CosmicEventRouter calls the Add... methods directly;
    // on a multiplayer client the packets CosmicRays (55) and CosmicEvent (56) are decoded into IncomingRays / IncomingEvents and applied here.
    //
    // Cost: a fixed pool of particles (ClientParticlePool, no allocation per tick or per frame). A particle is a whole ray: start, direction,
    // length, birth tick. Its position is worked out from its age when it is drawn, nothing is moved per tick. A particle that is off screen
    // costs one rectangle test. Everything is drawn with one additive SpriteBatch pass from PostDrawTiles, with TextureAssets.MagicPixel
    // (source rectangle 1x1, see PROJECT_CONTEXT gotchas) or, if the file exists, the optional circle texture.
    public partial class CosmicParticleSystem : ModSystem
    {
        public static CosmicParticleSystem Instance => ModContent.GetInstance<CosmicParticleSystem>();

        public readonly ConcurrentQueue<CosmicClientRay> IncomingRays = new ConcurrentQueue<CosmicClientRay>();
        public readonly ConcurrentQueue<CosmicClientEvent> IncomingEvents = new ConcurrentQueue<CosmicClientEvent>();

        struct Particle
        {
            public bool Active;
            public byte Color;
            public byte End;
            public uint Born;
            public float X, Y, DirX, DirY, Length, Speed, Life;
        }

        struct Glow
        {
            public byte Color;
            public bool Fading;
            public uint Born;
            public int Life;
        }

        struct Bolt
        {
            public bool Active;
            public bool Thin;
            public uint Born;
            public int Life;
            public float AX, AY, BX, BY;
            public byte Tier;
            public int Seed;
        }

        struct Spark
        {
            public bool Active;
            public byte Color;
            public uint Born;
            public float X, Y;
        }

        Particle[] pool = new Particle[0];
        int nextParticle;
        readonly Dictionary<long, Glow> glows = new Dictionary<long, Glow>();
        readonly List<long> expired = new List<long>();
        readonly Bolt[] bolts = new Bolt[48];
        int nextBolt;
        readonly Spark[] sparks = new Spark[128];
        int nextSpark;
        int boltsActive, sparksActive;
        int boltSeed;

        Texture2D circle;
        bool circleLooked;

        // What /fcosmic fx prints.
        public int ActiveCount, PeakActive;
        public long Spawned, Overwritten, ReceivedRays, ReceivedEvents;

        // ------------------------------------------------------------ lifecycle
        public override void OnWorldLoad() => Clear();

        public override void OnWorldUnload() => Clear();

        public void Clear()
        {
            while (IncomingRays.TryDequeue(out _)) { }
            while (IncomingEvents.TryDequeue(out _)) { }

            pool = new Particle[0];
            nextParticle = 0;
            glows.Clear();
            expired.Clear();
            Array.Clear(bolts, 0, bolts.Length);
            Array.Clear(sparks, 0, sparks.Length);
            boltsActive = sparksActive = 0;
            ActiveCount = PeakActive = 0;
            Spawned = Overwritten = ReceivedRays = ReceivedEvents = 0;
            ClearAmbient();
        }

        public string StatsText()
        {
            return "particles " + ActiveCount + " of " + pool.Length + " (peak " + PeakActive + ", pool full and overwrote " + Overwritten + ", spawned " + Spawned
                + "), ionized glow blocks " + glows.Count + ", bolts " + boltsActive + ", sparks " + sparksActive
                + ", received from the server: " + ReceivedRays + " rays and " + ReceivedEvents + " events, queued " + IncomingRays.Count + " + " + IncomingEvents.Count + ". " + AmbientText();
        }

        // ------------------------------------------------------------ adding things (main thread)
        void EnsurePool()
        {
            int wanted = Math.Max(16, CosmicRadiationSystem.Instance.ClientParticlePool);
            if (pool.Length != wanted)
            {
                pool = new Particle[wanted];   // only when the tunable changes (hot reload) or the first time
                nextParticle = 0;
            }
        }

        public void AddRay(CosmicColor color, byte end, float x, float y, float dirX, float dirY, float length, float speed)
        {
            AddRay((byte)color, end, x, y, dirX, dirY, length, speed);
        }

        public void AddRay(byte color, byte end, float x, float y, float dirX, float dirY, float length, float speed)
        {
            if (Main.dedServ || length < 0.05f || speed < 0.01f)
                return;

            EnsurePool();

            ref Particle p = ref pool[nextParticle];
            if (p.Active)
                Overwritten++;   // the pool is full: the oldest one goes

            nextParticle = (nextParticle + 1) % pool.Length;

            float trail = Math.Max(0.5f, CosmicRadiationSystem.Instance.ClientTrailTiles);
            p.Active = true;
            p.Color = color;
            p.End = end;
            p.Born = Main.GameUpdateCount;
            p.X = x;
            p.Y = y;
            p.DirX = dirX;
            p.DirY = dirY;
            p.Length = length;
            p.Speed = speed;
            p.Life = (length + trail) / speed + 2f;
            Spawned++;
        }

        public void AddAbsorb(CosmicColor color, int tileX, int tileY)
        {
            AddAbsorb((byte)color, tileX, tileY);
        }

        void AddAbsorb(byte color, float tileX, float tileY)
        {
            if (Main.dedServ)
                return;

            AddSpark(color, tileX + 0.5f, tileY + 0.5f, 0);

            // C7: the gemspark re-emits two particles of the next color, shown as a second, slightly later sparkle in that color.
            CosmicColor absorbed = (CosmicColor)Math.Min(color, (byte)(CosmicColors.Count - 1));
            CosmicAudioHooks.OnAbsorbed(absorbed, tileX + 0.5f, tileY + 0.5f);

            if (CosmicColors.HasNext(absorbed))
            {
                CosmicColor child = CosmicColors.Next(absorbed);
                AddSpark((byte)child, tileX + 0.5f, tileY + 0.5f, 3);
                CosmicAudioHooks.OnReemitted(child, tileX + 0.5f, tileY + 0.5f);
            }
        }

        // delayTicks > 0 makes the spark appear that many ticks from now (its Born is in the future and the draw skips it until then).
        void AddSpark(byte color, float x, float y, int delayTicks)
        {
            ref Spark s = ref sparks[nextSpark];
            nextSpark = (nextSpark + 1) % sparks.Length;
            s.Active = true;
            s.Color = color;
            s.Born = Main.GameUpdateCount + (uint)Math.Max(0, delayTicks);
            s.X = x;
            s.Y = y;
        }

        // Single-player path: the tile list belongs to the simulation and is only valid during the call.
        public void AddIonize(CosmicColor color, int lifeTicks, IReadOnlyList<(int x, int y)> tiles)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < tiles.Count; i++)
                SetGlow(tiles[i].x, tiles[i].y, (byte)color, lifeTicks);

            if (tiles.Count > 0)
                CosmicAudioHooks.OnIonized(color, tiles[0].x + 0.5f, tiles[0].y + 0.5f, tiles.Count);
        }

        void AddIonize(byte color, int lifeTicks, short[] tiles)
        {
            if (tiles == null)
                return;

            for (int i = 0; i + 1 < tiles.Length; i += 2)
                SetGlow(tiles[i], tiles[i + 1], color, lifeTicks);

            if (tiles.Length >= 2)
                CosmicAudioHooks.OnIonized((CosmicColor)Math.Min(color, (byte)(CosmicColors.Count - 1)), tiles[0] + 0.5f, tiles[1] + 0.5f, tiles.Length / 2);
        }

        void SetGlow(int x, int y, byte color, int lifeTicks)
        {
            glows[((long)x << 32) | (uint)y] = new Glow { Color = color, Fading = false, Born = Main.GameUpdateCount, Life = Math.Max(1, lifeTicks) };
        }

        // The cluster discharged: its glowing blocks flash white and fade out quickly.
        public void AddDischarge(IReadOnlyList<(int x, int y)> tiles)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < tiles.Count; i++)
                FadeGlow(tiles[i].x, tiles[i].y);
        }

        void AddDischarge(short[] tiles)
        {
            if (tiles == null)
                return;

            for (int i = 0; i + 1 < tiles.Length; i += 2)
                FadeGlow(tiles[i], tiles[i + 1]);
        }

        void FadeGlow(int x, int y)
        {
            long key = ((long)x << 32) | (uint)y;
            if (glows.TryGetValue(key, out Glow g))
                glows[key] = new Glow { Color = g.Color, Fading = true, Born = Main.GameUpdateCount, Life = 10 };
        }

        // Lightning from the thunder to the struck machine. Positions in tile units.
        public void AddStrike(float ax, float ay, float bx, float by, byte tier)
        {
            AddBolt(ax, ay, bx, by, tier, false);
        }

        // Thunder with no machine in range: a shorter bolt in a random direction (dirX, dirY is a unit vector).
        public void AddSpit(float ax, float ay, float dirX, float dirY, byte tier)
        {
            float length = 8f + Math.Min(12f, tier * 0.4f);
            AddBolt(ax, ay, ax + dirX * length, ay + dirY * length, tier, true);
        }

        void AddBolt(float ax, float ay, float bx, float by, byte tier, bool thin)
        {
            if (Main.dedServ)
                return;

            CosmicAudioHooks.OnThunder(ax, ay, bx, by, tier, !thin);

            ref Bolt b = ref bolts[nextBolt];
            nextBolt = (nextBolt + 1) % bolts.Length;
            b.Active = true;
            b.Thin = thin;
            b.Born = Main.GameUpdateCount;
            b.Life = Math.Max(4, CosmicRadiationSystem.Instance.BoltLifeTicks);
            b.AX = ax;
            b.AY = ay;
            b.BX = bx;
            b.BY = by;
            b.Tier = tier;
            b.Seed = ++boltSeed;
        }

        // ------------------------------------------------------------ update (main thread, every side)
        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            // 1. what the network handler queued
            int guard = 0;
            while (guard++ < 8192 && IncomingRays.TryDequeue(out CosmicClientRay r))
            {
                ReceivedRays++;
                AddRay(r.Color, r.End, r.X, r.Y, r.DirX, r.DirY, r.Length, r.Speed);
            }

            guard = 0;
            while (guard++ < 4096 && IncomingEvents.TryDequeue(out CosmicClientEvent e))
            {
                ReceivedEvents++;
                switch (e.Kind)
                {
                    case CosmicEventKind.Absorb: AddAbsorb(e.Color, e.AX, e.AY); break;
                    case CosmicEventKind.Ionize: AddIonize(e.Color, e.Life, e.Tiles); break;
                    case CosmicEventKind.Discharge: AddDischarge(e.Tiles); break;
                    case CosmicEventKind.Strike: AddBolt(e.AX, e.AY, e.BX, e.BY, e.Tier, false); break;
                    case CosmicEventKind.Spit: AddSpit(e.AX, e.AY, e.BX, e.BY, e.Tier); break;
                }
            }

            // 2. age out what is finished
            uint now = Main.GameUpdateCount;

            int active = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].Active)
                    continue;

                if ((float)(int)(now - pool[i].Born) > pool[i].Life)
                    pool[i].Active = false;
                else
                    active++;
            }

            ActiveCount = active;
            if (active > PeakActive)
                PeakActive = active;

            if (glows.Count > 0)
            {
                float lightScale = CosmicRadiationSystem.Instance.IonizedLightIntensity;

                foreach (KeyValuePair<long, Glow> pair in glows)
                {
                    // C6e: the flashing block gives off light in its color, as strong as its tint. Lighting.AddLight is called on the main thread
                    // here (not from a tile light hook, which can run on the lighting thread while this table changes).
                    if (lightScale > 0f)
                    {
                        float strength = GlowStrength(pair.Value, (int)(now - pair.Value.Born)) * lightScale;
                        Color lc = CosmicColors.DrawColor((CosmicColor)pair.Value.Color);
                        Lighting.AddLight((int)(pair.Key >> 32), (int)(uint)pair.Key, lc.R / 255f * strength, lc.G / 255f * strength, lc.B / 255f * strength);
                    }

                    int age = (int)(now - pair.Value.Born);
                    int life = pair.Value.Fading ? pair.Value.Life : pair.Value.Life + 30;   // 30 ticks of grace if the discharge never reaches us
                    if (age >= life)
                        expired.Add(pair.Key);
                }

                for (int i = 0; i < expired.Count; i++)
                    glows.Remove(expired[i]);

                expired.Clear();
            }

            int bolt = 0;
            for (int i = 0; i < bolts.Length; i++)
            {
                if (!bolts[i].Active)
                    continue;

                if ((int)(now - bolts[i].Born) >= bolts[i].Life)
                    bolts[i].Active = false;
                else
                    bolt++;
            }
            boltsActive = bolt;

            int spark = 0;
            for (int i = 0; i < sparks.Length; i++)
            {
                if (!sparks[i].Active)
                    continue;

                if ((int)(now - sparks[i].Born) >= 8)
                    sparks[i].Active = false;
                else
                    spark++;
            }
            sparksActive = spark;

            // C7: the ambient local simulation and the light of lightning bolts (both client only, nothing here touches a counter)
            UpdateAmbient(now);
            UpdateBoltLight(now);
        }

        // ------------------------------------------------------------ draw (client only)
        // Is the world-pixel box inside the visible area (plus a margin)? The area is the window divided by the zoom, centered on the window.
        float viewX0, viewY0, viewX1, viewY1;

        void ReadView()
        {
            float zoom = Math.Max(0.1f, Main.GameViewMatrix.ZoomMatrix.M11);
            float w = Main.screenWidth / zoom;
            float h = Main.screenHeight / zoom;
            float margin = 160f;
            viewX0 = Main.screenPosition.X + (Main.screenWidth - w) * 0.5f - margin;
            viewY0 = Main.screenPosition.Y + (Main.screenHeight - h) * 0.5f - margin;
            viewX1 = viewX0 + w + margin * 2f;
            viewY1 = viewY0 + h + margin * 2f;
        }

        bool BoxVisible(float x0, float y0, float x1, float y1)
        {
            return x1 >= viewX0 && x0 <= viewX1 && y1 >= viewY0 && y0 <= viewY1;
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if (ActiveCount == 0 && glows.Count == 0 && boltsActive == 0 && sparksActive == 0 && ambientActive == 0 && !DebugOn)
                return;

            if (!circleLooked)
            {
                circleLooked = true;
                if (ModContent.RequestIfExists<Texture2D>("Factorraria/Content/Particles/CosmicParticle", out Asset<Texture2D> asset))
                    circle = asset.Value;
            }

            ReadView();
            CosmicRadiationSystem system = CosmicRadiationSystem.Instance;
            uint now = Main.GameUpdateCount;
            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            DrawDischargeFlash(sb, pixel, now);
            DrawAmbient(sb, pixel, now, system);
            DrawParticles(sb, pixel, now, system);
            DrawBolts(sb, pixel, now);
            DrawSparks(sb, pixel, now);
            DrawStrikeFlash(sb, pixel, now, system);

            if (DebugOn)
                DrawDebugWorld(sb, pixel, system);

            sb.End();
        }

        // C6b: an ionized cloud is no longer drawn with an overlay (clouds are already bright, so a glow on top was hard to read). Instead the
        // tile itself is drawn in the color: CosmicIonizedTileDraw (a GlobalTile) asks for the tint of the tile it is drawing and multiplies the
        // tile's own draw color by it. The tint pulses faster until the discharge, then fades back to the normal cloud color. Returns false
        // (and White) when the tile is not ionized. Main thread, client only.
        public bool TryGetTileTint(int x, int y, out Color tint)
        {
            tint = Color.White;
            if (glows.Count == 0 || !glows.TryGetValue(((long)x << 32) | (uint)y, out Glow g))
                return false;

            float strength = GlowStrength(g, (int)(Main.GameUpdateCount - g.Born));

            tint = Color.Lerp(Color.White, CosmicColors.TileTint((CosmicColor)g.Color), Math.Min(1f, strength));
            return true;
        }

        // How strongly an ionized block shows right now (0 to 1): the exact alpha of the old glow overlay, so the flashing is the one from before C6b.
        // Used for both the tile tint and the light the block gives off.
        static float GlowStrength(Glow g, float age)
        {
            if (g.Fading)
                return Math.Max(0f, 1f - age / g.Life) * 0.9f;   // same fade and 0.9 start as the old white flash

            float frac = Math.Min(1f, age / g.Life);
            float pulse = 0.5f + 0.5f * (float)Math.Sin(age * (0.35f + 0.6f * frac));
            return 0.22f + 0.3f * pulse + 0.25f * frac;
        }

        // C6f: the white flash at the end of the discharge, as before C6b: ONLY the blocks that are fading out (the cluster just discharged)
        // get an additive white quad, 0.9 strong at first and gone after the fade (10 ticks). The tile tint and light are separate (C6b to C6e).
        void DrawDischargeFlash(SpriteBatch sb, Texture2D pixel, uint now)
        {
            if (glows.Count == 0)
                return;

            foreach (KeyValuePair<long, Glow> pair in glows)
            {
                Glow g = pair.Value;
                if (!g.Fading)
                    continue;

                float px = (int)(pair.Key >> 32) * 16f;
                float py = (int)(uint)pair.Key * 16f;
                if (!BoxVisible(px, py, px + 16f, py + 16f))
                    continue;

                float age = (int)(now - g.Born);
                float alpha = Math.Max(0f, 1f - age / g.Life) * 0.9f;
                if (alpha <= 0f)
                    continue;

                sb.Draw(pixel, new Vector2(px - Main.screenPosition.X, py - Main.screenPosition.Y), new Rectangle(0, 0, 1, 1), Color.White * alpha, 0f, Vector2.Zero, new Vector2(16f, 16f), SpriteEffects.None, 0f);
            }
        }

        void DrawParticles(SpriteBatch sb, Texture2D pixel, uint now, CosmicRadiationSystem system)
        {
            DrawParticleSet(sb, pixel, now, system, pool, 1f);
        }

        // C7: the same drawing for any particle array (the ambient local simulation has its own, dimmer set).
        void DrawParticleSet(SpriteBatch sb, Texture2D pixel, uint now, CosmicRadiationSystem system, Particle[] set, float brightScale)
        {
            float trail = Math.Max(0.5f, system.ClientTrailTiles);
            float size = Math.Max(1f, system.ClientParticleSizePx);
            float bright = Math.Max(0f, system.ClientBrightness) * brightScale;

            for (int i = 0; i < set.Length; i++)
            {
                ref Particle p = ref set[i];
                if (!p.Active)
                    continue;

                float age = (int)(now - p.Born);
                float travelled = age * p.Speed;
                float head = Math.Min(travelled, p.Length);
                float tail = Math.Min(Math.Max(0f, travelled - trail), p.Length);
                if (tail >= p.Length)
                    continue;

                float hx = p.X + p.DirX * head;
                float hy = p.Y + p.DirY * head;
                float tx = p.X + p.DirX * tail;
                float ty = p.Y + p.DirY * tail;

                if (!BoxVisible(Math.Min(hx, tx) * 16f, Math.Min(hy, ty) * 16f, Math.Max(hx, tx) * 16f, Math.Max(hy, ty) * 16f))
                    continue;

                Color color = CosmicColors.DrawColor((CosmicColor)p.Color) * bright;
                float rotation = (float)Math.Atan2(p.DirY, p.DirX);

                // 8 straight segments, each brighter than the one behind it
                const int segments = 8;
                float step = (head - tail) / segments;
                if (step > 0.001f)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        float a = tail + step * s;
                        float fade = (s + 1f) / segments;
                        Vector2 from = new Vector2((p.X + p.DirX * a) * 16f - Main.screenPosition.X, (p.Y + p.DirY * a) * 16f - Main.screenPosition.Y);
                        sb.Draw(pixel, from, new Rectangle(0, 0, 1, 1), color * (fade * fade), rotation, new Vector2(0f, 0.5f), new Vector2(step * 16f + 0.5f, Math.Max(1f, size * 0.6f)), SpriteEffects.None, 0f);
                    }
                }

                // the head only while it is still flying
                if (travelled <= p.Length)
                {
                    Vector2 pos = new Vector2(hx * 16f - Main.screenPosition.X, hy * 16f - Main.screenPosition.Y);

                    if (circle != null)
                        sb.Draw(circle, pos, null, color, 0f, new Vector2(circle.Width, circle.Height) * 0.5f, size / circle.Width, SpriteEffects.None, 0f);
                    else
                    {
                        sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.35f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 2.2f, size * 2.2f), SpriteEffects.None, 0f);
                        sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
                    }
                }
            }
        }

        // A deterministic number in 0..1 for a bolt's jitter (no Random, no allocation).
        static float Hash01(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        void DrawBolts(SpriteBatch sb, Texture2D pixel, uint now)
        {
            if (boltsActive == 0)
                return;

            for (int i = 0; i < bolts.Length; i++)
            {
                ref Bolt b = ref bolts[i];
                if (!b.Active)
                    continue;

                if (!BoxVisible(Math.Min(b.AX, b.BX) * 16f - 32f, Math.Min(b.AY, b.BY) * 16f - 32f, Math.Max(b.AX, b.BX) * 16f + 32f, Math.Max(b.AY, b.BY) * 16f + 32f))
                    continue;

                float age = (int)(now - b.Born);
                float fade = Math.Max(0f, 1f - age / b.Life);
                int flicker = (int)(age / 3f);

                float dx = b.BX - b.AX;
                float dy = b.BY - b.AY;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                if (length < 0.05f)
                    continue;

                float perpX = -dy / length;
                float perpY = dx / length;
                float amplitude = Math.Min(1.4f, length * 0.1f);
                int segments = Math.Max(4, Math.Min(24, (int)(length * 1.2f)));
                // C6b: thickness grows with the energy. Tier is log10(energy) x 4, so tier / 4 is the number of decades.
                CosmicRadiationSystem cosmic = CosmicRadiationSystem.Instance;
                float decades = b.Tier * 0.25f;
                float widthCore = Math.Min(Math.Max(1f, cosmic.BoltMaxWidthPx), Math.Max(0.5f, cosmic.BoltBaseWidthPx) + decades * Math.Max(0f, cosmic.BoltWidthPerDecadePx)) * (b.Thin ? 0.6f : 1f);
                float widthGlow = widthCore * 3f;

                float prevX = b.AX;
                float prevY = b.AY;
                for (int s = 1; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    float px = b.AX + dx * t;
                    float py = b.AY + dy * t;

                    if (s < segments)
                    {
                        float jitter = (Hash01(b.Seed, flicker, s) - 0.5f) * 2f * amplitude;
                        px += perpX * jitter;
                        py += perpY * jitter;
                    }

                    // C6c: transparent at the start, fully opaque 15% along the bolt, then thinner and a little fainter all the way to the end.
                    float tm = (s - 0.5f) / segments;
                    float fadeIn = Math.Min(1f, tm / 0.15f);
                    float taper = tm <= 0.15f ? 1f : 1f - 0.8f * ((tm - 0.15f) / 0.85f);
                    float alpha = fade * fadeIn * (0.4f + 0.6f * taper);

                    Line(sb, pixel, prevX, prevY, px, py, widthGlow * taper, new Color(110, 150, 255) * (alpha * 0.4f));
                    Line(sb, pixel, prevX, prevY, px, py, widthCore * taper, new Color(235, 240, 255) * alpha);

                    prevX = px;
                    prevY = py;
                }
            }
        }

        // A straight line between two points in TILE units.
        void Line(SpriteBatch sb, Texture2D pixel, float ax, float ay, float bx, float by, float thickness, Color color)
        {
            float dx = (bx - ax) * 16f;
            float dy = (by - ay) * 16f;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.01f)
                return;

            sb.Draw(pixel, new Vector2(ax * 16f - Main.screenPosition.X, ay * 16f - Main.screenPosition.Y), new Rectangle(0, 0, 1, 1), color, (float)Math.Atan2(dy, dx),
                new Vector2(0f, 0.5f), new Vector2(length + 0.5f, thickness), SpriteEffects.None, 0f);
        }

        void DrawSparks(SpriteBatch sb, Texture2D pixel, uint now)
        {
            if (sparksActive == 0)
                return;

            for (int i = 0; i < sparks.Length; i++)
            {
                ref Spark s = ref sparks[i];
                if (!s.Active)
                    continue;

                float px = s.X * 16f;
                float py = s.Y * 16f;
                if (!BoxVisible(px - 16f, py - 16f, px + 16f, py + 16f))
                    continue;

                float age = (int)(now - s.Born);
                if (age < 0f)
                    continue;   // a delayed spark (the re-emit sparkle) that has not started yet

                float fade = Math.Max(0f, 1f - age / 8f);
                float size = 6f + age * 1.5f;
                sb.Draw(pixel, new Vector2(px - Main.screenPosition.X, py - Main.screenPosition.Y), new Rectangle(0, 0, 1, 1), CosmicColors.DrawColor((CosmicColor)s.Color) * (fade * 0.8f),
                    0f, new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
            }
        }
    }
}
