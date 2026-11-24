using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Liquids
{
    // What the visuals know about one pipe (or pump) tile that liquid is moving through right now.
    public sealed class PipeFlowCell
    {
        public float S;          // path coordinate along the flow: 0 at the pump, +n for the n-th tile downstream, -n for the n-th tile upstream
        public Point Travel;     // the unit step the liquid takes through this tile (the pump's facing downstream, the reverse upstream)
        public int Liquid;       // LiquidTypeRegistry id (colour and dust)
        public float Rate;       // liquid units the pump moves per tick (smoothed, from the pump's quantized FlowRateLevel); scales the particle amount
        public bool IsMotor;     // this cell is the pump itself (swells with the pipes, drawn by its own tile code)
        public int EndMask;      // straight pipes only: bit (1 << Direction) for every end that opens onto empty space
        public float Intensity;  // 0..1, fades in and out so the animation never pops
        public int Stamp;        // the rebuild this cell was last seen in
    }

    // CLIENT ONLY and purely cosmetic: nothing here is saved or sent. It reads what every client already has: the motor replicas
    // (isOn, Facing, FlowLiquidType and FlowRateLevel, which the server writes and sends in the motor's light update) and the pipe tiles.
    //
    // Every few ticks it walks the pipes from each running pump: downstream along its Facing (ON pumps are one-way) and upstream
    // behind it, giving every pipe a path coordinate S (the pump itself is S 0). A wave then runs along S: a "ball" of fluid travels
    // intake -> pump -> outlet, swelling the pipes and the pump it passes and squeezing them a little right behind it.
    //
    // Drawing: EVERY pipe is drawn here (PostDrawTiles; PipeTileBase.PreDraw always says no), scaled by the wave while liquid moves and at
    // scale 1 otherwise, so a pipe never switches between two ways of being drawn (that switch was the flicker). The art outside the
    // tile (TileOverdrawSystem) and the pump (MachineVisualOverride) ask GetTileScale so they swell in step.
    //
    // Liquid itself is plain vanilla dust: bursts from the window of a windowed pipe, a stream out of an open pipe end, and leaks from
    // pipes that touch air. Every amount is liquid units moved per tick times a tuning field below.
    public class PipeFlowVisualSystem : ModSystem
    {
        // ---- Tuning (instance fields on purpose: hot reload can change them) ----
        public float BallSpeedTilesPerSecond = 3.5f;
        public float BallLengthTiles = 3f;        // the swelling lobe; the squeeze lobe right behind it is as long
        public float BallSpacingTiles = 9f;       // distance between two balls (must be more than 2 * BallLengthTiles)
        public float SwellAmount = 0.2f;          // extra scale at the middle of the ball
        public float SqueezeAmount = 0.07f;       // scale lost just behind the ball
        public float MotorSwellMultiplier = 1f;   // the pump swells SwellAmount and squeezes SqueezeAmount times this (0 = the pump stays put)
        public int RebuildIntervalTicks = 10;
        public int MaxWalkPipes = 600;            // per pump and side
        public float MaxRangeTiles = 90f;         // pumps farther than this from the player are not animated
        public float FadePerTick = 0.1f;

        // ---- Particle amounts ----
        // Each is "particles per tick, per liquid unit moved per tick, while the swell is at its peak" (the swell lobe scales it down toward
        // its edges and to 0 outside it). A lead pump moves up to 5 units per tick, an iron pump up to 10, so with the defaults an open
        // end of a full lead pump makes about 1 particle per tick at the swell's peak. Fractions are rolled (0.3 = one particle in three ticks).
        public float OpenEndParticlesPerUnit = 0.2f;   // the stream out of the open end the liquid leaves through
        public float LeakParticlesPerUnit = 0.05f;     // sprays out of any side of a pipe that touches air
        public float WindowParticlesPerUnit = 0.08f;   // drifting inside the window of a windowed pipe
        public int MaxParticlesPerTick = 24;           // a hard cap over all pipes on screen, so a huge network cannot flood the dust pool
        public float ParticleScaleMin = 0.8f;
        public float ParticleScaleMax = 1.1f;
        public float OpenEndSpeedMin = 0.8f;           // pixels per tick
        public float OpenEndSpeedMax = 2.2f;
        public float LeakSpeedMin = 0.4f;
        public float LeakSpeedMax = 1.4f;
        public float WindowSpeedMin = 0.6f;
        public float WindowSpeedMax = 1.4f;

        // Window rectangles in tile pixels (the 16x16 cell, 0,0 = top-left). Window particles appear inside these; match them to the hole in the art.
        public Rectangle WindowHorizontal = new Rectangle(3, 5, 10, 6);
        public Rectangle WindowVertical = new Rectangle(5, 3, 6, 10);

        readonly Dictionary<Point, PipeFlowCell> cells = new Dictionary<Point, PipeFlowCell>();
        readonly Queue<(Point pos, int dist, Point step)> queue = new Queue<(Point, int, Point)>();
        readonly HashSet<Point> walked = new HashSet<Point>();
        readonly List<Point> removeBuffer = new List<Point>();
        readonly List<KeyValuePair<Point, PipeFlowCell>> emitBuffer = new List<KeyValuePair<Point, PipeFlowCell>>();
        int stamp;
        int tickCounter;

        public override void Unload()
        {
            cells.Clear();
            queue.Clear();
            walked.Clear();
            emitBuffer.Clear();
        }

        public override void OnWorldUnload()
        {
            cells.Clear();
        }

        /// <summary>The scale a tile is drawn at right now: Vector2.One when no liquid moves through it. Pipes swell across their axis only (corners and junctions evenly), the pump evenly.</summary>
        public Vector2 GetTileScale(int i, int j, Tile tile)
        {
            if (cells.Count == 0 || !cells.TryGetValue(new Point(i, j), out PipeFlowCell cell))
                return Vector2.One;

            float s = ScaleAt(Phase(cell.S), cell.Intensity);
            if (cell.IsMotor)
                return new Vector2(1f + (s - 1f) * MotorSwellMultiplier);

            int mask = tile.TileFrameX / 18;
            if (mask == 12)
                return new Vector2(1f, s);
            if (mask == 3)
                return new Vector2(s, 1f);
            return new Vector2(s);
        }

        // ------------------------------------------------------------------ update

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            if (Main.gameMenu || Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers)
            {
                if (cells.Count > 0)
                    cells.Clear();
                return;
            }

            tickCounter++;
            if (tickCounter % Math.Max(1, RebuildIntervalTicks) == 0)
                Rebuild();

            FadeCells();
            SpawnParticles();
        }

        void Rebuild()
        {
            stamp++;
            Vector2 playerCenter = Main.LocalPlayer.Center;
            float rangeSq = MaxRangeTiles * 16f;
            rangeSq *= rangeSq;

            foreach (TileEntity entity in TileEntity.ByID.Values)
            {
                if (!(entity is MotorTileEntityBase motor) || !motor.isOn || motor.FlowLiquidType < 0 || motor.FlowLiquidType >= LiquidTypeRegistry.definitions.Count)
                    continue;

                Vector2 motorCenter = new Vector2(motor.Position.X * 16f + 8f, motor.Position.Y * 16f + 8f);
                if (Vector2.DistanceSquared(motorCenter, playerCenter) > rangeSq)
                    continue;

                Walk(motor, true);
                Walk(motor, false);
            }
        }

        // Breadth-first from the pump's front (downstream) or back (upstream) through connected pipes of the same material.
        void Walk(MotorTileEntityBase motor, bool downstream)
        {
            Direction side = downstream ? motor.Facing : motor.Facing.Opposite();
            Point step = side.ToOffset();
            Point motorPos = new Point(motor.Position.X, motor.Position.Y);
            int motorType = Main.tile[motorPos.X, motorPos.Y].TileType;
            float rate = motor.FlowRateLevel * motor.FlowRateStep;

            // The pump itself is the first cell of the wave (path coordinate 0), so it swells with its pipes.
            if (downstream)
                Record(motorPos, 0f, motor.Facing.ToOffset(), motor.FlowLiquidType, rate, Main.tile[motorPos.X, motorPos.Y], true);

            queue.Clear();
            walked.Clear();
            queue.Enqueue((motorPos + step, 1, step));
            int budget = Math.Max(1, MaxWalkPipes);

            while (queue.Count > 0 && budget > 0)
            {
                (Point pos, int dist, Point stepHere) = queue.Dequeue();
                if (!walked.Add(pos) || !WorldGen.InWorld(pos.X, pos.Y, 2))
                    continue;

                Tile tile = Main.tile[pos.X, pos.Y];
                if (!tile.HasTile || !PipeTierRegistry.IsPipeTile(tile.TileType) || !PipeTierRegistry.CanLink(motorType, tile.TileType))
                    continue;

                budget--;
                Record(pos, downstream ? dist : -dist, downstream ? stepHere : new Point(-stepHere.X, -stepHere.Y), motor.FlowLiquidType, rate, tile, false);

                for (int d = 0; d < 4; d++)
                {
                    Point offset = ((Direction)d).ToOffset();
                    queue.Enqueue((pos + offset, dist + 1, offset));
                }
            }
        }

        void Record(Point pos, float s, Point travel, int liquid, float rate, Tile tile, bool isMotor)
        {
            if (!cells.TryGetValue(pos, out PipeFlowCell cell))
            {
                cell = new PipeFlowCell();
                cells[pos] = cell;
            }
            else if (cell.Stamp == stamp && Math.Abs(cell.S) <= Math.Abs(s))
            {
                return;   // another pump already reached this pipe from closer by
            }

            cell.S = s;
            cell.Travel = travel;
            cell.Liquid = liquid;
            cell.Rate = rate;
            cell.IsMotor = isMotor;
            cell.Stamp = stamp;
            cell.EndMask = isMotor ? 0 : ComputeEndMask(pos.X, pos.Y, tile);
        }

        // Which ends of a straight pipe open onto empty space (nothing, not even a wall-less block, next to it).
        static int ComputeEndMask(int i, int j, Tile tile)
        {
            int mask = tile.TileFrameX / 18;
            if (mask == 3)
                return OpenEnd(i, j, Direction.Up) | OpenEnd(i, j, Direction.Down);
            if (mask == 12)
                return OpenEnd(i, j, Direction.Left) | OpenEnd(i, j, Direction.Right);
            return 0;
        }

        static int OpenEnd(int i, int j, Direction dir)
        {
            Point o = dir.ToOffset();
            int x = i + o.X;
            int y = j + o.Y;
            if (!WorldGen.InWorld(x, y, 1) || Main.tile[x, y].HasTile)
                return 0;
            return 1 << (int)dir;
        }

        void FadeCells()
        {
            if (cells.Count == 0)
                return;

            removeBuffer.Clear();
            foreach (KeyValuePair<Point, PipeFlowCell> pair in cells)
            {
                PipeFlowCell cell = pair.Value;
                if (cell.Stamp == stamp)
                {
                    cell.Intensity = Math.Min(1f, cell.Intensity + FadePerTick);
                }
                else
                {
                    cell.Intensity -= FadePerTick;
                    if (cell.Intensity <= 0f)
                        removeBuffer.Add(pair.Key);
                }
            }

            // Safe to drop: a pipe without a cell is drawn at scale 1, exactly what a cell at intensity 0 draws.
            for (int i = 0; i < removeBuffer.Count; i++)
                cells.Remove(removeBuffer[i]);
        }

        // ------------------------------------------------------------------ the wave

        float Time => Main.GameUpdateCount / 60f;

        // Position inside one ball period for a pipe at path coordinate s: 0..BallSpacingTiles. The ball moves toward +s.
        float Phase(float s)
        {
            float spacing = Math.Max(0.5f, BallSpacingTiles);
            float u = (s - Time * BallSpeedTilesPerSecond) % spacing;
            return u < 0f ? u + spacing : u;
        }

        // 0..1 smooth bump over the first BallLengthTiles of the period (the swelling part).
        float Lobe(float u)
        {
            float length = Math.Max(0.5f, BallLengthTiles);
            if (u < 0f || u >= length) return 0f;
            float sine = MathF.Sin(MathF.PI * u / length);
            return sine * sine;
        }

        float ScaleAt(float u, float intensity)
        {
            float length = Math.Max(0.5f, BallLengthTiles);
            float up = Lobe(u);
            float down = Lobe(u - length);   // the same bump, one ball length further on
            float raw = 1f + SwellAmount * up - SqueezeAmount * down;
            return 1f + (raw - 1f) * intensity;
        }

        // ------------------------------------------------------------------ particles

        void SpawnParticles()
        {
            if (cells.Count == 0 || Main.gamePaused)
                return;

            VisibleTileScan.GetRange(1, out int left, out int right, out int top, out int bottom);

            // Collected first so the cap below cuts a random part of a big network, not always the same pipes (dictionary order).
            emitBuffer.Clear();
            foreach (KeyValuePair<Point, PipeFlowCell> pair in cells)
            {
                PipeFlowCell cell = pair.Value;
                Point p = pair.Key;
                if (cell.IsMotor || cell.Intensity < 0.5f || cell.Rate <= 0f || p.X < left || p.X > right || p.Y < top || p.Y > bottom)
                    continue;

                emitBuffer.Add(pair);
            }

            if (emitBuffer.Count == 0)
                return;

            int budget = Math.Max(0, MaxParticlesPerTick);
            int startAt = Main.rand.Next(emitBuffer.Count);

            for (int n = 0; n < emitBuffer.Count && budget > 0; n++)
            {
                KeyValuePair<Point, PipeFlowCell> pair = emitBuffer[(startAt + n) % emitBuffer.Count];
                PipeFlowCell cell = pair.Value;
                Point p = pair.Key;

                // Everything is strongest while the swell passes through this pipe.
                float peak = Lobe(Phase(cell.S)) * cell.Intensity * cell.Rate;
                if (peak <= 0.001f)
                    continue;

                Tile tile = Main.tile[p.X, p.Y];
                if (!tile.HasTile)
                    continue;

                budget -= EmitWindow(p, cell, tile, peak, budget);
                budget -= EmitOpenEnd(p, cell, peak, budget);
                budget -= EmitLeak(p, cell, peak, budget);
            }
        }

        // How many particles to make this tick for an expected amount (the fraction is rolled), capped by what is left of the budget.
        static int Roll(float expected, int budget)
        {
            int count = (int)expected;
            if (Main.rand.NextFloat() < expected - count)
                count++;
            return Math.Min(count, Math.Max(0, budget));
        }

        // Windowed straight pipes only (TileFrameY 18 = the windowed art): drifting along the pipe inside the window.
        int EmitWindow(Point p, PipeFlowCell cell, Tile tile, float peak, int budget)
        {
            if (tile.TileFrameY < 18)
                return 0;

            int mask = tile.TileFrameX / 18;
            bool horizontal = mask == 12;
            if (!horizontal && mask != 3)
                return 0;

            int sign = horizontal ? cell.Travel.X : cell.Travel.Y;
            if (sign == 0)
                return 0;

            int count = Roll(peak * WindowParticlesPerUnit, budget);
            Rectangle window = horizontal ? WindowHorizontal : WindowVertical;

            for (int k = 0; k < count; k++)
            {
                Vector2 position = new Vector2(p.X * 16f + window.X + Main.rand.NextFloat() * window.Width, p.Y * 16f + window.Y + Main.rand.NextFloat() * window.Height);
                float speed = Main.rand.NextFloat(WindowSpeedMin, WindowSpeedMax) * sign;
                Vector2 velocity = horizontal ? new Vector2(speed, Main.rand.NextFloat(-0.1f, 0.1f)) : new Vector2(Main.rand.NextFloat(-0.1f, 0.1f), speed);
                SpawnDust(position, velocity, cell.Liquid, false);
            }
            return count;
        }

        // Straight pipes: the end the liquid leaves through, when it opens onto air. A stream that arcs down.
        int EmitOpenEnd(Point p, PipeFlowCell cell, float peak, int budget)
        {
            if (cell.EndMask == 0)
                return 0;

            for (int d = 0; d < 4; d++)
            {
                if ((cell.EndMask & (1 << d)) == 0)
                    continue;

                Point o = ((Direction)d).ToOffset();
                if (o != cell.Travel)
                    continue;

                int count = Roll(peak * OpenEndParticlesPerUnit, budget);
                for (int k = 0; k < count; k++)
                {
                    Vector2 position = new Vector2(p.X * 16f + 8f + o.X * 8f, p.Y * 16f + 8f + o.Y * 8f) + new Vector2(Main.rand.NextFloat(-2f, 2f) * Math.Abs(o.Y), Main.rand.NextFloat(-2f, 2f) * Math.Abs(o.X));
                    Vector2 velocity = new Vector2(o.X, o.Y) * Main.rand.NextFloat(OpenEndSpeedMin, OpenEndSpeedMax) + new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-0.35f, 0f));
                    SpawnDust(position, velocity, cell.Liquid, true);
                }
                return count;
            }
            return 0;
        }

        // Any pipe with air next to it: sprays out of one of the open sides.
        int EmitLeak(Point p, PipeFlowCell cell, float peak, int budget)
        {
            int count = Roll(peak * LeakParticlesPerUnit, budget);
            if (count <= 0)
                return 0;

            int made = 0;
            for (int k = 0; k < count; k++)
            {
                int start = Main.rand.Next(4);
                for (int n = 0; n < 4; n++)
                {
                    Point o = ((Direction)((start + n) % 4)).ToOffset();
                    if (!WorldGen.InWorld(p.X + o.X, p.Y + o.Y, 1) || Main.tile[p.X + o.X, p.Y + o.Y].HasTile)
                        continue;

                    Vector2 position = new Vector2(p.X * 16f + 8f + o.X * 8f, p.Y * 16f + 8f + o.Y * 8f);
                    Vector2 velocity = new Vector2(o.X, o.Y) * Main.rand.NextFloat(LeakSpeedMin, LeakSpeedMax) + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.4f, 0f));
                    SpawnDust(position, velocity, cell.Liquid, true);
                    made++;
                    break;
                }
            }
            return made;
        }

        void SpawnDust(Vector2 position, Vector2 velocity, int liquid, bool gravity)
        {
            int dustType;
            Color tint = default;
            if (LiquidTypeRegistry.IsWater(liquid)) dustType = DustID.Water;
            else if (liquid == LiquidTypeRegistry.Lava) dustType = DustID.Lava;
            else if (liquid == LiquidTypeRegistry.Honey) dustType = DustID.Honey;
            else
            {
                dustType = DustID.RainbowMk2;   // the tintable one: Oil, Fuel, Shimmer and anything added later get their registered colour
                tint = LiquidColor(liquid);
            }

            Dust dust = Dust.NewDustPerfect(position, dustType, velocity, 0, tint, Main.rand.NextFloat(ParticleScaleMin, Math.Max(ParticleScaleMin, ParticleScaleMax)));
            dust.noGravity = !gravity;
        }

        static Color LiquidColor(int liquid)
        {
            if (liquid < 0 || liquid >= LiquidTypeRegistry.definitions.Count)
                return Color.White;

            Color c = LiquidTypeRegistry.Get(liquid).RenderColor;
            return new Color(c.R, c.G, c.B, (byte)255);
        }

        // ------------------------------------------------------------------ drawing

        // Draws every pipe on screen (PipeTileBase.PreDraw skips the vanilla draw for all of them), after all tiles so a swollen pipe
        // covers its neighbours instead of being painted over by them.
        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            VisibleTileScan.GetRange(2, out int left, out int right, out int top, out int bottom);
            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = left; i <= right; i++)
            {
                for (int j = top; j <= bottom; j++)
                {
                    Tile tile = Main.tile[i, j];
                    if (!tile.HasTile || !PipeTierRegistry.IsPipeTile(tile.TileType))
                        continue;

                    DrawPipe(spriteBatch, i, j, tile);
                }
            }

            spriteBatch.End();
        }

        void DrawPipe(SpriteBatch spriteBatch, int i, int j, Tile tile)
        {
            Texture2D texture = Main.instance.TilesRenderer.GetTileDrawTexture(tile, i, j);
            if (texture == null)
                return;

            Color light = Lighting.GetColor(i, j);
            if (tile.IsActuated)
                light = new Color((byte)(light.R * 0.4f), (byte)(light.G * 0.4f), (byte)(light.B * 0.4f), light.A);

            Vector2 scale = GetTileScale(i, j, tile);
            Vector2 center = new Vector2(i * 16f + 8f, j * 16f + 8f) - Main.screenPosition;
            Rectangle source = new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16);
            spriteBatch.Draw(texture, center, source, light, 0f, new Vector2(8f, 8f), scale, SpriteEffects.None, 0f);
        }
    }
}
