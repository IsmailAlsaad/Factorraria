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
    // One chain of pumps (a lone pump, or pumps that feed each other's backs) and the single ball of fluid travelling through all its pipes.
    public sealed class PumpChain
    {
        public float Progress;       // 0..1: how far every ball is on its way to the place of the next one (one spacing = one chunk moved)
        public float Rate;           // liquid units per tick the chain moves (smoothed)
        public float Min, Max;       // path coordinates S of the first and the last tile of the chain
        public float NextMin, NextMax;   // being collected by the walk in progress
        public int Balls = 1;        // how many balls travel along this chain at once (BallsPerSpan per BallSpanTiles of its length)
        public float Spacing = 1f;   // tiles between two balls: the chain's length / Balls
        public bool Synced;          // lined up with a real chunk at least once
        public int Stamp;
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
        public PumpChain Chain;  // the ball that passes through this cell
    }

    // CLIENT ONLY and purely cosmetic: nothing here is saved or sent. It reads what every client already has: the motor replicas
    // (isOn, Facing, FlowLiquidType and FlowRateLevel, which the server writes and sends in the motor's light update) and the pipe tiles.
    //
    // Every few ticks it walks the pipes from each running pump: downstream along its Facing (ON pumps are one-way) and upstream
    // behind it, giving every pipe a path coordinate S. A pump that is switched OFF is a plain pipe, so the walk goes through it. Pumps in a
    // chain (one feeding the back of the next) continue each other's S, and share one ball.
    //
    // The balls: a chain has BallsPerSpan balls for every BallSpanTiles of its length (first intake tile to last outlet tile), evenly spaced,
    // so a long network has many and a short one few (at least one). The pattern moves one spacing in exactly the time the pump needs to move a
    // full world cell (ChunkUnits, 255) at the current rate: more balls = slower balls, and a faster pump = faster, more frequent balls. The
    // position is the integral of the rate, so a rate change never makes it jump. Exactly one ball reaches the end pipe per chunk, and when the
    // network delivers a real chunk (MotorTileEntityBase.FlowChunkCount changes: a world cell received liquid, or 255 units went into a machine)
    // the pattern is lined up so that ball is at the end pipe at that moment. The count travels in the motor's light update, so every client
    // sees the same moments.
    //
    // Drawing: EVERY pipe is drawn here (PostDrawTiles; PipeTileBase.PreDraw always says no), scaled by the wave while liquid moves and at
    // scale 1 otherwise, so a pipe never switches between two ways of being drawn (that switch was the flicker). The art outside the
    // tile (TileOverdrawSystem) and the pump (MachineVisualOverride) ask GetTileScale so they swell in step.
    //
    // Liquid itself uses the liquid's registered dust (LiquidTypeDefinition.SplashDust: the biome dust for biome waters, the ModLiquid's own
    // splash dust for the custom liquids): a stream out of an open pipe end and now and then a leak from a pipe that touches air, both real dust,
    // plus the liquid drifting inside the window of a windowed pipe. The window particles are not dust: they are drawn under the pipe (see
    // DrawWindowParticles) so the pipe's frame covers them and they look like they are inside it.
    public class PipeFlowVisualSystem : ModSystem
    {
        // ---- Tuning (instance fields on purpose: hot reload can change them) ----
        public float ChunkUnits = 255f;           // liquid units in a full world cell: one ball per this much liquid moved
        public float BallsPerSpan = 1f;           // x: this many balls...
        public float BallSpanTiles = 9f;          // y: ...for every this many tiles of pipe network (the chain's length from first intake to last outlet). 2 and 9 = two balls per 9 tiles
        public float SyncGain = 0.5f;             // 0..1: how much of its timing error the ball corrects at each real chunk (the first one lines it up fully)
        public float BallLengthTiles = 3f;        // the swelling lobe; the squeeze lobe right behind it is as long

        public float SwellAmount = 0.15f;          // extra scale at the middle of the ball
        public float SqueezeAmount = 0f;       // scale lost just behind the ball
        public float MotorSwellMultiplier = 1f;   // the pump swells SwellAmount and squeezes SqueezeAmount times this (0 = the pump stays put)
        public int RebuildIntervalTicks = 10;
        public int MaxWalkPipes = 600;            // per pump and side
        public float MaxRangeTiles = 90f;         // pumps farther than this from the player are not animated
        public float FadePerTick = 0.1f;

        // ---- Particle amounts ----
        // The open end is "particles per tick, per liquid unit moved per tick, while the swell is at its peak" (the swell lobe scales it down toward
        // its edges and to 0 outside it). A lead pump moves up to 5 units per tick, an iron pump up to 10, so with the defaults an open
        // end of a full lead pump makes about 1 particle per tick at the swell's peak. Fractions are rolled (0.3 = one particle in three ticks).
        // The window drifters use the same unit but run the whole time the liquid moves (not tied to the swell).
        public float OpenEndParticlesPerUnit = 0.2f;   // the stream out of the open end the liquid leaves through
        public float LeakChancePerTick = 0.012f;       // a pipe that touches air leaks only now and then: this chance per tick at the swell's peak (scaled down toward its edges)
        public int LeakBurstMin = 1;                   // particles in one leak
        public int LeakBurstMax = 2;
        public float WindowParticlesPerUnit = 0.12f;   // drifting inside the window of a windowed pipe, continuously
        public int MaxParticlesPerTick = 24;           // a hard cap over all pipes on screen, so a huge network cannot flood the dust pool
        public float ParticleScaleMin = 0.8f;
        public float ParticleScaleMax = 1.1f;
        public float OpenEndSpeedMin = 0.8f;           // pixels per tick
        public float OpenEndSpeedMax = 2.2f;
        public float LeakSpeedMin = 0.4f;
        public float LeakSpeedMax = 1.4f;
        public float WindowSpeedMin = 0.6f;
        public float WindowSpeedMax = 1.4f;
        public float WindowScaleMultiplier = 0.75f;    // window drifters are the dust's art scaled by this (the window is small)
        public int MaxWindowParticles = 400;           // hard cap over all windows on screen

        // Window rectangles in tile pixels (the 16x16 cell, 0,0 = top-left). Window particles appear inside these; match them to the hole in the art.
        public Rectangle WindowHorizontal = new Rectangle(3, 5, 10, 6);
        public Rectangle WindowVertical = new Rectangle(5, 3, 6, 10);

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

        void FinishChains()
        {
            for (int c = 0; c < activeChains.Count; c++)
            {
                PumpChain chain = activeChains[c];
                if (chain.NextMax < chain.NextMin)
                    continue;

                chain.Min = chain.NextMin;
                chain.Max = Math.Max(chain.NextMax, chain.NextMin + 1f);

                // At most as many as fit without their swells overlapping.
                float length = chain.Max - chain.Min;
                int fit = Math.Max(1, (int)MathF.Floor(length / Math.Max(0.5f, BallLengthTiles)));
                chain.Balls = Math.Clamp((int)MathF.Round(length * Math.Max(0f, BallsPerSpan) / Math.Max(1f, BallSpanTiles)), 1, fit);
                chain.Spacing = length / chain.Balls;
            }
        }

        // Every tick: the balls of each chain move by the liquid the chain moved (rate / ChunkUnits of one spacing, so one spacing = one chunk), and
        // are lined up when the network reports a real chunk. After a chunk the last ball is at the end pipe and a new one starts at the intake.
        void AdvanceChains()
        {
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
                if (!chain.Synced)
                {
                    chain.Progress = 0f;
                    chain.Synced = true;
                }
                else
                {
                    float error = chain.Progress < 0.5f ? chain.Progress : chain.Progress - 1f;
                    chain.Progress -= error * Math.Clamp(SyncGain, 0f, 1f);
                }
            }

            for (int c = 0; c < activeChains.Count; c++)
            {
                PumpChain chain = activeChains[c];
                chain.Progress += chain.Rate / Math.Max(1f, ChunkUnits);
                chain.Progress -= MathF.Floor(chain.Progress);
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

        // Every chain has its own ball (PumpChain.Progress), so unrelated pumps already differ; a chain's first pump starts at S 0.
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
        // at u = BallLengthTiles / 2), negative before it. The balls sit at Min + Spacing * (k + Progress) for k = 0..Balls-1. A tile with no chain
        // is never on a ball.
        float BallU(PipeFlowCell cell)
        {
            PumpChain chain = cell.Chain;
            if (chain == null)
                return -1f;

            float q = (cell.S - chain.Min) / chain.Spacing - chain.Progress;
            int k = Math.Clamp((int)MathF.Round(q), 0, chain.Balls - 1);
            float center = chain.Min + chain.Spacing * (k + chain.Progress);
            return cell.S - center + Math.Max(0.5f, BallLengthTiles) * 0.5f;
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
                float along = Main.rand.NextFloat() * 0.5f;
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
                Tint = proxy.GetAlpha(Color.White),
                Scale = proxy.scale,
                Life = life
            });

            proxy.active = false;
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
        }

        // The dust a liquid sprays: the one it registered (LiquidTypeDefinition.SplashDust), so biome waters match their biome and custom liquids
        // use their own ModLiquid.SplashDustType; plain water follows the player's biome (below). Only a liquid with nothing registered (Shimmer) falls back to the tintable dust in its colour.
        static int SplashDustFor(int liquid, out Color tint)
        {
            tint = default;

            // Plain water (what a pump takes from a pond or the sea) is the water of the biome the player is in, exactly as the world water and
            // vanilla's own splashes pick it (Dust.dustWater follows Main.waterStyle). A biome water liquid (a machine output) keeps its own.
            if (liquid == LiquidTypeRegistry.Water)
                return Dust.dustWater();

            if (liquid >= 0 && liquid < LiquidTypeRegistry.definitions.Count)
            {
                int registered = LiquidTypeRegistry.Get(liquid).SplashDust;
                if (registered >= 0)
                    return registered;
            }

            if (LiquidTypeRegistry.IsWater(liquid))
                return DustID.Water;

            tint = LiquidColor(liquid);
            return DustID.RainbowMk2;
        }

        static Color LiquidColor(int liquid)
        {
            if (liquid < 0 || liquid >= LiquidTypeRegistry.definitions.Count)
                return Color.White;

            Color c = LiquidTypeRegistry.Get(liquid).RenderColor;
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
