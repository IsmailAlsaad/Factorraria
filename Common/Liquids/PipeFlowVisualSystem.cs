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
    // One chain of pumps (a lone pump, or pumps that feed each other's backs) and the balls of fluid travelling through all its pipes.
    public sealed class PumpChain
    {
        public readonly List<FlowBall> Balls = new List<FlowBall>();   // every ball in flight, oldest first
        public float EmitProgress;   // 0..1: how far the chain is toward emitting its next ball (one ball per chunk of liquid moved)
        public float Rate;           // liquid units per tick the chain moves
        public float StableRate;     // Rate held steady against small wobble (PipeFlowVisualSystem.HoldRate): what the balls' speed, spacing and making use
        public float Min, Max;       // path coordinates S of the first and the last tile of the chain
        public float NextMin, NextMax;   // being collected by the walk in progress
        public bool Primed;          // the balls a running chain already has when it is first seen were created
        public bool Synced;          // lined up with a real chunk at least once
        public int Stamp;
    }

    // One ball: it is made at the source pipe with the speed the chain had at that moment and keeps that speed until it leaves at the end pipe,
    // so a change of the pump rate only shows in the balls made after it.
    public sealed class FlowBall
    {
        public float Pos;     // path coordinate S of its centre
        public float Speed;   // tiles per tick
    }

    // What the visuals know about one pipe (or pump) tile that liquid is moving through right now.
    public sealed class PipeFlowCell
    {
        public float S;          // path coordinate along the flow: the pump's own phase, +n for the n-th tile downstream of it, -n for the n-th tile upstream
        public float Dist;       // tiles between this cell and the pump that reached it (a pipe two pumps reach keeps the closer pump)
        public Point Travel;     // the unit step the liquid takes through this tile (the pump's facing downstream, the reverse upstream)
        public int Liquid;       // LiquidTypeRegistry id (colour and dust)
        public float Rate;       // liquid units the pump moves per tick (smoothed, from the pump's quantized FlowRateLevel); scales the particle amount
        public bool IsMotor;     // this cell is the pump itself (swells with the pipes, drawn by its own tile code)
        public int EndMask;      // straight pipes only: bit (1 << Direction) for every end that opens onto empty space
        public float Intensity;  // 0..1, fades in and out so the animation never pops
        public int Stamp;        // the rebuild this cell was last seen in
        public PumpChain Chain;  // the balls that pass through this cell
        public int WaveStamp;    // the tick WaveU was worked out in (BallU)
        public float WaveU;      // where this tile is relative to its nearest ball (BallU)
    }

    // CLIENT ONLY and purely cosmetic: nothing here is saved or sent. It reads what every client already has: the motor replicas
    // (isOn, Facing, FlowLiquidType and FlowRateLevel, which the server writes and sends in the motor's light update) and the pipe tiles.
    //
    // Every few ticks it walks the pipes from each running pump: downstream along its Facing (ON pumps are one-way) and upstream
    // behind it, giving every pipe a path coordinate S. A pump that is switched OFF is a plain pipe, so the walk goes through it. Pumps in a
    // chain (one feeding the back of the next) continue each other's S, and share one ball.
    //
    // The balls: exactly one ball per chunk of liquid (ChunkUnits, 255, a full world cell). A ball is made at the source pipe and then only
    // ever moves forward, at the speed the chain had when it was made: BallSpeedTilesPerSecond, so the gap between two balls is that speed
    // times the time the pump needs to move a chunk (a slower ball speed means more balls, closer together, and a faster pump means more of
    // them too). When that gap would be smaller than BallMinSpacingTiles it stays at the minimum and the balls are faster instead, so one ball
    // per chunk always holds. A change of the pump rate therefore never touches the balls already in the pipes: the new slower or faster balls
    // start at the source and travel through the network, replacing the old pattern as they go (a faster ball that catches a slower one
    // simply passes it). When the network delivers a real chunk (MotorTileEntityBase.FlowChunkCount changes: a world cell received liquid, or
    // 255 units went into a machine) the moment the next balls are made is lined up so one of them reaches the end pipe at the delivery
    // moments (the travel time is known, so no ball is ever pulled back or pushed). The count travels in the motor's light update, so every
    // client sees the same moments.
    //
    // Drawing: EVERY pipe is drawn here (PostDrawTiles; PipeTileBase.PreDraw always says no), scaled by the wave while liquid moves and at
    // scale 1 otherwise, so a pipe never switches between two ways of being drawn (that switch was the flicker). The art outside the
    // tile (TileOverdrawSystem) and the pump (MachineVisualOverride) ask GetTileScale so they swell in step.
    //
    // Liquid itself uses water dust (the biome dust for the water liquids, plain Water dust tinted to the liquid's RenderColor for every other
    // liquid): a stream out of an open pipe end and now and then a leak from a pipe that touches air, both real dust,
    // plus the liquid drifting inside the window of a windowed pipe. The window particles are not dust: they are drawn under the pipe (see
    // DrawWindowParticles) so the pipe's frame covers them and they look like they are inside it.
    public class PipeFlowVisualSystem : ModSystem
    {
        // ---- Tuning (expression-bodied properties on purpose: a field initializer only runs when the object is created, so hot reload never
        //      changed it; a property body is code, and hot reload replaces it) ----
        public float ChunkUnits => 255f;           // liquid units in a full world cell: one ball per this much liquid moved
        public float BallSpeedTilesPerSecond => 4f;   // how fast the balls travel while they are not crowded: slower = more balls, closer together
        public float BallMinSpacingTiles => 5f;       // the closest two balls may get (the swell is BallLengthTiles long); past that the balls go faster instead of more frequent

        public float SyncGain => 0.5f;             // 0..1: how much of the timing error the making of new balls corrects at each real chunk (the first one lines it up fully)
        public float SyncDeadbandTicks => 3f;      // a chunk that arrives this many ticks off the expected moment is not corrected (the delivery itself jitters by a tick or two)
        public float SyncMaxStep => 0.005f;        // the most the making of balls is shifted at one chunk, as a part of the gap between two balls (every shift is an uneven gap)
        public float RateHoldFraction => 0.08f;    // the rate the balls use only changes when the pump's rate moves by more than this fraction (smoothing + rounding wobble must not change ball speeds)
        public float BallLengthTiles => 3f;        // the swelling lobe; the squeeze lobe right behind it is as long

        public float SwellAmount => 0.15f;          // extra scale at the middle of the ball
        public float SqueezeAmount => 0f;       // scale lost just behind the ball
        public float MotorSwellMultiplier => 1f;   // the pump swells SwellAmount and squeezes SqueezeAmount times this (0 = the pump stays put)
        public int RebuildIntervalTicks => 10;
        public int MaxWalkPipes => 600;            // per pump and side
        public float MaxRangeTiles => 90f;         // pumps farther than this from the player are not animated
        public float FadePerTick => 0.1f;

        // ---- Particle amounts ----
        // The open end is "particles per tick, per liquid unit moved per tick, while the swell is at its peak" (the swell lobe scales it down toward
        // its edges and to 0 outside it). A lead pump moves up to 5 units per tick, an iron pump up to 10, so with the defaults an open
        // end of a full lead pump makes about 1 particle per tick at the swell's peak. Fractions are rolled (0.3 = one particle in three ticks).
        // The window drifters use the same unit but run the whole time the liquid moves (not tied to the swell).
        public float OpenEndParticlesPerUnit => 0.2f;   // the stream out of the open end the liquid leaves through
        public float LeakChancePerTick => 0.012f;       // a pipe that touches air leaks only now and then: this chance per tick at the swell's peak (scaled down toward its edges)
        public int LeakBurstMin => 1;                   // particles in one leak
        public int LeakBurstMax => 2;
        public float WindowParticlesPerUnit => 0.26f;   // drifting inside the window of a windowed pipe, continuously
        public int MaxParticlesPerTick => 24;           // a hard cap over all pipes on screen, so a huge network cannot flood the dust pool
        public float ParticleScaleMin => 0.8f;
        public float ParticleScaleMax => 1.1f;
        public float OpenEndSpeedMin => 0.8f;           // pixels per tick
        public float OpenEndSpeedMax => 2.2f;
        public float LeakSpeedMin => 0.4f;
        public float LeakSpeedMax => 1.4f;
        public float WindowSpeedMin => 0.6f;
        public float WindowSpeedMax => 1.4f;
        public float WindowScaleMultiplier => 0.75f;    // window drifters are the dust's art scaled by this (the window is small)
        public int MaxWindowParticles => 400;           // hard cap over all windows on screen

        // ---- Particle look (hot reload friendly: all of it is code in these bodies) ----
        public float TintedDustTransparency => 0.65f;   // 0 = solid, 1 = invisible: the dust every non-water liquid sprays (open end, leaks, windows)
        public float WaterDustTransparency => 0f;       // EXTRA see-through on top of the water dust's own (water and biome waters); 0 = exactly vanilla

        // The colour of each liquid's particles, by the liquid's registered name. Edit, add or remove lines and hot reload. A liquid not listed
        // (default) uses its RenderColor. Only the RGB counts here: the see-through is TintedDustTransparency.
        public Color DustColorFor(string liquidName) => liquidName switch
        {
            "Lava" => new Color(255, 110, 30),
            "Honey" => new Color(255, 190, 40),
            "Shimmer" => new Color(220, 100, 255),
            "Oil" => new Color(70, 60, 80),
            "Fuel" => new Color(180, 135, 80),
            _ => default
        };

        public float CustomDustTransparency => 0.1f;    // 0 = solid, 1 = invisible: the custom liquid dusts below (on top of whatever the art itself has)

        // The custom dust each liquid sprays (the classes in Common/Liquids/PipeDust/PipeLiquidDusts.cs; their art is Common/Liquids/PipeDust/<ClassName>.png).
        // -1 = none: that liquid sprays the tintable dust in DustColorFor's colour instead (so a new liquid works before it has art).
        public int PipeDustTypeFor(string liquidName) => liquidName switch
        {
            "Lava" => ModContent.DustType<LavaPipeDust>(),
            "Honey" => ModContent.DustType<HoneyPipeDust>(),
            "Shimmer" => ModContent.DustType<ShimmerPipeDust>(),
            "Oil" => ModContent.DustType<OilPipeDust>(),
            "Fuel" => ModContent.DustType<FuelPipeDust>(),
            _ => -1
        };

        // Window rectangles in tile pixels (the 16x16 cell, 0,0 = top-left). Window particles appear inside these; match them to the hole in the art.
        public Rectangle WindowHorizontal => new Rectangle(3, 5, 10, 6);
        public Rectangle WindowVertical => new Rectangle(5, 3, 6, 10);

        readonly Dictionary<Point, PipeFlowCell> cells = new Dictionary<Point, PipeFlowCell>();
        readonly Queue<(Point pos, int dist, Point step)> queue = new Queue<(Point, int, Point)>();
        readonly HashSet<Point> walked = new HashSet<Point>();
        readonly List<Point> removeBuffer = new List<Point>();
        readonly List<KeyValuePair<Point, PipeFlowCell>> emitBuffer = new List<KeyValuePair<Point, PipeFlowCell>>();
        // Pump chains (see AssignMotorPhases)
        readonly List<MotorTileEntityBase> runningMotors = new List<MotorTileEntityBase>();
        readonly Dictionary<Point, MotorTileEntityBase> runningMotorAt = new Dictionary<Point, MotorTileEntityBase>();
        readonly Dictionary<MotorTileEntityBase, List<(MotorTileEntityBase to, int dist)>> relays = new Dictionary<MotorTileEntityBase, List<(MotorTileEntityBase to, int dist)>>();
        readonly HashSet<MotorTileEntityBase> fedPumps = new HashSet<MotorTileEntityBase>();
        readonly Dictionary<MotorTileEntityBase, float> motorPhase = new Dictionary<MotorTileEntityBase, float>();
        readonly Queue<MotorTileEntityBase> phaseQueue = new Queue<MotorTileEntityBase>();
        readonly Dictionary<MotorTileEntityBase, MotorTileEntityBase> motorRoot = new Dictionary<MotorTileEntityBase, MotorTileEntityBase>();   // pump -> first pump of its chain
        readonly Dictionary<MotorTileEntityBase, PumpChain> chainByRoot = new Dictionary<MotorTileEntityBase, PumpChain>();   // lives across rebuilds: the ball keeps its place
        readonly Dictionary<MotorTileEntityBase, PumpChain> chainOf = new Dictionary<MotorTileEntityBase, PumpChain>();
        readonly Dictionary<MotorTileEntityBase, byte> lastChunkCount = new Dictionary<MotorTileEntityBase, byte>();
        readonly List<PumpChain> activeChains = new List<PumpChain>();
        readonly List<MotorTileEntityBase> staleRoots = new List<MotorTileEntityBase>();
        PumpChain activeChain;   // the chain the walk in progress belongs to
        int waveStamp = 1;       // counts ticks: BallU is worked out once per tile per tick

        // One liquid drifter inside the window of a pipe. Client only, not saved. Drawn under the pipe (DrawWindowParticles).
        struct WindowParticle
        {
            public Point Tile;          // the pipe it lives in
            public Vector2 Position;    // world pixels, the centre of the sprite
            public Vector2 Velocity;    // pixels per tick
            public Texture2D Texture;   // the dust atlas (or a ModDust's own texture) and the frame the game picked for that dust
            public Rectangle Frame;
            public Color Tint;          // the dust's own alpha colour; multiplied by the tile light when drawn
            public float Scale;
            public int Age;
            public int Life;            // ticks until it has drifted out of the window
        }

        readonly List<WindowParticle> windowParticles = new List<WindowParticle>();
        readonly List<Point> pipeBuffer = new List<Point>();
        int stamp;
        int tickCounter;

        public override void Unload()
        {
            cells.Clear();
            queue.Clear();
            walked.Clear();
            emitBuffer.Clear();
            ClearPumpChains();
            windowParticles.Clear();
            pipeBuffer.Clear();
        }

        public override void OnWorldUnload()
        {
            cells.Clear();
            windowParticles.Clear();
            ClearPumpChains();
        }

        void ClearPumpChains()
        {
            runningMotors.Clear();
            runningMotorAt.Clear();
            relays.Clear();
            fedPumps.Clear();
            motorPhase.Clear();
            phaseQueue.Clear();
            motorRoot.Clear();
            chainByRoot.Clear();
            chainOf.Clear();
            lastChunkCount.Clear();
            activeChains.Clear();
        }

        /// <summary>The scale a tile is drawn at right now: Vector2.One when no liquid moves through it. Pipes swell across their axis only (corners and junctions evenly), the pump evenly.</summary>
        public Vector2 GetTileScale(int i, int j, Tile tile)
        {
            if (cells.Count == 0 || !cells.TryGetValue(new Point(i, j), out PipeFlowCell cell))
                return Vector2.One;

            float s = ScaleAt(BallU(cell), cell.Intensity);
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
                if (windowParticles.Count > 0)
                    windowParticles.Clear();
                return;
            }

            tickCounter++;
            if (tickCounter % Math.Max(1, RebuildIntervalTicks) == 0)
                Rebuild();

            FadeCells();
            AdvanceChains();
            UpdateWindowParticles();
            SpawnParticles();
        }

        void Rebuild()
        {
            stamp++;
            Vector2 playerCenter = Main.LocalPlayer.Center;
            float rangeSq = MaxRangeTiles * 16f;
            rangeSq *= rangeSq;

            runningMotors.Clear();
            runningMotorAt.Clear();
            foreach (TileEntity entity in TileEntity.ByID.Values)
            {
                if (!(entity is MotorTileEntityBase motor) || !motor.isOn || motor.FlowLiquidType < 0 || motor.FlowLiquidType >= LiquidTypeRegistry.definitions.Count)
                    continue;

                Vector2 motorCenter = new Vector2(motor.Position.X * 16f + 8f, motor.Position.Y * 16f + 8f);
                if (Vector2.DistanceSquared(motorCenter, playerCenter) > rangeSq)
                    continue;

                runningMotors.Add(motor);
                runningMotorAt[new Point(motor.Position.X, motor.Position.Y)] = motor;
            }

            if (runningMotors.Count == 0)
            {
                activeChains.Clear();
                chainByRoot.Clear();
                chainOf.Clear();
                return;
            }

            AssignMotorPhases();
            PrepareChains();

            for (int m = 0; m < runningMotors.Count; m++)
            {
                Walk(runningMotors[m], true, false);
                Walk(runningMotors[m], false, false);
            }

            FinishChains();
        }

        // One PumpChain per first pump (see AssignMotorPhases); the walk below collects the first and the last S of each.
        void PrepareChains()
        {
            activeChains.Clear();
            chainOf.Clear();

            for (int m = 0; m < runningMotors.Count; m++)
            {
                MotorTileEntityBase motor = runningMotors[m];
                MotorTileEntityBase root = motorRoot.TryGetValue(motor, out MotorTileEntityBase first) ? first : motor;
                if (!chainByRoot.TryGetValue(root, out PumpChain chain))
                {
                    chain = new PumpChain();
                    chainByRoot[root] = chain;
                }

                if (chain.Stamp != stamp)
                {
                    chain.Stamp = stamp;
                    chain.NextMin = float.MaxValue;
                    chain.NextMax = float.MinValue;
                    chain.Rate = 0f;
                    activeChains.Add(chain);
                }

                chainOf[motor] = chain;
                chain.Rate = Math.Max(chain.Rate, motor.FlowRateLevel * motor.FlowRateStep);
            }

            staleRoots.Clear();
            foreach (KeyValuePair<MotorTileEntityBase, PumpChain> pair in chainByRoot)
            {
                if (pair.Value.Stamp != stamp)
                    staleRoots.Add(pair.Key);
            }
            for (int k = 0; k < staleRoots.Count; k++)
                chainByRoot.Remove(staleRoots[k]);
        }

        // The pump's rate wobbles by a level or two (it is smoothed and rounded), and every wobble made balls of slightly different speed and
        // period, so the gaps between them drifted. The balls follow the rate only when it really changed, then snap to it.
        float HoldRate(float held, float rate)
        {
            if (rate <= 0f)
                return 0f;
            if (held <= 0f)
                return rate;

            return MathF.Abs(rate - held) > Math.Max(0f, RateHoldFraction) * held ? rate : held;
        }

        void FinishChains()
        {
            for (int c = 0; c < activeChains.Count; c++)
            {
                PumpChain chain = activeChains[c];
                if (chain.NextMax < chain.NextMin)
                    continue;

                chain.Min = chain.NextMin;
                chain.Max = Math.Max(chain.NextMax, chain.NextMin + 1f);
                chain.StableRate = HoldRate(chain.StableRate, chain.Rate);

                if (!chain.Primed && chain.StableRate > 0f)
                    PrimeChain(chain);
            }
        }

        // Tiles between two balls. One ball per chunk, so it is how far a ball goes at BallSpeedTilesPerSecond in the time the chain needs to move
        // ChunkUnits at its rate (units per tick, 60 ticks a second). Closer than BallMinSpacingTiles is not allowed: the gap stays there and the
        // balls travel faster than BallSpeedTilesPerSecond (the gap per chunk time), which keeps one ball per chunk.
        float ComputeSpacing(float rate)
        {
            float min = Math.Max(0.5f, BallMinSpacingTiles);
            if (rate <= 0f)
                return 10000f;

            float secondsPerChunk = Math.Max(1f, ChunkUnits) / (rate * 60f);
            return Math.Clamp(Math.Max(0.1f, BallSpeedTilesPerSecond) * secondsPerChunk, min, 10000f);
        }

        // The balls a chain has the first moment it is seen: the ones that would be in the pipes if it had been running for a while at this rate,
        // lined up so one is at the end pipe. They are made once; after that balls only come from the source.
        void PrimeChain(PumpChain chain)
        {
            chain.Primed = true;
            chain.Balls.Clear();

            float spacing = ComputeSpacing(chain.StableRate);
            float start = chain.Min - Math.Max(0.5f, BallLengthTiles) * 0.5f;
            float phase = ChunkPhase(chain);
            chain.EmitProgress = phase;

            float speed = spacing * chain.StableRate / Math.Max(1f, ChunkUnits);
            float end = chain.Max + Math.Max(0.5f, BallLengthTiles) * 0.5f;
            for (float pos = start + spacing * phase; pos <= end && chain.Balls.Count < 1024; pos += spacing)
                chain.Balls.Add(new FlowBall { Pos = pos, Speed = speed });
        }

        // Where the making of balls should be (fraction of a period since the last one) at the moment a chunk is delivered, so that a ball made
        // earlier arrives at the end pipe at that moment: the travel time over the period, without the whole periods.
        float ChunkPhase(PumpChain chain)
        {
            float spacing = ComputeSpacing(chain.StableRate);
            float start = chain.Min - Math.Max(0.5f, BallLengthTiles) * 0.5f;
            float periods = (chain.Max - start) / spacing;
            return periods - MathF.Floor(periods);
        }

        // A new ball at the source, with the speed this rate gives: one spacing in the time the chain needs to move one chunk. The ball was made
        // elapsedTicks (0..1 of a tick) ago, somewhere inside this tick, so it has already travelled that long: this keeps the gaps between
        // balls equal (made on whole ticks, the gap would jump between two whole numbers of ticks, which shows when the pump is fast).
        void SpawnBall(PumpChain chain, float elapsedTicks)
        {
            if (chain.StableRate <= 0f || chain.Balls.Count >= 1024)
                return;

            float spacing = ComputeSpacing(chain.StableRate);
            float speed = spacing * chain.StableRate / Math.Max(1f, ChunkUnits);
            chain.Balls.Add(new FlowBall
            {
                Pos = chain.Min - Math.Max(0.5f, BallLengthTiles) * 0.5f + speed * Math.Clamp(elapsedTicks, 0f, 2f),
                Speed = speed
            });
        }

        // Every tick: balls move at their own speed (never backwards) and leave after the end pipe; a new ball is made every ChunkUnits of liquid
        // the chain moves. A real chunk only shifts the making of the NEXT balls, never a ball that is already there.
        void AdvanceChains()
        {
            waveStamp++;
            if (activeChains.Count == 0 || Main.gamePaused)
                return;

            for (int m = 0; m < runningMotors.Count; m++)
            {
                MotorTileEntityBase motor = runningMotors[m];
                if (!chainOf.TryGetValue(motor, out PumpChain chain))
                    continue;

                byte count = motor.FlowChunkCount;
                if (!lastChunkCount.TryGetValue(motor, out byte last))
                {
                    lastChunkCount[motor] = count;
                    continue;
                }
                if (count == last)
                    continue;

                lastChunkCount[motor] = count;
                if (!chain.Primed || chain.StableRate <= 0f)
                    continue;

                float target = ChunkPhase(chain);
                if (!chain.Synced)
                {
                    chain.EmitProgress = target;
                    chain.Synced = true;
                }
                else
                {
                    float error = chain.EmitProgress - target;
                    error -= MathF.Round(error);
                    float deadband = chain.StableRate / Math.Max(1f, ChunkUnits) * Math.Max(0f, SyncDeadbandTicks);
                    if (MathF.Abs(error) > deadband)
                    {
                        float step = (MathF.Abs(error) - deadband) * Math.Clamp(SyncGain, 0f, 1f);
                        chain.EmitProgress -= MathF.Sign(error) * Math.Min(step, Math.Max(0f, SyncMaxStep));
                    }
                }
            }

            float halfLength = Math.Max(0.5f, BallLengthTiles) * 0.5f;
            for (int c = 0; c < activeChains.Count; c++)
            {
                PumpChain chain = activeChains[c];
                if (!chain.Primed)
                    continue;

                for (int b = chain.Balls.Count - 1; b >= 0; b--)
                {
                    FlowBall ball = chain.Balls[b];
                    ball.Pos += ball.Speed;
                    if (ball.Pos - halfLength > chain.Max)
                        chain.Balls.RemoveAt(b);
                }

                if (chain.StableRate <= 0f)
                    continue;

                float perTick = chain.StableRate / Math.Max(1f, ChunkUnits);
                chain.EmitProgress += perTick;
                for (int guard = 0; guard < 4 && chain.EmitProgress >= 1f; guard++)
                {
                    chain.EmitProgress -= 1f;
                    SpawnBall(chain, chain.EmitProgress / perTick);   // the part of this tick that passed since the ball was made
                }
                chain.EmitProgress = Math.Clamp(chain.EmitProgress, -1f, 1f);
            }
        }

        // Gives every running pump its own S. A pump whose back receives the liquid of another running pump (found by a downstream walk that
        // reaches its back) continues that pump's S: its S is the other pump's S plus the tiles between them, so the swelling ball travels
        // through the chain one pump after another. The first pump of a chain starts at a phase taken from its position instead, so
        // unrelated pumps (separate networks, or pumps that do not feed each other) do not all pulse at the same moment.
        void AssignMotorPhases()
        {
            relays.Clear();
            fedPumps.Clear();
            motorPhase.Clear();
            phaseQueue.Clear();
            motorRoot.Clear();

            for (int m = 0; m < runningMotors.Count; m++)
                Walk(runningMotors[m], true, true);

            for (int m = 0; m < runningMotors.Count; m++)
            {
                MotorTileEntityBase motor = runningMotors[m];
                if (fedPumps.Contains(motor))
                    continue;

                motorPhase[motor] = RootPhase(motor);
                motorRoot[motor] = motor;
                phaseQueue.Enqueue(motor);
            }
            PropagatePhases();

            // Pumps that feed each other in a loop have no first pump: the first of them starts the chain.
            for (int m = 0; m < runningMotors.Count; m++)
            {
                MotorTileEntityBase motor = runningMotors[m];
                if (motorPhase.ContainsKey(motor))
                    continue;

                motorPhase[motor] = RootPhase(motor);
                motorRoot[motor] = motor;
                phaseQueue.Enqueue(motor);
                PropagatePhases();
            }
        }

        void PropagatePhases()
        {
            while (phaseQueue.Count > 0)
            {
                MotorTileEntityBase from = phaseQueue.Dequeue();
                if (!relays.TryGetValue(from, out List<(MotorTileEntityBase to, int dist)> fed))
                    continue;

                float phase = motorPhase[from];
                for (int k = 0; k < fed.Count; k++)
                {
                    if (motorPhase.ContainsKey(fed[k].to))
                        continue;

                    motorPhase[fed[k].to] = phase + fed[k].dist;
                    motorRoot[fed[k].to] = motorRoot.TryGetValue(from, out MotorTileEntityBase firstOfChain) ? firstOfChain : from;
                    phaseQueue.Enqueue(fed[k].to);
                }
            }
        }

        // Every chain has its own balls (PumpChain.Balls), so unrelated pumps already differ; a chain's first pump starts at S 0.
        float RootPhase(MotorTileEntityBase motor) => 0f;

        void AddRelay(MotorTileEntityBase from, MotorTileEntityBase to, int dist)
        {
            if (!relays.TryGetValue(from, out List<(MotorTileEntityBase to, int dist)> fed))
            {
                fed = new List<(MotorTileEntityBase to, int dist)>();
                relays[from] = fed;
            }
            fed.Add((to, dist));
            fedPumps.Add(to);
        }

        // Breadth-first from the pump's front (downstream) or back (upstream) through connected pipes of the same material.
        // discoverOnly (downstream only): records which other running pumps this one feeds, and records no cells.
        void Walk(MotorTileEntityBase motor, bool downstream, bool discoverOnly)
        {
            Direction side = downstream ? motor.Facing : motor.Facing.Opposite();
            Point step = side.ToOffset();
            Point motorPos = new Point(motor.Position.X, motor.Position.Y);
            int motorType = Main.tile[motorPos.X, motorPos.Y].TileType;
            float rate = motor.FlowRateLevel * motor.FlowRateStep;
            float phase = !discoverOnly && motorPhase.TryGetValue(motor, out float p) ? p : 0f;
            activeChain = !discoverOnly && chainOf.TryGetValue(motor, out PumpChain chain) ? chain : null;

            // The pump itself is the first cell of its wave, so it swells with its pipes.
            if (downstream && !discoverOnly)
                Record(motorPos, phase, 0f, motor.Facing.ToOffset(), motor.FlowLiquidType, rate, Main.tile[motorPos.X, motorPos.Y], true);

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
                if (!tile.HasTile)
                    continue;

                // Another running pump stops the walk (it has its own). If the liquid arrives at its back, this pump feeds it.
                if (runningMotorAt.TryGetValue(pos, out MotorTileEntityBase next))
                {
                    if (discoverOnly && downstream && next != motor && stepHere == next.Facing.ToOffset() && PipeTierRegistry.CanLink(motorType, tile.TileType))
                        AddRelay(motor, next, dist);
                    continue;
                }

                // A pump that is switched OFF is a plain pipe (the network treats it so): the ball goes through it, and it swells like the pumps do.
                bool switchedOffPump = PipeTierRegistry.IsMotorTile(tile.TileType) && IsSwitchedOffPump(pos);
                if (!PipeTierRegistry.CanLink(motorType, tile.TileType) || (!switchedOffPump && !PipeTierRegistry.IsPipeTile(tile.TileType)))
                    continue;

                budget--;
                if (!discoverOnly)
                    Record(pos, downstream ? phase + dist : phase - dist, dist, downstream ? stepHere : new Point(-stepHere.X, -stepHere.Y), motor.FlowLiquidType, rate, tile, switchedOffPump);

                for (int d = 0; d < 4; d++)
                {
                    Point offset = ((Direction)d).ToOffset();
                    queue.Enqueue((pos + offset, dist + 1, offset));
                }
            }
        }

        static bool IsSwitchedOffPump(Point pos)
        {
            return TileEntity.ByPosition.TryGetValue(new Point16(pos.X, pos.Y), out TileEntity entity) && entity is MotorTileEntityBase pump && !pump.isOn;
        }

        void Record(Point pos, float s, float dist, Point travel, int liquid, float rate, Tile tile, bool isMotor)
        {
            if (!cells.TryGetValue(pos, out PipeFlowCell cell))
            {
                cell = new PipeFlowCell();
                cells[pos] = cell;
            }
            else if (cell.Stamp == stamp && cell.Dist <= dist)
            {
                return;   // another pump already reached this pipe from closer by
            }

            cell.S = s;
            cell.Dist = dist;
            cell.Travel = travel;
            cell.Liquid = liquid;
            cell.Rate = rate;
            cell.IsMotor = isMotor;
            cell.Stamp = stamp;
            cell.Chain = activeChain;
            if (activeChain != null)
            {
                activeChain.NextMin = Math.Min(activeChain.NextMin, s);
                activeChain.NextMax = Math.Max(activeChain.NextMax, s);
            }
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

        // Where this tile is relative to the nearest ball of its chain: 0..BallLengthTiles while the ball is on it (the ball is centred on the tile
        // at u = BallLengthTiles / 2), negative before it. Where two balls overlap (a faster one passing a slower one) the nearer one counts,
        // which is continuous because the bump is symmetric around the ball. A tile with no chain, or a chain with no ball, is never on a ball.
        // Worked out once per tile per tick (the draw, the overhang art and the particles all ask).
        float BallU(PipeFlowCell cell)
        {
            if (cell.WaveStamp == waveStamp)
                return cell.WaveU;

            float u = BallUAt(cell.Chain, cell.S);

            cell.WaveU = u;
            cell.WaveStamp = waveStamp;
            return u;
        }

        // The same lookup for any path coordinate of a chain (BallU for a real cell; the backdrop pipes behind machines ask with their own S).
        float BallUAt(PumpChain chain, float s)
        {
            float u = -1f;
            if (chain != null && chain.Balls.Count > 0)
            {
                float nearest = chain.Balls[0].Pos;
                float best = Math.Abs(s - nearest);
                for (int b = 1; b < chain.Balls.Count; b++)
                {
                    float distance = Math.Abs(s - chain.Balls[b].Pos);
                    if (distance < best)
                    {
                        best = distance;
                        nearest = chain.Balls[b].Pos;
                    }
                }
                u = s - nearest + Math.Max(0.5f, BallLengthTiles) * 0.5f;
            }

            return u;
        }

        /// <summary>The swell scale for a decorative pipe tile that carries on a real pipe's wave (PipeMachineBackdropSystem). pipe = the real pipe touching the machine, towardMachine = the step from it into the machine, stepsIn = how many tiles further this tile is along the backdrop path, mask = this tile's pipe frame (swells across its axis like GetTileScale). False when no liquid moves through that pipe (the tile is drawn at scale 1).</summary>
        public bool TryGetBackdropScale(Point pipe, Point towardMachine, int stepsIn, int mask, out Vector2 scale)
        {
            scale = Vector2.One;
            if (cells.Count == 0 || !cells.TryGetValue(pipe, out PipeFlowCell cell))
                return false;

            // The liquid runs along cell.Travel: into the machine (+1 per tile), out of it (-1 per tile), or sideways (the wave stands still).
            int direction = towardMachine.X * cell.Travel.X + towardMachine.Y * cell.Travel.Y;
            float s = ScaleAt(BallUAt(cell.Chain, cell.S + direction * stepsIn), cell.Intensity);

            if (mask == 12)
                scale = new Vector2(1f, s);
            else if (mask == 3)
                scale = new Vector2(s, 1f);
            else
                scale = new Vector2(s);

            return true;
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

            for (int n = 0; n < emitBuffer.Count; n++)
            {
                KeyValuePair<Point, PipeFlowCell> pair = emitBuffer[(startAt + n) % emitBuffer.Count];
                PipeFlowCell cell = pair.Value;
                Point p = pair.Key;

                Tile tile = Main.tile[p.X, p.Y];
                if (!tile.HasTile)
                    continue;

                // The window drifters run the whole time the liquid moves (they are not tied to the swell) and have their own list and cap.
                EmitWindow(p, cell, tile);

                if (budget <= 0)
                    continue;

                // The open end and the leaks are strongest while the swell passes through this pipe.
                float swell = Lobe(BallU(cell)) * cell.Intensity;
                float peak = swell * cell.Rate;
                if (peak <= 0.001f)
                    continue;

                budget -= EmitOpenEnd(p, cell, peak, budget);
                budget -= EmitLeak(p, cell, swell, budget);
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

        // Windowed straight pipes only (TileFrameY 18 = the windowed art): liquid drifting along the pipe inside the window, the whole time it
        // moves (not tied to the swell). These are not dust: dust is drawn over the tiles, these are drawn under the pipe (DrawWindowParticles),
        // so the pipe's frame covers them and they read as inside it. They still use the liquid's own registered dust art (SplashDustFor).
        void EmitWindow(Point p, PipeFlowCell cell, Tile tile)
        {
            if (tile.TileFrameY < 18 || windowParticles.Count >= MaxWindowParticles)
                return;

            int mask = tile.TileFrameX / 18;
            bool horizontal = mask == 12;
            if (!horizontal && mask != 3)
                return;

            int sign = horizontal ? cell.Travel.X : cell.Travel.Y;
            if (sign == 0)
                return;

            int count = Roll(cell.Rate * cell.Intensity * WindowParticlesPerUnit, MaxWindowParticles - windowParticles.Count);
            if (count <= 0)
                return;

            Rectangle local = horizontal ? WindowHorizontal : WindowVertical;
            Rectangle window = new Rectangle(p.X * 16 + local.X, p.Y * 16 + local.Y, local.Width, local.Height);

            for (int k = 0; k < count; k++)
            {
                // Starts in the upstream half of the window and drifts out through the downstream end.
                float along = Main.rand.NextFloat(-0.35f, 0.1f);
                float across = Main.rand.NextFloat();
                float speed = Math.Max(0.05f, Main.rand.NextFloat(WindowSpeedMin, Math.Max(WindowSpeedMin, WindowSpeedMax)));

                Vector2 position;
                Vector2 velocity;
                float travelLength;
                if (horizontal)
                {
                    float x = sign > 0 ? window.X + along * window.Width : window.Right - along * window.Width;
                    position = new Vector2(x, window.Y + across * window.Height);
                    velocity = new Vector2(speed * sign, Main.rand.NextFloat(-0.05f, 0.05f));
                    travelLength = sign > 0 ? window.Right - x : x - window.X;
                }
                else
                {
                    float y = sign > 0 ? window.Y + along * window.Height : window.Bottom - along * window.Height;
                    position = new Vector2(window.X + across * window.Width, y);
                    velocity = new Vector2(Main.rand.NextFloat(-0.05f, 0.05f), speed * sign);
                    travelLength = sign > 0 ? window.Bottom - y : y - window.Y;
                }

                AddWindowParticle(p, position, velocity, Math.Max(6, (int)MathF.Ceiling(travelLength / speed)), cell.Liquid);
            }
        }

        void AddWindowParticle(Point tile, Vector2 position, Vector2 velocity, int life, int liquid)
        {
            int dustType = SplashDustFor(liquid, out Color tint);

            // Dust.NewDustPerfect sets up the frame, alpha and scale exactly as the game does for that dust. Read them, then let the dust go.
            float scale = Main.rand.NextFloat(ParticleScaleMin, Math.Max(ParticleScaleMin, ParticleScaleMax)) * WindowScaleMultiplier;
            Dust proxy = Dust.NewDustPerfect(position, dustType, Vector2.Zero, 0, tint, scale);

            Texture2D texture = TextureAssets.Dust.Value;
            ModDust modDust = DustLoader.GetDust(proxy.type);
            if (modDust != null)
                texture = ModContent.Request<Texture2D>(modDust.Texture).Value;

            windowParticles.Add(new WindowParticle
            {
                Tile = tile,
                Position = position,
                Velocity = velocity,
                Texture = texture,
                Frame = proxy.frame,
                Tint = WindowTint(proxy, tint),
                Scale = proxy.scale,
                Life = life
            });

            proxy.active = false;
        }

        // The colour a window drifter is drawn in (before the tile light). Tinted dust: its art is white and the game's own tinting is not
        // there for a hand-drawn particle, so the tint itself is the colour. Water dust: its own draw colour. Both fade by their transparency slider.
        Color WindowTint(Dust proxy, Color tint)
        {
            float fade = 1f - TransparencyFor(proxy.type, tint);
            if (tint != default)
                return new Color(tint.ToVector4() * fade);

            Color own = proxy.GetAlpha(Color.White);
            return new Color(own.ToVector4() * fade);
        }

        // Which slider a dust follows: the tinted fallback dust, the custom liquid dusts (PipeLiquidDust), or the water dust.
        float TransparencyFor(int dustType, Color tint)
        {
            float t = tint != default ? TintedDustTransparency
                : DustLoader.GetDust(dustType) is PipeLiquidDust ? CustomDustTransparency
                : WaterDustTransparency;
            return Math.Clamp(t, 0f, 1f);
        }

        // Real dust: more see-through by adding to the dust's alpha (255 = invisible).
        void ApplyTransparency(Dust dust, Color tint)
        {
            float t = tint != default ? Main.rand.NextFloat(TintedDustTransparency, 1f) : TransparencyFor(dust.type, tint);
            dust.alpha = Math.Clamp(dust.alpha + (int)(255f * Math.Clamp(t, 0f, 1f)), 0, 255);
        }

        void UpdateWindowParticles()
        {
            if (windowParticles.Count == 0 || Main.gamePaused)
                return;

            for (int n = windowParticles.Count - 1; n >= 0; n--)
            {
                WindowParticle w = windowParticles[n];
                w.Age++;
                w.Position += w.Velocity;

                Tile tile = Main.tile[w.Tile.X, w.Tile.Y];
                if (w.Age >= w.Life || !tile.HasTile || !PipeTierRegistry.IsPipeTile(tile.TileType))
                {
                    windowParticles.RemoveAt(n);
                    continue;
                }

                windowParticles[n] = w;
            }
        }

        // Drawn before the pipes, in the same batch, so the pipe art covers them: the liquid shows through the window and nowhere else.
        void DrawWindowParticles(SpriteBatch spriteBatch)
        {
            for (int n = 0; n < windowParticles.Count; n++)
            {
                WindowParticle w = windowParticles[n];
                float fade = Math.Min(1f, w.Age / 4f) * Math.Min(1f, (w.Life - w.Age) / 4f);
                if (fade <= 0f)
                    continue;

                Color light = Lighting.GetColor(w.Tile.X, w.Tile.Y);
                Color color = new Color(light.ToVector4() * w.Tint.ToVector4() * fade);
                spriteBatch.Draw(w.Texture, w.Position - Main.screenPosition, w.Frame, color, 0f, new Vector2(4f, 4f), w.Scale, SpriteEffects.None, 0f);
            }
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

        // A pipe with air next to it leaks only now and then: a small chance per tick (strongest while the swell passes), a short burst when it hits,
        // out of one of the open sides.
        int EmitLeak(Point p, PipeFlowCell cell, float swell, int budget)
        {
            if (Main.rand.NextFloat() >= LeakChancePerTick * swell)
                return 0;

            int burst = Main.rand.Next(Math.Max(1, LeakBurstMin), Math.Max(LeakBurstMin, LeakBurstMax) + 1);
            int count = Math.Min(burst, Math.Max(0, budget));

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
            int dustType = SplashDustFor(liquid, out Color tint);
            Dust dust = Dust.NewDustPerfect(position, dustType, velocity, 0, tint, Main.rand.NextFloat(ParticleScaleMin, Math.Max(ParticleScaleMin, ParticleScaleMax)));
            dust.noGravity = !gravity;
            ApplyTransparency(dust, tint);
        }

        // The dust a liquid sprays. Water liquids keep their biome dust (plain water follows the player's biome, below).
        // Every other liquid (Lava, Honey, Shimmer, Oil, Fuel, later ones) sprays the tintable dust in its registered RenderColor. Not their own
        // vanilla dust: Lava/Honey dust (and the bubble dust) are made to live only inside liquid, so they died the tick they left a pipe.
        // Not the water dust either: it draws in its own blue and ignores the dust colour, so every liquid came out dark blue.
        int SplashDustFor(int liquid, out Color tint)
        {
            tint = default;

            // Plain water (what a pump takes from a pond or the sea) is the water of the biome the player is in, exactly as the world water and
            // vanilla's own splashes pick it (Dust.dustWater follows Main.waterStyle). A biome water liquid (a machine output) keeps its own.
            if (liquid == LiquidTypeRegistry.Water)
                return Dust.dustWater();

            if (LiquidTypeRegistry.IsWater(liquid))
            {
                if (liquid >= 0 && liquid < LiquidTypeRegistry.definitions.Count)
                {
                    int registered = LiquidTypeRegistry.Get(liquid).SplashDust;
                    if (registered >= 0)
                        return registered;
                }

                return DustID.Water;
            }

            if (liquid >= 0 && liquid < LiquidTypeRegistry.definitions.Count)
            {
                int custom = PipeDustTypeFor(LiquidTypeRegistry.Get(liquid).Name);
                if (custom >= 0)
                    return custom;
            }

            tint = LiquidColor(liquid);
            return DustID.TintableDust;
        }

        Color LiquidColor(int liquid)
        {
            if (liquid < 0 || liquid >= LiquidTypeRegistry.definitions.Count)
                return Color.White;

            Color c = DustColorFor(LiquidTypeRegistry.Get(liquid).Name);
            if (c == default)
                c = LiquidTypeRegistry.Get(liquid).RenderColor;

            return new Color(c.R, c.G, c.B, (byte)255);
        }

        // ------------------------------------------------------------------ drawing

        // Draws, after all tiles and in this order: the liquid drifting in the windows, every pump on screen, then every pipe on screen
        // (PipeTileBase.PreDraw and the pump's PreDraw skip the normal draw), so a swollen pipe covers its neighbours instead of being painted
        // over by them, the window liquid sits under the pipe art, and pumps and pipes share one point-sampled batch (a swollen pump drawn in the
        // tile pass came out blurry).
        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            VisibleTileScan.GetRange(2, out int left, out int right, out int top, out int bottom);
            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            DrawWindowParticles(spriteBatch);

            pipeBuffer.Clear();
            for (int i = left; i <= right; i++)
            {
                for (int j = top; j <= bottom; j++)
                {
                    Tile tile = Main.tile[i, j];
                    if (!tile.HasTile)
                        continue;

                    if (PipeTierRegistry.IsPipeTile(tile.TileType))
                        pipeBuffer.Add(new Point(i, j));
                    else if (PipeTierRegistry.IsMotorTile(tile.TileType))
                        MachineVisualOverride.DrawDeferredMotor(spriteBatch, i, j);
                }
            }

            for (int n = 0; n < pipeBuffer.Count; n++)
                DrawPipe(spriteBatch, pipeBuffer[n].X, pipeBuffer[n].Y, Main.tile[pipeBuffer[n].X, pipeBuffer[n].Y]);

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
