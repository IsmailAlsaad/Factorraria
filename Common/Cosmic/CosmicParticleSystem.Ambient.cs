using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Cosmic
{
    // Patch C7 (client only): the ambient LOCAL SIMULATION of cosmic particles around the local player, strike flashes and light, and the
    // EnableDebugs overlay. This is the second half of CosmicParticleSystem (a partial class); it uses the same Particle struct and the
    // same DrawParticleSet as the real particles.
    //
    // AUTHORITY: none. Nothing here is saved, sent, or read by any server logic. It never touches a counter (CosmicSimulation and its
    // counters are not referenced except for READING them in the debug overlay), never calls a sink, never changes the index, a machine or
    // a receiver. A dedicated server never runs it.
    //
    // WHAT IT SIMULATES: the same particles the real sky has, with the same rules. A local particle starts in the particle band with the
    // same density formula as the server's sampler (CosmicSampling.AmbientExpectedPerTick), the same heatmap color, the same tilt, flies
    // straight at ParticleSpeed and is traced with the SAME CosmicRayTracer against the real tiles: only a Lead Brick stops it, a gemspark
    // of its own color or a cloud would absorb it. Grey (below red) has no gemspark at all, so it passes every gemspark.
    //
    // NO DOUBLE DRAWING: the server only simulates (and sends) particles where a receiver exists (roadmap N0), so around a receiver the
    // real particles already fill the sky. The local simulation therefore switches itself OFF whenever the player is within
    // LocalSimOffRadiusTiles of any receiver (a cloud block or a lit gemspark), found by a cheap rolling scan of the player's own tiles.
    // Particles already flying keep going and die by themselves. As a safety net a local particle that would hit a receiver is dropped at
    // spawn, because the real one is the server's to draw.
    //
    // PERFORMANCE BUDGET (per tick, client): the receiver scan reads about (2 x radius + 1) / 30 columns x up to 2 x radius rows, about 2000
    // tile reads at the default radius of 120; at most 24 new particles per tick, each one a tracer walk of at most 2 x ParticleRange tile
    // reads (a burst of up to LocalSimMaxParticles on switching on or after a teleport); a fixed pool of LocalSimMaxParticles (no
    // allocation per tick); drawing costs one rectangle test per particle and about 8 quads per visible one.
    public partial class CosmicParticleSystem : ModSystem
    {
        Particle[] ambient = new Particle[0];
        int ambientNext;
        int ambientActive;
        readonly Random ambientRng = new Random();
        readonly CosmicTileProbe ambientProbe = new CosmicTileProbe();

        // the receiver scan (rolling, one cycle is about 30 ticks)
        bool scanActive;
        int scanCenterX, scanCenterY, scanColumn;
        bool scanFound;
        bool ambientNear;
        int ambientClearCycles;
        bool ambientWasOn;
        int ambientLastX, ambientLastY;
        int scanRadiusUsed = 120;

        // C7b: how visible the ambient sky is right now, 0 to 1. It eases toward 1 while the simulation is allowed and toward 0 while it is
        // not (LocalSimFadeSeconds for the whole way), so it fades in and out instead of switching. It scales both how many particles are
        // spawned and how bright they are drawn.
        float ambientFade;

        // What /fcosmic fx and the debug overlay show.
        public bool AmbientOn { get; private set; }
        public string AmbientOffReason = "not started";
        public long AmbientSpawned, AmbientDropped, AmbientSkippedFull;

        void ClearAmbient()
        {
            ambient = new Particle[0];
            ambientNext = 0;
            ambientActive = 0;
            scanActive = false;
            scanFound = false;
            ambientNear = false;
            ambientClearCycles = 0;
            ambientWasOn = false;
            ambientFade = 0f;
            AmbientOn = false;
            AmbientOffReason = "not started";
            AmbientSpawned = AmbientDropped = AmbientSkippedFull = 0;
        }

        string AmbientText()
        {
            return "ambient " + ambientActive + " of " + ambient.Length + " " + (AmbientOn ? "ON" : "OFF (" + AmbientOffReason + ")") + ", fade " + (int)(ambientFade * 100f) + "%"
                + ", spawned " + AmbientSpawned + ", dropped because they would hit a receiver " + AmbientDropped + ", skipped because the pool was full " + AmbientSkippedFull;
        }

        // The simulation is not allowed right now: ease the sky out. Once it is fully faded the particles still in the pool are dropped, so
        // the next switch-on starts from an empty pool and pre-fills the sky (which then fades in).
        void AmbientOff(string reason)
        {
            AmbientOn = false;
            AmbientOffReason = reason;
            ambientWasOn = false;
            StepFade(false);

            if (ambientFade <= 0f && ambientActive > 0)
            {
                Array.Clear(ambient, 0, ambient.Length);
                ambientActive = 0;
            }
        }

        // One tick of the fade toward on (true) or off (false). LocalSimFadeSeconds <= 0 switches at once.
        void StepFade(bool on)
        {
            float seconds = CosmicRadiationSystem.Instance.LocalSimFadeSeconds;
            float step = seconds <= 0.01f ? 1f : 1f / (60f * seconds);
            ambientFade = on ? Math.Min(1f, ambientFade + step) : Math.Max(0f, ambientFade - step);
        }

        // ------------------------------------------------------------ update (main thread, client only)
        void UpdateAmbient(uint now)
        {
            CosmicRadiationSystem s = CosmicRadiationSystem.Instance;

            int wanted = Math.Max(0, s.LocalSimMaxParticles);
            if (ambient.Length != wanted)
            {
                ambient = new Particle[wanted];   // only the first time or when the tunable changes (hot reload)
                ambientNext = 0;
            }

            int active = 0;
            for (int i = 0; i < ambient.Length; i++)
            {
                if (!ambient[i].Active)
                    continue;

                if ((float)(int)(now - ambient[i].Born) > ambient[i].Life)
                    ambient[i].Active = false;
                else
                    active++;
            }

            ambientActive = active;

            if (!s.LocalSimEnabled)
            {
                AmbientOff("turned off with LocalSimEnabled");
                return;
            }

            Player player = Main.LocalPlayer;
            if (Main.gameMenu || player == null || !player.active)
            {
                AmbientOff("no local player");
                return;
            }

            CosmicHeatmap heat = s.Heatmap;
            if (!heat.Ready)
            {
                AmbientOff("waiting for the heatmap");
                return;
            }

            float band = (float)(Main.worldSurface * s.SpaceBottomFraction);
            int px = (int)(player.Center.X / 16f);
            int py = (int)(player.Center.Y / 16f);

            // 1. Receiver guard: the real particles own every sky that has a receiver nearby.
            ScanForReceivers(s, px, py, band);
            if (ambientNear)
            {
                AmbientOff("a receiver (cloud block or lit gemspark) is within " + scanRadiusUsed + " tiles, the real particles cover it");
                return;
            }

            if (ambientClearCycles < 2)
            {
                AmbientOff("checking for receivers nearby");
                return;
            }

            // 2. Rows a particle can start at and still pass through the area around the player.
            float halfW = Math.Max(10f, s.LocalSimHalfWidthTiles);
            float halfH = Math.Max(10f, s.LocalSimHalfHeightTiles);
            if (!CosmicSampling.TryAmbientStartRows(py - halfH, py + halfH, s.ParticleRange, band, out float y0, out float y1))
            {
                AmbientOff("outside the particle band (nothing falls this far down)");
                return;
            }

            AmbientOn = true;
            AmbientOffReason = "";

            // Just switched on from nothing, or the player jumped (teleport): fill the area with particles that are already part of the way
            // down, so the sky does not start empty. They are drawn at the current fade, so they fade in instead of popping. A brief off
            // (some particles still alive) is not fresh: the density just ramps back up with the fade.
            bool jumped = ambientWasOn && (Math.Abs(px - ambientLastX) > 40 || Math.Abs(py - ambientLastY) > 40);
            bool fresh = (!ambientWasOn && ambientActive == 0) || jumped;
            if (jumped)
                ambientFade = 0f;   // a teleport: the new sky fades in from nothing

            StepFade(true);
            ambientWasOn = true;
            ambientLastX = px;
            ambientLastY = py;

            // 3. How many: the same density as the real sky.
            int world = Main.maxTilesX;
            float weight = 0f;
            for (int c = 0; c < CosmicColors.Count; c++)
                weight += heat.Weight((CosmicColor)c, Math.Clamp(px, 0, world - 1), world, s.HeatFloor, s.HeatSigmaFraction);

            double perTick = CosmicSampling.AmbientExpectedPerTick(s.RateDensity, s.LocalSimDensityBoost, weight, y0, y1, band, halfW * 2f);
            double flightTicks = (s.ParticleRange + Math.Max(0.5f, s.ClientTrailTiles)) / Math.Max(0.01f, s.ParticleSpeed);
            int count = CosmicSampling.Poisson(fresh ? perTick * flightTicks : perTick * ambientFade, ambientRng);
            count = Math.Min(count, fresh ? ambient.Length : 24);

            for (int k = 0; k < count; k++)
                SpawnAmbient(s, now, px, halfW, y0, y1, band, world, heat, fresh);
        }

        void SpawnAmbient(CosmicRadiationSystem s, uint now, int px, float halfW, float y0, float y1, float band, int world, CosmicHeatmap heat, bool fresh)
        {
            int slot = -1;
            for (int n = 0; n < ambient.Length; n++)
            {
                int i = (ambientNext + n) % ambient.Length;
                if (!ambient[i].Active)
                {
                    slot = i;
                    ambientNext = (i + 1) % ambient.Length;
                    break;
                }
            }

            if (slot < 0)
            {
                AmbientSkippedFull++;
                return;
            }

            float x = Math.Clamp(px - halfW + (float)ambientRng.NextDouble() * halfW * 2f, 0.5f, world - 0.5f);
            float y = CosmicSampling.SampleY(y0, y1, band, ambientRng.NextDouble());
            float tilt = ((float)ambientRng.NextDouble() * 2f - 1f) * s.SpawnSpreadDeg;
            CosmicRayTracer.DirectionFromTilt(tilt, out float dirX, out float dirY);
            CosmicColor color = heat.Pick((int)x, world, s.HeatFloor, s.HeatSigmaFraction, ambientRng);

            // The real rules: the same tracer on the real tiles. Only a Lead Brick (or the world edge) ends it early. A gemspark of another
            // color is transparent, and a color with no gemspark (Grey) is never absorbed by one.
            CosmicRayResult r = CosmicRayTracer.Trace(ambientProbe, x, y, dirX, dirY, color, s.ParticleRange, s.CloudAbsorbChance, ambientRng);
            if (r.End == CosmicRayEnd.Absorb || r.End == CosmicRayEnd.Ionize)
            {
                AmbientDropped++;   // a receiver would take it: that one is the server's to draw
                return;
            }

            float length = r.End == CosmicRayEnd.Solid ? r.Distance : s.ParticleRange;
            if (length < 0.5f)
                return;

            float speed = Math.Max(0.01f, s.ParticleSpeed);
            float trail = Math.Max(0.5f, s.ClientTrailTiles);
            float life = (length + trail) / speed + 2f;
            float age = fresh ? (float)ambientRng.NextDouble() * life : 0f;

            ref Particle p = ref ambient[slot];
            p.Active = true;
            p.Color = (byte)color;
            p.End = 0;
            p.Born = now - (uint)age;
            p.X = x;
            p.Y = y;
            p.DirX = dirX;
            p.DirY = dirY;
            p.Length = length;
            p.Speed = speed;
            p.Life = life;
            AmbientSpawned++;
        }

        // Rolling scan for receivers in the box around the player, about 30 ticks per full pass. Finding one switches the simulation off at
        // once; it switches back on only after two clean passes in a row (so it does not flicker at the edge).
        void ScanForReceivers(CosmicRadiationSystem s, int px, int py, float band)
        {
            int radius = Math.Max(8, (int)s.LocalSimOffRadiusTiles);
            scanRadiusUsed = radius;

            if (!scanActive)
            {
                scanActive = true;
                scanCenterX = px;
                scanCenterY = py;
                scanColumn = 0;
                scanFound = false;
            }

            int width = radius * 2 + 1;
            int perTick = (width + 29) / 30;
            int deepest = Math.Min(Main.maxTilesY - 1, (int)(band + s.ParticleRange) + 2);   // the same rows the server indexes
            int rowFrom = Math.Max(0, scanCenterY - radius);
            int rowTo = Math.Min(deepest, scanCenterY + radius);
            int columnFrom = scanCenterX - radius;

            for (int n = 0; n < perTick && scanColumn < width && !scanFound; n++)
            {
                int x = columnFrom + scanColumn;
                scanColumn++;

                if (x < 0 || x >= Main.maxTilesX)
                    continue;

                for (int y = rowFrom; y <= rowTo; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasUnactuatedTile)
                        continue;

                    if (CosmicTileCodes.CodeOf(tile.TileType) != CosmicTileCodes.None)
                    {
                        scanFound = true;
                        break;
                    }
                }
            }

            if (scanFound || scanColumn >= width)
            {
                ambientNear = scanFound;
                ambientClearCycles = scanFound ? 0 : Math.Min(ambientClearCycles + 1, 1000);
                scanActive = false;
            }
        }

        // ------------------------------------------------------------ strike light (main thread, client only)
        void UpdateBoltLight(uint now)
        {
            if (boltsActive == 0)
                return;

            float intensity = CosmicRadiationSystem.Instance.BoltLightIntensity;
            if (intensity <= 0f)
                return;

            for (int i = 0; i < bolts.Length; i++)
            {
                ref Bolt b = ref bolts[i];
                if (!b.Active)
                    continue;

                float fade = Math.Max(0f, 1f - (int)(now - b.Born) / (float)b.Life);
                float power = fade * intensity * (1f + Math.Min(2f, b.Tier * 0.05f));

                Lighting.AddLight((int)b.AX, (int)b.AY, 0.55f * power, 0.7f * power, 1f * power);
                if (!b.Thin)
                    Lighting.AddLight((int)b.BX, (int)b.BY, 0.55f * power, 0.7f * power, 1f * power);
            }
        }

        // ------------------------------------------------------------ draw (client only, inside the additive batch of PostDrawTiles)
        void DrawAmbient(SpriteBatch sb, Texture2D pixel, uint now, CosmicRadiationSystem system)
        {
            if (ambientActive == 0 || ambientFade <= 0f)
                return;

            float eased = ambientFade * ambientFade * (3f - 2f * ambientFade);   // smoothstep, so the ends of the fade are gentle too
            DrawParticleSet(sb, pixel, now, system, ambient, Math.Max(0f, system.LocalSimBrightness) * eased);
        }

        // A short flash where the lightning starts and where it lands (the first 6 ticks of a bolt).
        void DrawStrikeFlash(SpriteBatch sb, Texture2D pixel, uint now, CosmicRadiationSystem system)
        {
            if (boltsActive == 0 || system.StrikeFlashSizePx <= 0f)
                return;

            for (int i = 0; i < bolts.Length; i++)
            {
                ref Bolt b = ref bolts[i];
                if (!b.Active)
                    continue;

                float age = (int)(now - b.Born);
                if (age >= 6f)
                    continue;

                float fade = 1f - age / 6f;
                float size = system.StrikeFlashSizePx * fade * (1f + Math.Min(2f, b.Tier * 0.05f));

                FlashAt(sb, pixel, b.AX, b.AY, size * (b.Thin ? 0.6f : 0.5f), fade);
                if (!b.Thin)
                    FlashAt(sb, pixel, b.BX, b.BY, size, fade);
            }
        }

        void FlashAt(SpriteBatch sb, Texture2D pixel, float tileX, float tileY, float sizePx, float fade)
        {
            float px = tileX * 16f;
            float py = tileY * 16f;
            if (!BoxVisible(px - sizePx, py - sizePx, px + sizePx, py + sizePx))
                return;

            // Two round discs (a wide blue one and a small white core), same extents as the old squares.
            Texture2D disc = FlashDisc();
            Vector2 pos = new Vector2(px - Main.screenPosition.X, py - Main.screenPosition.Y);
            Vector2 middle = new Vector2(disc.Width / 2f, disc.Height / 2f);
            float outer = sizePx * 2f / disc.Width;
            float core = sizePx * 0.8f / disc.Width;
            sb.Draw(disc, pos, null, new Color(110, 150, 255) * (fade * 0.35f), 0f, middle, outer, SpriteEffects.None, 0f);
            sb.Draw(disc, pos, null, new Color(235, 240, 255) * (fade * 0.8f), 0f, middle, core, SpriteEffects.None, 0f);
        }

        // A solid round disc with a soft rim, made once on the main thread (the first time a flash is drawn). The colors are premultiplied,
        // like every texture the game draws, so it also works inside the additive batch.
        Texture2D flashDisc;

        Texture2D FlashDisc()
        {
            if (flashDisc != null && !flashDisc.IsDisposed)
                return flashDisc;

            int size = 64;
            Color[] data = new Color[size * size];
            float radius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float d = MathF.Sqrt(dx * dx + dy * dy) / radius;
                    float t = Math.Clamp((d - 0.55f) / 0.45f, 0f, 1f);
                    float a = 1f - t * t * (3f - 2f * t);
                    data[y * size + x] = new Color(a, a, a, a);
                }
            }

            flashDisc = new Texture2D(Main.instance.GraphicsDevice, size, size);
            flashDisc.SetData(data);
            return flashDisc;
        }

        public override void Unload()
        {
            Texture2D disc = flashDisc;
            flashDisc = null;

            if (disc != null)
                Main.QueueMainThreadAction(() => disc.Dispose());
        }

        // ------------------------------------------------------------ EnableDebugs overlay
        static bool DebugOn
        {
            get
            {
                FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
                return config != null && config.EnableDebugs;
            }
        }

        // World-space part (inside the additive batch): the sampling "rig" boxes of the server, the ambient window and the receiver guard box.
        void DrawDebugWorld(SpriteBatch sb, Texture2D pixel, CosmicRadiationSystem system)
        {
            // Rig boxes: runs of neighboring sampling strips (server and single-player only, a multiplayer client has no simulation).
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                IReadOnlyList<CosmicSamplingStrip> strips = system.Sim.SamplingList;
                int i = 0;
                while (i < strips.Count)
                {
                    int j = i;
                    float lo = strips[i].Y0;
                    float hi = strips[i].Y1;
                    while (j + 1 < strips.Count && strips[j + 1].Strip == strips[j].Strip + 1)
                    {
                        j++;
                        lo = Math.Min(lo, strips[j].Y0);
                        hi = Math.Max(hi, strips[j].Y1);
                    }

                    float left = strips[i].Strip * CosmicReceiverIndex.StripWidth;
                    float right = (strips[j].Strip + 1) * CosmicReceiverIndex.StripWidth;
                    DebugBox(sb, pixel, left, lo, right, hi, new Color(255, 220, 60) * 0.7f);
                    i = j + 1;
                }
            }

            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                float cx = player.Center.X / 16f;
                float cy = player.Center.Y / 16f;
                float halfW = Math.Max(10f, system.LocalSimHalfWidthTiles);
                float halfH = Math.Max(10f, system.LocalSimHalfHeightTiles);
                float guard = Math.Max(8f, system.LocalSimOffRadiusTiles);
                DebugBox(sb, pixel, cx - halfW, cy - halfH, cx + halfW, cy + halfH, (AmbientOn ? new Color(80, 255, 160) : new Color(255, 90, 90)) * 0.7f);
                DebugBox(sb, pixel, cx - guard, cy - guard, cx + guard, cy + guard, new Color(255, 150, 50) * 0.5f);
            }
        }

        void DebugBox(SpriteBatch sb, Texture2D pixel, float x0, float y0, float x1, float y1, Color color)
        {
            if (!BoxVisible(x0 * 16f, y0 * 16f, x1 * 16f, y1 * 16f))
                return;

            Line(sb, pixel, x0, y0, x1, y0, 2f, color);
            Line(sb, pixel, x1, y0, x1, y1, 2f, color);
            Line(sb, pixel, x1, y1, x0, y1, 2f, color);
            Line(sb, pixel, x0, y1, x0, y0, 2f, color);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (index < 0)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer("Factorraria: Cosmic debug", delegate
            {
                DrawDebugInterface(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.UI));
        }

        // Screen-space part: the counters and the heatmap curve (every color's weight across the whole world, the player's column marked).
        void DrawDebugInterface(SpriteBatch sb)
        {
            if (Main.dedServ || Main.gameMenu || !DebugOn)
                return;

            CosmicRadiationSystem system = CosmicRadiationSystem.Instance;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int left = 20;
            int top = 230;

            List<string> lines = new List<string>();
            lines.Add("Cosmic [debug]: " + ActiveCount + " of " + pool.Length + " real particles, glow blocks " + glows.Count + ", bolts " + boltsActive + ", sparks " + sparksActive);
            lines.Add("Cosmic [debug]: " + AmbientText());

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                CosmicSimulation sim = system.Sim;
                lines.Add("Cosmic [debug] server side: receivers " + sim.Index.Total + " (clouds " + sim.Index.CloudCount + "), rigs " + sim.Rigs + ", queue " + sim.Heap.Count + ", " + sim.RaysPerSecond.ToString("0.#") + " rays/s, " + sim.AvgMsPerTick.ToString("0.000") + " ms/tick");
            }

            int y = top;
            for (int i = 0; i < lines.Count; i++)
            {
                Utils.DrawBorderString(sb, lines[i], new Vector2(left, y), Color.White, 0.75f);
                y += 18;
            }

            CosmicHeatmap heat = system.Heatmap;
            if (!heat.Ready)
                return;

            int width = 320;
            int height = 48;
            y += 4;
            sb.Draw(pixel, new Rectangle(left, y, width, height), new Rectangle(0, 0, 1, 1), Color.Black * 0.55f);

            int world = Math.Max(1, Main.maxTilesX);
            int samples = 160;
            int barWidth = Math.Max(1, width / samples);

            for (int c = 0; c < CosmicColors.Count; c++)
            {
                Color color = CosmicColors.DrawColor((CosmicColor)c);
                for (int i = 0; i < samples; i++)
                {
                    int tileX = (int)((i + 0.5f) / samples * world);
                    float weight = heat.Weight((CosmicColor)c, tileX, world, system.HeatFloor, system.HeatSigmaFraction);
                    int bar = (int)(Math.Min(1f, weight) * (height - 2));
                    sb.Draw(pixel, new Rectangle(left + i * width / samples, y + height - bar, barWidth, 2), new Rectangle(0, 0, 1, 1), color * 0.85f);
                }
            }

            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                int markX = left + (int)(Math.Clamp(player.Center.X / 16f / world, 0f, 1f) * width);
                sb.Draw(pixel, new Rectangle(markX, y, 1, height), new Rectangle(0, 0, 1, 1), Color.White * 0.9f);
            }

            Utils.DrawBorderString(sb, "heatmap: weight of each color across the world (white line = you)", new Vector2(left, y + height + 2), Color.White, 0.65f);
        }
    }
}