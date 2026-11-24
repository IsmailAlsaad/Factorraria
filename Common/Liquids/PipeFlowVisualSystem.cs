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
    // What the visuals know about one pipe tile that liquid is moving through right now.
    public sealed class PipeFlowCell
    {
        public float S;          // path coordinate along the flow: 0 at the pump, +n for the n-th tile downstream, -n for the n-th tile upstream
        public Point Travel;     // the unit step the liquid takes through this tile (the pump's facing downstream, the reverse upstream)
        public int Liquid;       // LiquidTypeRegistry id (colour and dust)
        public int EndMask;      // straight pipes only: bit (1 << Direction) for every end that opens onto empty space
        public float Intensity;  // 0..1, fades in and out so the animation never pops
        public int Stamp;        // the rebuild this cell was last seen in
    }

    // CLIENT ONLY and purely cosmetic: nothing here is saved or sent. It reads what every client already has: the motor replicas
    // (isOn, Facing and FlowLiquidType, which the server writes and sends in the motor's light update) and the pipe tiles.
    //
    // Every few ticks it walks the pipes from each running pump: downstream along its Facing (ON pumps are one-way) and upstream
    // behind it, giving every pipe a path coordinate S. A wave then runs along S: a "ball" of fluid travels intake -> pump -> outlet,
    // swelling the pipes it passes and squeezing them a little right behind it.
    //
    // Drawing (PostDrawTiles): a pipe that is part of a flow is NOT drawn by vanilla (PipeTileBase.PreDraw says no) but here, scaled,
    // with the window liquid drawn BEHIND it (so the window art's transparent hole masks the liquid) and the open-end blobs in front.
    // The liquid in a window and at an open end is a small 2D metaball field evaluated per pixel (no shader): a pixel is liquid when the
    // summed field of the blobs crosses MetaballThreshold, so blobs that come close melt into each other.
    public class PipeFlowVisualSystem : ModSystem
    {
        // ---- Tuning (instance fields on purpose: hot reload can change them) ----
        public float BallSpeedTilesPerSecond = 3.5f;
        public float BallLengthTiles = 3f;        // the swelling lobe; the squeeze lobe right behind it is as long
        public float BallSpacingTiles = 9f;       // distance between two balls (must be more than 2 * BallLengthTiles)
        public float SwellAmount = 0.2f;          // extra scale at the middle of the ball
        public float SqueezeAmount = 0.07f;       // scale lost just behind the ball
        public int RebuildIntervalTicks = 10;
        public int MaxWalkPipes = 600;            // per pump and side
        public float MaxRangeTiles = 90f;         // pumps farther than this from the player are not animated
        public float FadePerTick = 0.1f;

        // Window rectangles in tile pixels (the 16x16 cell, 0,0 = top-left). Match them to the transparent hole in the art.
        public Rectangle WindowHorizontal = new Rectangle(3, 5, 10, 6);
        public Rectangle WindowVertical = new Rectangle(5, 3, 6, 10);

        // The blobs of one ball: offset along the pipe in tiles from the ball's centre (negative = behind it) and radius in pixels.
        public float[] BlobOffsetsTiles = { 0f, -1.55f, 1.4f };
        public float[] BlobRadiiPx = { 5.5f, 2.6f, 2f };
        public float MetaballThreshold = 1f;
        public float MetaballCore = 2.2f;         // field value where the lighter core colour starts

        public float OpenEndReachPx = 5f;         // how far the blob at an open end pokes out at its biggest
        public int LeakOneInChance = 25;          // per tick while a ball is inside a pipe tile that has air next to it
        public int MaxLeaksPerTick = 2;

        readonly Dictionary<Point, PipeFlowCell> cells = new Dictionary<Point, PipeFlowCell>();
        readonly Queue<(Point pos, int dist, Point step)> queue = new Queue<(Point, int, Point)>();
        readonly HashSet<Point> walked = new HashSet<Point>();
        readonly List<Point> removeBuffer = new List<Point>();
        readonly float[] blobX = new float[8];
        readonly float[] blobY = new float[8];
        readonly float[] blobR = new float[8];
        int stamp;
        int tickCounter;

        public override void Unload()
        {
            cells.Clear();
            queue.Clear();
            walked.Clear();
        }

        public override void OnWorldUnload()
        {
            cells.Clear();
        }

        /// <summary>True while this pipe is part of a flow the visuals draw themselves (PipeTileBase.PreDraw then skips the vanilla draw).</summary>
        public bool TryGetCell(int i, int j, out PipeFlowCell cell)
        {
            if (cells.Count == 0)
            {
                cell = null;
                return false;
            }
            return cells.TryGetValue(new Point(i, j), out cell);
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
            SpawnLeaks();
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
                Record(pos, downstream ? dist : -dist, downstream ? stepHere : new Point(-stepHere.X, -stepHere.Y), motor.FlowLiquidType, tile);

                for (int d = 0; d < 4; d++)
                {
                    Point offset = ((Direction)d).ToOffset();
                    queue.Enqueue((pos + offset, dist + 1, offset));
                }
            }
        }

        void Record(Point pos, float s, Point travel, int liquid, Tile tile)
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
            cell.Stamp = stamp;
            cell.EndMask = ComputeEndMask(pos.X, pos.Y, tile);
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

        // ------------------------------------------------------------------ leaks

        void SpawnLeaks()
        {
            if (cells.Count == 0 || Main.gamePaused)
                return;

            VisibleTileScan.GetRange(1, out int left, out int right, out int top, out int bottom);
            int spawned = 0;

            foreach (KeyValuePair<Point, PipeFlowCell> pair in cells)
            {
                if (spawned >= MaxLeaksPerTick)
                    break;

                PipeFlowCell cell = pair.Value;
                Point p = pair.Key;
                if (cell.Intensity < 0.9f || p.X < left || p.X > right || p.Y < top || p.Y > bottom)
                    continue;

                if (Lobe(Phase(cell.S)) < 0.7f || !Main.rand.NextBool(Math.Max(1, LeakOneInChance)))
                    continue;

                int start = Main.rand.Next(4);
                for (int n = 0; n < 4; n++)
                {
                    Point o = ((Direction)((start + n) % 4)).ToOffset();
                    if (!WorldGen.InWorld(p.X + o.X, p.Y + o.Y, 1) || Main.tile[p.X + o.X, p.Y + o.Y].HasTile)
                        continue;

                    SpawnLeakDust(p, o, cell.Liquid);
                    spawned++;
                    break;
                }
            }
        }

        static void SpawnLeakDust(Point tilePos, Point side, int liquid)
        {
            Vector2 position = new Vector2(tilePos.X * 16f + 8f + side.X * 8f, tilePos.Y * 16f + 8f + side.Y * 8f);
            Vector2 velocity = new Vector2(side.X, side.Y) * Main.rand.NextFloat(0.4f, 1.4f) + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.4f, 0f));

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

            Dust dust = Dust.NewDustPerfect(position, dustType, velocity, 0, tint, Main.rand.NextFloat(0.8f, 1.1f));
            dust.noGravity = false;
        }

        static Color LiquidColor(int liquid)
        {
            if (liquid < 0 || liquid >= LiquidTypeRegistry.definitions.Count)
                return Color.White;

            Color c = LiquidTypeRegistry.Get(liquid).RenderColor;
            return new Color(c.R, c.G, c.B, (byte)255);
        }

        // ------------------------------------------------------------------ drawing

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu || cells.Count == 0)
                return;

            VisibleTileScan.GetRange(2, out int left, out int right, out int top, out int bottom);
            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (KeyValuePair<Point, PipeFlowCell> pair in cells)
            {
                Point p = pair.Key;
                if (p.X < left || p.X > right || p.Y < top || p.Y > bottom)
                    continue;

                DrawCell(spriteBatch, p.X, p.Y, pair.Value);
            }

            spriteBatch.End();
        }

        void DrawCell(SpriteBatch spriteBatch, int i, int j, PipeFlowCell cell)
        {
            Tile tile = Main.tile[i, j];
            if (!tile.HasTile || !PipeTierRegistry.IsPipeTile(tile.TileType))
                return;

            Color light = Lighting.GetColor(i, j);
            Color liquid = LiquidColor(cell.Liquid);
            float u = Phase(cell.S);
            float scale = ScaleAt(u, cell.Intensity);

            int mask = tile.TileFrameX / 18;
            bool horizontal = mask == 12;
            bool straight = horizontal || mask == 3;
            Vector2 origin = new Vector2(i * 16f, j * 16f) - Main.screenPosition;

            Color body = liquid.MultiplyLight(Color.Lerp(light, Color.White, 0.2f));
            Color core = Color.Lerp(liquid, Color.White, 0.35f).MultiplyLight(Color.Lerp(light, Color.White, 0.2f));

            // 1. Window liquid, behind the tile so the art's transparent hole masks it.
            if (straight && tile.TileFrameY >= 18 && (horizontal ? cell.Travel.X : cell.Travel.Y) != 0)
            {
                int count = FillWindowBlobs(cell, horizontal);
                Rectangle window = horizontal ? WindowHorizontal : WindowVertical;
                DrawMetaballs(spriteBatch, origin, window, count, false, body, core);
            }

            // 2. The tile itself, scaled. Straight pipes swell across their axis only; corners and junctions swell evenly.
            Texture2D texture = Main.instance.TilesRenderer.GetTileDrawTexture(tile, i, j);
            if (texture != null)
            {
                Vector2 scaleVector = !straight ? new Vector2(scale) : horizontal ? new Vector2(1f, scale) : new Vector2(scale, 1f);
                Rectangle source = new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16);
                spriteBatch.Draw(texture, origin + new Vector2(8f, 8f), source, light, 0f, new Vector2(8f, 8f), scaleVector, SpriteEffects.None, 0f);
            }

            // 3. A blob poking out of the open end the liquid leaves through.
            if (cell.EndMask != 0)
                DrawOpenEnds(spriteBatch, origin, cell, u, body, core);
        }

        // Puts the blobs of the nearest ball into blobX/Y/R, in tile pixels. Returns how many.
        int FillWindowBlobs(PipeFlowCell cell, bool horizontal)
        {
            float spacing = Math.Max(0.5f, BallSpacingTiles);
            float firstCentre = Time * BallSpeedTilesPerSecond + BallLengthTiles * 0.5f;
            float centre = firstCentre + MathF.Round((cell.S - firstCentre) / spacing) * spacing;
            int sign = horizontal ? cell.Travel.X : cell.Travel.Y;

            int count = Math.Min(Math.Min(BlobOffsetsTiles.Length, BlobRadiiPx.Length), blobX.Length);
            for (int k = 0; k < count; k++)
            {
                float along = 8f + (centre + BlobOffsetsTiles[k] - cell.S) * 16f * sign;
                blobX[k] = horizontal ? along : 8f;
                blobY[k] = horizontal ? 8f : along;
                blobR[k] = BlobRadiiPx[k];
            }
            return count;
        }

        void DrawOpenEnds(SpriteBatch spriteBatch, Vector2 origin, PipeFlowCell cell, float u, Color body, Color core)
        {
            float lobe = Lobe(u);
            if (lobe < 0.05f)
                return;

            for (int d = 0; d < 4; d++)
            {
                if ((cell.EndMask & (1 << d)) == 0)
                    continue;

                Point o = ((Direction)d).ToOffset();
                if (o != cell.Travel)
                    continue;   // only the end the liquid leaves through

                float reach = -2f + (OpenEndReachPx + 2f) * lobe;
                float f = Math.Clamp(u / Math.Max(0.5f, BallLengthTiles), 0f, 1f);

                // Blob 0 swells out of the mouth. Blob 1 is a drip that detaches from it and falls.
                blobX[0] = 8f + o.X * (8f + reach);
                blobY[0] = 8f + o.Y * (8f + reach);
                blobR[0] = 1.5f + 2.5f * lobe;
                blobX[1] = 8f + o.X * 11f;
                blobY[1] = 8f + o.Y * 11f + 2f + 12f * f * f;
                blobR[1] = 1.7f * (1f - 0.5f * f);

                Rectangle region = new Rectangle((int)(blobX[0] - 10f), (int)(blobY[0] - 10f), 20, 30);
                DrawMetaballs(spriteBatch, origin, region, 2, true, body, core);
            }
        }

        // Fills every pixel of region (tile pixels) whose metaball field reaches the threshold, one horizontal run at a time.
        void DrawMetaballs(SpriteBatch spriteBatch, Vector2 origin, Rectangle region, int count, bool skipInsideTile, Color body, Color core)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle pixelSource = new Rectangle(0, 0, 1, 1);

            for (int y = region.Top; y < region.Bottom; y++)
            {
                int runStart = 0;
                int runState = 0;

                for (int x = region.Left; x <= region.Right; x++)
                {
                    int state = 0;
                    if (x < region.Right && !(skipInsideTile && x >= 0 && x < 16 && y >= 0 && y < 16))
                    {
                        float field = 0f;
                        for (int k = 0; k < count; k++)
                        {
                            float dx = x + 0.5f - blobX[k];
                            float dy = y + 0.5f - blobY[k];
                            field += blobR[k] * blobR[k] / (dx * dx + dy * dy + 1f);
                        }
                        state = field >= MetaballCore ? 2 : field >= MetaballThreshold ? 1 : 0;
                    }

                    if (state == runState)
                        continue;

                    if (runState != 0)
                        spriteBatch.Draw(pixel, origin + new Vector2(runStart, y), pixelSource, runState == 2 ? core : body, 0f, Vector2.Zero, new Vector2(x - runStart, 1f), SpriteEffects.None, 0f);

                    runStart = x;
                    runState = state;
                }
            }
        }
    }

    internal static class PipeFlowColorExtensions
    {
        // Lighting-style multiply of two colours (channel by channel), alpha kept from the first.
        public static Color MultiplyLight(this Color color, Color light)
        {
            return new Color((byte)(color.R * light.R / 255), (byte)(color.G * light.G / 255), (byte)(color.B * light.B / 255), color.A);
        }
    }
}