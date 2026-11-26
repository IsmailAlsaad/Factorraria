using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.Liquids
{
    // COSMETIC ONLY, CLIENT ONLY: nothing here is saved, sent or read by the liquid network. A machine's footprint (its TileEntity tiles)
    // can be bigger than what its sprite shows, so a pipe that touches the footprint looks like it floats with a gap before the sprite
    // even though it really is connected. For every pipe touching a machine that has liquid ports, this draws extra pipe tiles BEHIND the
    // machine: the footprint tile right next to the pipe (so the pipe carries on one tile in the direction it came from), then a path
    // through the footprint (4 directions only, straight runs preferred) to the machine's PRIORITY tile (see PickGoal: a manual tile per
    // machine through RegisterPriorityTile, else a rule by footprint size). The machine's own sprite is drawn over them, so they only
    // show in the gap.
    //
    // Multiplayer: no packets, no authority. Every client works it out from the tiles and the machine entity it already has (the pipe
    // tiles, the entity's footprint size) and from PipeFlowVisualSystem's own client-side wave, so single player and multiplayer are the same.
    //
    // Look: the 16x16 tile sprite only (no overdraw art, it left seams) of the pipe that touches the machine, always the non-windowed frame (TileFrameY 0), lit like a
    // normal tile, not darkened. Swell: the wave of the pipe that touches the machine carries on into the backdrop pipes
    // (PipeFlowVisualSystem.TryGetBackdropScale), so the animation flows through them.
    //
    // Drawing: machines drawn as one canvas (MachineOverhangSystem) get DrawMachine right before their art, in the same pass and batch.
    // Every other machine (drawn tile by tile in the tile pass) gets its whole backdrop from DrawBehind, called at the start of the
    // behind-tiles pass: that pass runs before any tile, so no tile's art can ever be covered by a backdrop piece (drawing a piece in
    // each tile's PreDraw put it over the art of tiles drawn earlier).
    //
    // The end of a path (the priority tile) is a cap: it is open only on the sides pipes arrive from, never onward.
    public class PipeMachineBackdropSystem : ModSystem
    {
        // ---- Tuning (expression-bodied properties on purpose: hot reload replaces a body, not a field initializer) ----
        public int TurnPenalty => 2;               // path cost of a corner, on top of 1 per tile: higher = straighter paths
        public int PruneIntervalTicks => 3600;     // cached backdrops of removed machines are dropped this often
        public bool PumpBackdrops => true;         // false = pumps facing a machine start no bg pipes (patch 5 feature, switch for finding a drawing bug)
        public bool ClampTileSwell => false;        // tile-by-tile machines: a bg pipe never grows past its own 16x16 cell (a bigger one is drawn over tiles that were already drawn); false = swell fully

        sealed class BackdropTile
        {
            public int X;
            public int Y;
            public int Mask;          // connection mask like a real pipe's frame: up 1, down 2, left 4, right 8
            public int PipeType;      // tile type of the pipe this tile copies (for a pump: the pipe of the pump's material)
            public bool FromMotor;    // the tile at Pipe is a pump (the texture comes from PipeType, not from the tile there)
            public Point Pipe;        // the real pipe that touches the machine (its wave is carried on)
            public Point Toward;      // the step from that pipe into the machine
            public int Steps;         // tiles between that pipe and this tile along the path (the first footprint tile is 1)
            public Vector2 Scale;     // set every frame
        }

        sealed class Backdrop
        {
            public int Signature;
            public uint Stamp;   // the tick it was last checked against the pipes around the machine
            public readonly List<BackdropTile> Tiles = new List<BackdropTile>();
            public Dictionary<Point, BackdropTile> ByCell = new Dictionary<Point, BackdropTile>();
        }

        struct Attachment
        {
            public Point Pipe;
            public Direction Dir;     // from the pipe into the machine
            public Point Foot;        // the footprint tile next to the pipe
            public int Type;
            public bool Motor;        // Pipe is a pump directly facing the machine (Type is then the pipe of its material)
        }

        readonly Dictionary<Point16, Backdrop> backdrops = new Dictionary<Point16, Backdrop>();
        readonly List<Attachment> attachments = new List<Attachment>();
        int lastPruneTick;

        readonly Dictionary<int, Point> priorityTiles = new Dictionary<int, Point>();

        public override void Unload()
        {
            backdrops.Clear();
            priorityTiles.Clear();
        }

        /// <summary>Manual priority tile for one machine type (its ValidTileType): the backdrop pipes run to this footprint tile instead of the size rule. col/row count from the footprint's top-left tile; a negative number counts from the far side (-1 = last column / bottom row), so (1, -1) is the bottom middle of a 3-wide machine. Call from MachineRegisterationSystem.PostSetupContent.</summary>
        public void RegisterPriorityTile(int tileType, int col, int row)
        {
            priorityTiles[tileType] = new Point(col, row);
        }

        public override void OnWorldUnload()
        {
            backdrops.Clear();
        }

        // The cached backdrop of a machine, checked against the pipes around it once per tick. Null when it has nothing to draw for.
        Backdrop GetBackdrop(BaseMachine machine)
        {
            if (Main.dedServ || Main.gameMenu || machine is MotorTileEntityBase || !machine.HasLiquidPorts || !machine.InitializeGeometry())
                return null;

            uint now = Main.GameUpdateCount;
            if ((int)now - lastPruneTick > PruneIntervalTicks || (int)now < lastPruneTick)
            {
                lastPruneTick = (int)now;
                backdrops.Clear();
            }

            if (backdrops.TryGetValue(machine.Position, out Backdrop backdrop) && backdrop.Stamp == now)
                return backdrop;

            CollectAttachments(machine, out int signature);
            if (backdrop == null || backdrop.Signature != signature)
            {
                backdrop = Build(machine, signature);
                backdrops[machine.Position] = backdrop;
            }

            backdrop.Stamp = now;
            return backdrop;
        }

        /// <summary>The backdrops of every machine drawn tile by tile, for the behind-tiles pass (MachineOverhangSystem.DrawPass(false) calls this first, inside the SpriteBatch vanilla has open there). Canvas machines draw their own through DrawMachine.</summary>
        public void DrawBehind(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (!(entity is BaseMachine machine) || machine is MotorTileEntityBase || !machine.HasLiquidPorts || !machine.InitializeGeometry())
                    continue;

                Tile corner = Framing.GetTileSafely(machine.Position.X, machine.Position.Y);
                if (corner.HasTile && MachineVisualRegistry.Definitions.TryGetValue(corner.TileType, out MachineVisualDefinition def) && def.Canvas != null)
                    continue;

                float margin = Math.Max(machine.MachineWidth, machine.MachineHeight) * 16f + 48f;
                if (!BaseMachine.IsOnScreen(machine.MachineCenter, margin))
                    continue;

                DrawMachine(machine, spriteBatch);
            }
        }

        /// <summary>A machine's whole backdrop, for machines drawn as one canvas (MachineOverhangSystem calls this right before the canvas, in the same pass and batch). Same order as real pipes: the art outside the tile first, then the bodies.</summary>
        public void DrawMachine(BaseMachine machine, SpriteBatch spriteBatch)
        {
            Backdrop backdrop = GetBackdrop(machine);
            if (backdrop == null || backdrop.Tiles.Count == 0)
                return;

            PipeFlowVisualSystem flow = ModContent.GetInstance<PipeFlowVisualSystem>();
            for (int n = 0; n < backdrop.Tiles.Count; n++)
            {
                BackdropTile t = backdrop.Tiles[n];
                PrepareScale(t, flow);
            }

            for (int n = 0; n < backdrop.Tiles.Count; n++)
                DrawBody(backdrop.Tiles[n], spriteBatch, Vector2.Zero);
        }

        /// <summary>One tile of a machine's backdrop, for machines drawn tile by tile (MachineVisualOverride.PreDraw calls this for each footprint tile right before that tile's own art). The tile pass draws with vanilla's off-screen offset.</summary>
        public void DrawCell(BaseMachine machine, int i, int j, SpriteBatch spriteBatch)
        {
            Backdrop backdrop = GetBackdrop(machine);
            if (backdrop == null || backdrop.Tiles.Count == 0 || !backdrop.ByCell.TryGetValue(new Point(i, j), out BackdropTile t))
                return;

            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange);
            PrepareScale(t, ModContent.GetInstance<PipeFlowVisualSystem>());

            // This tile's piece goes down before its own art but AFTER the tiles drawn earlier, so a swollen piece (bigger than 16x16) would
            // be painted over the neighbours' art as lines. Canvas machines draw the whole backdrop before their art, they are not affected.
            if (ClampTileSwell)
                t.Scale = new Vector2(Math.Min(t.Scale.X, 1f), Math.Min(t.Scale.Y, 1f));

            DrawBody(t, spriteBatch, offset);
        }

        static void PrepareScale(BackdropTile t, PipeFlowVisualSystem flow)
        {
            t.Scale = flow.TryGetBackdropScale(t.Pipe, t.Toward, t.Steps, t.Mask, out Vector2 scale) ? scale : Vector2.One;
        }


        static void DrawBody(BackdropTile t, SpriteBatch spriteBatch, Vector2 offset)
        {
            Tile pipeTile = Main.tile[t.Pipe.X, t.Pipe.Y];
            if (!pipeTile.HasTile)
                return;

            Texture2D texture;
            if (t.FromMotor)
            {
                Main.instance.LoadTiles(t.PipeType);
                texture = TextureAssets.Tile[t.PipeType].Value;
            }
            else
            {
                texture = Main.instance.TilesRenderer.GetTileDrawTexture(pipeTile, t.Pipe.X, t.Pipe.Y);
            }

            if (texture == null)
                return;

            Vector2 center = new Vector2(t.X * 16f + 8f, t.Y * 16f + 8f) - Main.screenPosition + offset;
            Rectangle source = new Rectangle(t.Mask * 18, 0, 16, 16);   // row 0 = never the windowed art
            spriteBatch.Draw(texture, center, source, Lighting.GetColor(t.X, t.Y), 0f, new Vector2(8f, 8f), t.Scale, SpriteEffects.None, 0f);
        }

        // Every pipe tile touching the machine's footprint, plus a signature of them so the paths are only rebuilt when something changed.
        void CollectAttachments(BaseMachine machine, out int signature)
        {
            attachments.Clear();
            int left = machine.cornerPosition.X;
            int top = machine.cornerPosition.Y;
            int width = machine.MachineWidth;
            int height = machine.MachineHeight;

            for (int x = left; x < left + width; x++)
            {
                TryAttach(new Point(x, top), Direction.Down);
                TryAttach(new Point(x, top + height - 1), Direction.Up);
            }

            for (int y = top; y < top + height; y++)
            {
                TryAttach(new Point(left, y), Direction.Right);
                TryAttach(new Point(left + width - 1, y), Direction.Left);
            }

            unchecked
            {
                int hash = left * 73856093 ^ top * 19349663 ^ width * 83492791 ^ height * 2654435;
                for (int n = 0; n < attachments.Count; n++)
                {
                    Attachment a = attachments[n];
                    hash = hash * 31 + a.Pipe.X;
                    hash = hash * 31 + a.Pipe.Y;
                    hash = hash * 31 + a.Type;
                    hash = hash * 31 + (int)a.Dir;
                }

                signature = hash;
            }
        }

        // foot = a footprint tile on the edge; dir = which way the machine lies from the pipe (so the pipe is at foot - dir).
        void TryAttach(Point foot, Direction dir)
        {
            Point step = dir.ToOffset();
            Point pipe = new Point(foot.X - step.X, foot.Y - step.Y);
            if (!WorldGen.InWorld(pipe.X, pipe.Y, 2))
                return;

            Tile tile = Main.tile[pipe.X, pipe.Y];
            if (!tile.HasTile)
                return;

            int type = tile.TileType;
            bool motor = false;
            if (!PipeTierRegistry.IsPipeTile(type))
            {
                // A pump directly facing the machine (its front or back, no pipe between) starts backdrop pipes too, drawn with the pipe of
                // the pump's material. A pump touching the machine with its side does not (it is not connected there either).
                if (!PumpBackdrops || !PipeTierRegistry.IsMotorTile(type)
                    || !TileEntityHelper.TryGetEntityFromTile(pipe.X, pipe.Y, out TileEntity entity, out _)
                    || !(entity is MotorTileEntityBase pump)
                    || (dir != pump.Facing && dir != pump.Facing.Opposite())
                    || !TryGetPipeTypeOfMaterial(type, out type))
                    return;

                motor = true;
            }

            attachments.Add(new Attachment { Pipe = pipe, Dir = dir, Foot = foot, Type = type, Motor = motor });
        }

        // The lowest-numbered pipe tile type that has the same material as a pump tile.
        static bool TryGetPipeTypeOfMaterial(int motorTileType, out int pipeType)
        {
            pipeType = -1;
            if (!PipeTierRegistry.TryGetMaterial(motorTileType, out PipeMaterial material))
                return false;

            foreach (int candidate in PipeTierRegistry.MaxFlowRateByTileType.Keys)
            {
                if (PipeTierRegistry.TryGetMaterial(candidate, out PipeMaterial other) && other == material && (pipeType < 0 || candidate < pipeType))
                    pipeType = candidate;
            }

            return pipeType >= 0;
        }

        Backdrop Build(BaseMachine machine, int signature)
        {
            Backdrop backdrop = new Backdrop { Signature = signature };
            if (attachments.Count == 0)
                return backdrop;

            int left = machine.cornerPosition.X;
            int top = machine.cornerPosition.Y;
            int width = machine.MachineWidth;
            int height = machine.MachineHeight;
            // The goal (priority tile) is picked per pipe below, see PickGoal.

            Dictionary<Point, BackdropTile> byPos = backdrop.ByCell;

            for (int n = 0; n < attachments.Count; n++)
            {
                Attachment a = attachments[n];
                Point goal = PickGoal(machine.ValidTileType, a.Foot, left, top, width, height);
                List<Point> path = FindPath(a.Foot, a.Dir, left, top, width, height, goal);
                Point toward = a.Dir.ToOffset();

                BackdropTile previous = null;
                for (int k = 0; k < path.Count; k++)
                {
                    Point p = path[k];
                    if (!byPos.TryGetValue(p, out BackdropTile tile))
                    {
                        tile = new BackdropTile { X = p.X, Y = p.Y, PipeType = a.Type, Pipe = a.Pipe, FromMotor = a.Motor, Toward = toward, Steps = k + 1 };
                        byPos[p] = tile;
                        backdrop.Tiles.Add(tile);
                    }
                    else if (k + 1 < tile.Steps)
                    {
                        // Two pipes share this tile: it follows the nearer one.
                        tile.PipeType = a.Type;
                        tile.Pipe = a.Pipe;
                        tile.FromMotor = a.Motor;
                        tile.Toward = toward;
                        tile.Steps = k + 1;
                    }

                    if (previous == null)
                    {
                        tile.Mask |= a.Dir.Opposite().ToMaskBit();   // the side the real pipe is on
                    }
                    else
                    {
                        Direction fromPrevious = DirectionExtensions.FromOffset(new Point(p.X - previous.X, p.Y - previous.Y));
                        previous.Mask |= fromPrevious.ToMaskBit();
                        tile.Mask |= fromPrevious.Opposite().ToMaskBit();
                    }

                    previous = tile;
                }
            }

            // The last tile of a path keeps only the sides pipes arrive from: nothing continues past it, so it is a cap (one arriving pipe =
            // a single open side, several = those sides). No auto-opening of the opposite side like a real pipe's dead end.


            return backdrop;
        }

        // Where the backdrop pipes of one pipe end: the machine's registered priority tile if it has one, else by footprint size (bottom
        // tiles first): 2x2 = only the tile the pipe came into; 3x2 = bottom middle; 3x3 = middle; 4x4 = the middle tile (of the middle 2x2)
        // nearest to where the pipe came in. In general, per axis: odd size = the middle; 2 wide = either column (nearest the entry), 2 high =
        // the bottom row; 4 or more even = the two middle ones (nearest the entry). Ties go to the lower, then the left tile.
        Point PickGoal(int tileType, Point foot, int left, int top, int width, int height)
        {
            if (priorityTiles.TryGetValue(tileType, out Point manual))
            {
                int col = manual.X < 0 ? width + manual.X : manual.X;
                int row = manual.Y < 0 ? height + manual.Y : manual.Y;
                return new Point(left + Math.Clamp(col, 0, width - 1), top + Math.Clamp(row, 0, height - 1));
            }

            if (width == 2 && height == 2)
                return foot;

            AxisCandidates(width, false, out int colFirst, out int colLast);
            AxisCandidates(height, true, out int rowFirst, out int rowLast);

            int footCol = foot.X - left;
            int footRow = foot.Y - top;
            Point best = new Point(colFirst, rowLast);
            int bestDistance = int.MaxValue;
            for (int row = rowLast; row >= rowFirst; row--)
            {
                for (int col = colFirst; col <= colLast; col++)
                {
                    int distance = Math.Abs(col - footCol) + Math.Abs(row - footRow);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = new Point(col, row);
                    }
                }
            }

            return new Point(left + best.X, top + best.Y);
        }

        // The tiles of one axis a pipe may run to: size 1 or odd = the middle; 2 = the bottom one (bottomBias, the vertical axis) or both;
        // 4 or more even = the two middle ones.
        static void AxisCandidates(int size, bool bottomBias, out int first, out int last)
        {
            if (size % 2 == 1)
            {
                first = size / 2;
                last = size / 2;
            }
            else if (size == 2)
            {
                first = bottomBias ? 1 : 0;
                last = 1;
            }
            else
            {
                first = size / 2 - 1;
                last = size / 2;
            }
        }

        // Cheapest route inside the footprint rectangle from start to goal, 4 directions, every tile costs 1 and every change of direction
        // TurnPenalty more (the entry direction is the way the pipe came in, so carrying straight on is free). The footprint is a few tiles,
        // so a plain scan for the cheapest open state is plenty (this only runs when the pipes around a machine change).
        List<Point> FindPath(Point start, Direction startDir, int left, int top, int width, int height, Point goal)
        {
            int states = width * height * 4;
            int[] cost = new int[states];
            int[] previous = new int[states];
            bool[] closed = new bool[states];
            for (int n = 0; n < states; n++)
            {
                cost[n] = int.MaxValue;
                previous[n] = -1;
            }

            int Index(int x, int y, int d) => ((y - top) * width + (x - left)) * 4 + d;

            cost[Index(start.X, start.Y, (int)startDir)] = 0;

            while (true)
            {
                int current = -1;
                int best = int.MaxValue;
                for (int n = 0; n < states; n++)
                {
                    if (!closed[n] && cost[n] < best)
                    {
                        best = cost[n];
                        current = n;
                    }
                }

                if (current < 0)
                    break;

                closed[current] = true;
                int dir = current % 4;
                int cell = current / 4;
                int x = left + cell % width;
                int y = top + cell / width;

                if (x == goal.X && y == goal.Y)
                {
                    List<Point> path = new List<Point>();
                    for (int s = current; s >= 0; s = previous[s])
                    {
                        int c = s / 4;
                        path.Add(new Point(left + c % width, top + c / width));
                    }

                    path.Reverse();
                    return path;
                }

                for (int nd = 0; nd < 4; nd++)
                {
                    Point o = ((Direction)nd).ToOffset();
                    int nx = x + o.X;
                    int ny = y + o.Y;
                    if (nx < left || nx >= left + width || ny < top || ny >= top + height)
                        continue;

                    int next = Index(nx, ny, nd);
                    int c2 = best + 1 + (nd != dir ? TurnPenalty : 0);
                    if (c2 < cost[next])
                    {
                        cost[next] = c2;
                        previous[next] = current;
                    }
                }
            }

            return new List<Point> { start };   // unreachable cannot happen inside a full rectangle; just draw the entry tile
        }
    }
}