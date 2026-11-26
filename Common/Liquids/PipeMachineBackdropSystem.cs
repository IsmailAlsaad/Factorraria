using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Common.Liquids
{
    // COSMETIC ONLY, CLIENT ONLY: nothing here is saved, sent or read by the liquid network. A machine's footprint (its TileEntity tiles)
    // can be bigger than what its sprite shows, so a pipe that touches the footprint looks like it floats with a gap before the sprite
    // even though it really is connected. For every pipe touching a machine that has liquid ports, this draws extra pipe tiles BEHIND the
    // machine: the footprint tile right next to the pipe (so the pipe carries on one tile in the direction it came from), then a path
    // through the footprint (4 directions only, straight runs preferred) to the machine's centre tile. The machine's own sprite is drawn
    // over them, so they only show in the gap.
    //
    // Multiplayer: no packets, no authority. Every client works it out from the tiles and the machine entity it already has (the pipe
    // tiles, the entity's footprint size) and from PipeFlowVisualSystem's own client-side wave, so single player and multiplayer are the same.
    //
    // Look: the sprite (and overdraw art) of the pipe that touches the machine, always the non-windowed frame (TileFrameY 0), lit like a
    // normal tile, not darkened. Swell: the wave of the pipe that touches the machine carries on into the backdrop pipes
    // (PipeFlowVisualSystem.TryGetBackdropScale), so the animation flows through them.
    //
    // Drawing rides on the DrawNPCs(behindTiles: true) call vanilla makes before any tile is drawn (same ride as TileOverdrawSystem), into
    // the SpriteBatch vanilla already has open there.
    public class PipeMachineBackdropSystem : ModSystem
    {
        // ---- Tuning (expression-bodied properties on purpose: hot reload replaces a body, not a field initializer) ----
        public int TurnPenalty => 2;               // path cost of a corner, on top of 1 per tile: higher = straighter paths
        public int PruneIntervalTicks => 3600;     // cached backdrops of removed machines are dropped this often

        sealed class BackdropTile
        {
            public int X;
            public int Y;
            public int Mask;          // connection mask like a real pipe's frame: up 1, down 2, left 4, right 8
            public int PipeType;      // tile type of the pipe this tile copies
            public Point Pipe;        // the real pipe that touches the machine (its wave is carried on)
            public Point Toward;      // the step from that pipe into the machine
            public int Steps;         // tiles between that pipe and this tile along the path (the first footprint tile is 1)
            public Vector2 Scale;     // set every frame
        }

        sealed class Backdrop
        {
            public int Signature;
            public readonly List<BackdropTile> Tiles = new List<BackdropTile>();
        }

        struct Attachment
        {
            public Point Pipe;
            public Direction Dir;     // from the pipe into the machine
            public Point Foot;        // the footprint tile next to the pipe
            public int Type;
        }

        readonly Dictionary<Point16, Backdrop> backdrops = new Dictionary<Point16, Backdrop>();
        readonly List<Backdrop> visible = new List<Backdrop>();
        readonly List<Attachment> attachments = new List<Attachment>();
        int lastPruneTick;

        public override void Load()
        {
            if (Main.dedServ) return; // no screen on a dedicated server
            On_Main.DrawNPCs += DrawBehindTilesPass;
        }

        public override void Unload()
        {
            backdrops.Clear();
            if (Main.dedServ) return; // matches Load()
            On_Main.DrawNPCs -= DrawBehindTilesPass;
        }

        public override void OnWorldUnload()
        {
            backdrops.Clear();
            visible.Clear();
        }

        void DrawBehindTilesPass(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles)
        {
            if (behindTiles)
                DrawBackdrops(Main.spriteBatch);

            orig(self, behindTiles);
        }

        // ------------------------------------------------------------------ work out

        void DrawBackdrops(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if ((int)Main.GameUpdateCount - lastPruneTick > PruneIntervalTicks || (int)Main.GameUpdateCount < lastPruneTick)
            {
                lastPruneTick = (int)Main.GameUpdateCount;
                backdrops.Clear();
            }

            visible.Clear();
            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not BaseMachine machine || entity is MotorTileEntityBase || !machine.HasLiquidPorts || !machine.InitializeGeometry())
                    continue;

                float margin = Math.Max(machine.MachineWidth, machine.MachineHeight) * 16f + 48f;
                if (!BaseMachine.IsOnScreen(machine.MachineCenter, margin))
                    continue;

                CollectAttachments(machine, out int signature);

                if (!backdrops.TryGetValue(machine.Position, out Backdrop backdrop) || backdrop.Signature != signature)
                {
                    backdrop = Build(machine, signature);
                    backdrops[machine.Position] = backdrop;
                }

                if (backdrop.Tiles.Count > 0)
                    visible.Add(backdrop);
            }

            if (visible.Count == 0)
                return;

            PipeFlowVisualSystem flow = ModContent.GetInstance<PipeFlowVisualSystem>();
            TileOverdrawSystem overdraw = ModContent.GetInstance<TileOverdrawSystem>();

            // Same order as real pipes: the art outside the tile first (it sits behind every pipe body), then the bodies.
            for (int b = 0; b < visible.Count; b++)
            {
                List<BackdropTile> tiles = visible[b].Tiles;
                for (int n = 0; n < tiles.Count; n++)
                {
                    BackdropTile t = tiles[n];
                    t.Scale = flow.TryGetBackdropScale(t.Pipe, t.Toward, t.Steps, t.Mask, out Vector2 scale) ? scale : Vector2.One;
                    overdraw.TryDrawCell(spriteBatch, t.PipeType, t.Mask, t.X, t.Y, Lighting.GetColor(t.X, t.Y), t.Scale);
                }
            }

            for (int b = 0; b < visible.Count; b++)
            {
                List<BackdropTile> tiles = visible[b].Tiles;
                for (int n = 0; n < tiles.Count; n++)
                {
                    BackdropTile t = tiles[n];
                    Tile pipeTile = Main.tile[t.Pipe.X, t.Pipe.Y];
                    if (!pipeTile.HasTile)
                        continue;

                    Texture2D texture = Main.instance.TilesRenderer.GetTileDrawTexture(pipeTile, t.Pipe.X, t.Pipe.Y);
                    if (texture == null)
                        continue;

                    Vector2 center = new Vector2(t.X * 16f + 8f, t.Y * 16f + 8f) - Main.screenPosition;
                    Rectangle source = new Rectangle(t.Mask * 18, 0, 16, 16);   // row 0 = never the windowed art
                    spriteBatch.Draw(texture, center, source, Lighting.GetColor(t.X, t.Y), 0f, new Vector2(8f, 8f), t.Scale, SpriteEffects.None, 0f);
                }
            }
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
            if (!tile.HasTile || !PipeTierRegistry.IsPipeTile(tile.TileType))
                return;

            attachments.Add(new Attachment { Pipe = pipe, Dir = dir, Foot = foot, Type = tile.TileType });
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
            Point goal = new Point(left + (width - 1) / 2, top + (height - 1) / 2);   // the machine's centre tile

            Dictionary<Point, BackdropTile> byPos = new Dictionary<Point, BackdropTile>();

            for (int n = 0; n < attachments.Count; n++)
            {
                Attachment a = attachments[n];
                List<Point> path = FindPath(a.Foot, a.Dir, left, top, width, height, goal);
                Point toward = a.Dir.ToOffset();

                BackdropTile previous = null;
                for (int k = 0; k < path.Count; k++)
                {
                    Point p = path[k];
                    if (!byPos.TryGetValue(p, out BackdropTile tile))
                    {
                        tile = new BackdropTile { X = p.X, Y = p.Y, PipeType = a.Type, Pipe = a.Pipe, Toward = toward, Steps = k + 1 };
                        byPos[p] = tile;
                        backdrop.Tiles.Add(tile);
                    }
                    else if (k + 1 < tile.Steps)
                    {
                        // Two pipes share this tile: it follows the nearer one.
                        tile.PipeType = a.Type;
                        tile.Pipe = a.Pipe;
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

            // A dead end opens on its opposite side, exactly like a real pipe with one connection (PipeConnectionHelper.GetOpenSides).
            for (int n = 0; n < backdrop.Tiles.Count; n++)
            {
                BackdropTile t = backdrop.Tiles[n];
                int bits = (t.Mask & 1) + ((t.Mask >> 1) & 1) + ((t.Mask >> 2) & 1) + ((t.Mask >> 3) & 1);
                if (bits != 1)
                    continue;

                if ((t.Mask & 1) != 0) t.Mask |= 2;
                else if ((t.Mask & 2) != 0) t.Mask |= 1;
                else if ((t.Mask & 4) != 0) t.Mask |= 8;
                else t.Mask |= 4;
            }

            return backdrop;
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