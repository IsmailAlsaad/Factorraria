using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.BeeWheel
{
    // Power producer: 5 power per exposed Hive tile found by BeeWheelScanner within 50 tiles of path length, no fuel, no slots.
    //
    // Multiplayer: the server (and single-player) scan their own tiles; HiveCount sets PowerSupply and isWorking, which the existing
    // snapshot / light updates already sync to clients. A client has no authority over power: it scans its own copy of the tiles
    // only for the visuals (bee start points, honey, wheel speed), so no new packet is needed and nothing is saved.
    // Update() runs on the server / single-player, DynamicUpdate() on clients / single-player; both call EnsureScan, which runs
    // each scan once whichever caller reaches it first.
    public class BeeWheelTileEntity : ElectricProducerMachine, IMachineDynamicDraw
    {
        public override int ValidTileType => ModContent.TileType<BeeWheelTile>();
        public override float PowerSupply => scanner.HiveCount * PowerPerHive;

        // ---- Tunables (expression-bodied so hot reload picks changes up) ----
        public float PowerPerHive => 5f;               // power points per exposed Hive tile
        public int HiveScanRadius => 50;               // path length of the flood fill, in tiles
        public int RescanDelayTicks => 10;             // a nearby tile change schedules the next scan this many ticks later at the earliest (debounce)
        public int SafetyRescanTicks => 120;            // scan again at least this often even when no change was reported

        public float MinWheelSpeed => 0.001f;           // radians per tick with the fewest hives
        public float MaxWheelSpeed => 0.3f;           // radians per tick at HivesForMaxSpeed hives or more
        public int HivesForMaxSpeed => 300;
        public float WheelSpeedEase => 0.03f;          // 0..1, how fast the wheel speeds up and slows down

        public float BigBeeWeight => 1f;               // relative frequency of the big bee ...
        public float SmallBeeWeight => 3f;             // ... and of the small bee
        public float BeeSpawnPerHive => 0.001f;        // bees per tick per exposed Hive tile
        public int MaxBees => 60;
        public float BeeSpeed => 2.2f;                 // pixels per tick along the path
        public float BeeWobble => 10f;                 // sideways sway in pixels
        public float BeeScale => 1f;
        public float BeeFullOpacityAt => 1f;           // fraction of the flight at which a bee is fully opaque (opacity rises from 0 at the hive)
        public float BeeFadeOutTiles => 2f;            // a bee fades out over its last this many tiles before the wheel
        public int BeeFrameTicks => 4;
        public int CustomBeeFrames => 4;              // frames in BeeWheel_BeeBig.png / BeeWheel_BeeSmall.png when you add them
        public bool BeeSheetFacesLeft => true;         // the vanilla bee faces left

        // Bees of a broken wheel: each one slows down to a stop, hovers a random time, then fades out over a random time (so they vanish one by one).
        public float DyingSlowdownMin => 0.95f;        // speed is multiplied by a random value between these two every tick (lower = stops sooner)
        public float DyingSlowdownMax => 0.98f;
        public float DyingStopSpeed => 0.04f;          // below this fraction of the flight speed the bee counts as stopped and its hover timer starts
        public int DyingHoverMinTicks => 10;           // random hover time after the stop, before the fade begins
        public int DyingHoverMaxTicks => 90;
        public int DyingFadeMinTicks => 30;            // random fade-out duration
        public int DyingFadeMaxTicks => 100;
        public float DyingBrightenPerTick => 0.04f;    // a bee that was fading into the (now gone) wheel eases back to full opacity at this rate

        public int HoneyTilesPerTry => 25;             // one honey attempt per this many exposed hives
        public int MaxHoneyTries => 6;                 // attempts per tick
        public float HoneyDustChance => 0.25f;
        public float VisualRange => 700f;              // bees and honey are only simulated this close (pixels) to the screen

        // ---- State ----
        readonly BeeWheelScanner scanner = new BeeWheelScanner();
        public int HiveCount => scanner.HiveCount;
        public int ExposedSolidCount => scanner.ExposedSolidCount;

        bool scannedOnce;
        uint nextScanAt;

        // Client-only visuals, never saved or sent.
        bool angleInitialized;
        float wheelAngle;
        float wheelSpeed;
        float beeSpawnAccumulator;
        readonly List<Bee> bees = new List<Bee>();

        sealed class Bee
        {
            public Vector2 Start;
            public Vector2 Pos;
            public float Progress;   // 0 at the hive, 1 at the wheel
            public float Step;       // progress per tick
            public float Length;     // pixels from the hive to the wheel (to know how far from the wheel it is)
            public float Phase;
            public float Wobble;
            public bool Big;
            public int Age;
            public bool MovingRight;

            // Only used once the wheel is gone (see "Bees of a removed wheel").
            public bool Dying;
            public Vector2 Base;        // sway-free position
            public Vector2 Velocity;    // pixels per tick at Speed 1
            public Vector2 Side;        // unit vector the sway is applied along
            public float SwayAmount;    // sway in pixels at Speed 1
            public float Speed;         // 1 at the moment the wheel breaks, shrinks to 0
            public float Slow;          // per-tick multiplier of Speed
            public float ApproachAlpha; // opacity factor from the old "fade into the wheel" rule
            public int StoppedFor;      // ticks since Speed dropped below DyingStopSpeed
            public int HoverTicks;
            public int FadeTicks;
        }

        Vector2 WheelCenter => BeeWheelVisual.WheelCenter(cornerPosition.ToVector2() * 16f, MachineWidth);

        public override void Update()
        {
            base.Update();
            EnsureScan();

            // Supply only counts while at least one exposed hive is in range.
            isWorking = scanner.HiveCount > 0;
        }

        // ---- Scanning ----

        void EnsureScan()
        {
            uint now = Main.GameUpdateCount;
            if (scannedOnce && now < nextScanAt)
                return;

            if (!InitializeGeometry())
                return;

            scanner.Scan(cornerPosition.X, cornerPosition.Y, MachineWidth, MachineHeight, HiveScanRadius, !Main.dedServ);
            scannedOnce = true;
            nextScanAt = now + (uint)Math.Max(1, SafetyRescanTicks);
        }

        // Called for every placed or killed tile (see BeeWheelTileHooks). Only pulls scan times forward, so calling it twice is harmless.
        public static void NotifyTileChanged(int i, int j)
        {
            if (Main.gameMenu || TileEntity.ByPosition.Count == 0)
                return;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is BeeWheelTileEntity wheel)
                    wheel.OnNearbyTileChanged(i, j);
            }
        }

        void OnNearbyTileChanged(int i, int j)
        {
            if (!MachineInitialized)
                return;

            int range = HiveScanRadius + 2;
            int dx = i - (cornerPosition.X + MachineWidth / 2);
            int dy = j - (cornerPosition.Y + MachineHeight / 2);
            if (Math.Abs(dx) > range + MachineWidth || Math.Abs(dy) > range + MachineHeight)
                return;

            uint due = Main.GameUpdateCount + (uint)Math.Max(1, RescanDelayTicks);
            if (!scannedOnce || nextScanAt > due)
                nextScanAt = due;
        }

        // ---- IMachineDynamicDraw (client) ----

        public void DynamicUpdate()
        {
            if (!InitializeGeometry())
                return;

            EnsureScan();

            bool running = isOn && isWorking;

            if (!angleInitialized)
            {
                wheelAngle = StartAngleFor(Position);
                angleInitialized = true;
            }

            float hiveFraction = MathHelper.Clamp(scanner.HiveCount / (float)Math.Max(1, HivesForMaxSpeed), 0f, 1f);
            float targetSpeed = running ? MathHelper.Lerp(MinWheelSpeed, MaxWheelSpeed, hiveFraction) : 0f;
            wheelSpeed = MathHelper.Lerp(wheelSpeed, targetSpeed, WheelSpeedEase);
            if (targetSpeed == 0f && wheelSpeed < 0.0005f)
                wheelSpeed = 0f;
            wheelAngle = MathHelper.WrapAngle(wheelAngle + wheelSpeed);

            if (!IsOnScreen(MachineCenter, VisualRange))
            {
                bees.Clear();
                beeSpawnAccumulator = 0f;
                return;
            }

            UpdateBees(running);
            UpdateHoney(running);
        }

        public bool TryGetDynamicBounds(out Vector2 center, out float radius)
        {
            center = MachineCenter;
            radius = (HiveScanRadius + 6) * 16f;   // bees can start this far from the wheel
            return InitializeGeometry();
        }

        // Empty on purpose: BeeWheelDrawSystem draws the wheel and bees (DrawFront) after the base, so they end up in front of it.
        public void DynamicDraw(SpriteBatch spriteBatch) { }

        public void DrawFront(SpriteBatch spriteBatch)
        {
            if (!InitializeGeometry())
                return;

            Vector2 center = WheelCenter;
            BeeWheelVisual.DrawWheel(spriteBatch, center, wheelAngle, Lighting.GetColor(center.ToTileCoordinates()));

            foreach (Bee bee in bees)
                DrawBee(spriteBatch, bee);
        }

        // The start angle is a stable pseudo-random value of the placement position: every client agrees without a packet,
        // and it survives saving and reloading.
        static float StartAngleFor(Point16 position)
        {
            unchecked
            {
                uint h = (uint)(position.X * 73856093) ^ (uint)(position.Y * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h % 6283u) / 1000f;
            }
        }

        // ---- Bees of a removed wheel ----
        // A broken wheel's entity leaves TileEntity.ByPosition, so its DynamicUpdate / DrawFront stop at once and its bees used to vanish.
        // BeeWheelDrawSystem calls UpdateOrphans every tick (client / single-player only; visuals, nothing is saved or sent): it notices a
        // tracked wheel that left the world and keeps that dead entity object alive, only as the owner of its bee list and its tunables.
        // Each bee turns into a free flyer that slows down to a stop, hovers a random time, then fades out over a random time.
        static readonly List<BeeWheelTileEntity> trackedWheels = new List<BeeWheelTileEntity>();
        static readonly List<BeeWheelTileEntity> orphanedWheels = new List<BeeWheelTileEntity>();
        static readonly List<BeeWheelTileEntity> liveWheelsScratch = new List<BeeWheelTileEntity>();
        Vector2 orphanCenter;

        public static bool HasOrphanBees => orphanedWheels.Count > 0;

        public static void ResetOrphans()
        {
            trackedWheels.Clear();
            orphanedWheels.Clear();
            liveWheelsScratch.Clear();
        }

        public static void UpdateOrphans()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            liveWheelsScratch.Clear();
            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is BeeWheelTileEntity wheel)
                    liveWheelsScratch.Add(wheel);
            }

            for (int i = trackedWheels.Count - 1; i >= 0; i--)
            {
                BeeWheelTileEntity wheel = trackedWheels[i];
                if (liveWheelsScratch.Contains(wheel))
                    continue;

                trackedWheels.RemoveAt(i);
                if (wheel.BecomeOrphan())
                    orphanedWheels.Add(wheel);
            }

            foreach (BeeWheelTileEntity wheel in liveWheelsScratch)
            {
                if (!trackedWheels.Contains(wheel))
                    trackedWheels.Add(wheel);
            }

            for (int i = orphanedWheels.Count - 1; i >= 0; i--)
            {
                BeeWheelTileEntity wheel = orphanedWheels[i];
                wheel.UpdateOrphanBees();
                if (wheel.bees.Count == 0)
                    orphanedWheels.RemoveAt(i);
            }
        }

        public static void DrawOrphanBees(SpriteBatch spriteBatch)
        {
            foreach (BeeWheelTileEntity wheel in orphanedWheels)
            {
                if (!IsOnScreen(wheel.orphanCenter, wheel.VisualRange))
                    continue;

                foreach (Bee bee in wheel.bees)
                    wheel.DrawBee(spriteBatch, bee);
            }
        }

        // Returns false when there is nothing to animate.
        bool BecomeOrphan()
        {
            if (bees.Count == 0)
                return false;

            orphanCenter = WheelCenter;

            foreach (Bee bee in bees)
            {
                Vector2 along = orphanCenter - bee.Start;

                bee.Dying = true;
                bee.Base = Vector2.Lerp(bee.Start, orphanCenter, bee.Progress);
                bee.Velocity = along * bee.Step;
                bee.Side = along.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                bee.SwayAmount = bee.Wobble * (1f - bee.Progress);
                bee.Speed = 1f;
                bee.Slow = Main.rand.NextFloat(Math.Min(DyingSlowdownMin, DyingSlowdownMax), Math.Max(DyingSlowdownMin, DyingSlowdownMax));
                bee.ApproachAlpha = ApproachFade(bee);
                bee.StoppedFor = 0;
                bee.HoverTicks = Main.rand.Next(Math.Max(0, DyingHoverMinTicks), Math.Max(DyingHoverMinTicks, DyingHoverMaxTicks) + 1);
                bee.FadeTicks = Main.rand.Next(Math.Max(1, DyingFadeMinTicks), Math.Max(DyingFadeMinTicks, DyingFadeMaxTicks) + 1);
            }

            return true;
        }

        void UpdateOrphanBees()
        {
            if (!IsOnScreen(orphanCenter, VisualRange))
            {
                bees.Clear();
                return;
            }

            for (int i = bees.Count - 1; i >= 0; i--)
            {
                Bee bee = bees[i];
                bee.Age++;
                bee.Speed *= bee.Slow;
                bee.Base += bee.Velocity * bee.Speed;

                float sway = (float)Math.Sin(bee.Phase + bee.Age * 0.15f) * bee.SwayAmount * bee.Speed;
                bee.Pos = bee.Base + bee.Side * sway;

                bee.ApproachAlpha = Math.Min(1f, bee.ApproachAlpha + DyingBrightenPerTick);

                if (bee.Speed < DyingStopSpeed)
                    bee.StoppedFor++;

                if (bee.StoppedFor >= bee.HoverTicks + bee.FadeTicks)
                    bees.RemoveAt(i);
            }
        }

        // 0..1, the fade a bee gets over its last BeeFadeOutTiles before reaching the wheel.
        float ApproachFade(Bee bee)
        {
            float pixelsLeft = (1f - bee.Progress) * bee.Length;
            return MathHelper.Clamp(pixelsLeft / Math.Max(1f, BeeFadeOutTiles * 16f), 0f, 1f);
        }

        // ---- Honey and bees ----

        void UpdateHoney(bool running)
        {
            List<Point> hives = scanner.ExposedHives;
            if (!running || hives.Count == 0)
                return;

            int tries = Math.Min(MaxHoneyTries, 1 + hives.Count / Math.Max(1, HoneyTilesPerTry));
            for (int n = 0; n < tries; n++)
            {
                if (Main.rand.NextFloat() > HoneyDustChance)
                    continue;

                Point tile = hives[Main.rand.Next(hives.Count)];
                Vector2 position = new Vector2(tile.X * 16f, tile.Y * 16f);
                if (!IsOnScreen(position + new Vector2(8f), 40f))
                    continue;

                Dust dust = Dust.NewDustDirect(position, 16, 16, DustID.Honey, 0f, 0f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
                dust.velocity = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(0.2f, 0.7f));
            }
        }

        void UpdateBees(bool running)
        {
            Vector2 target = WheelCenter + new Vector2(0f,0f);

            for (int i = bees.Count - 1; i >= 0; i--)
            {
                Bee bee = bees[i];
                bee.Age++;
                bee.Progress += bee.Step;
                if (bee.Progress >= 1f)
                {
                    bees.RemoveAt(i);
                    continue;
                }

                Vector2 along = target - bee.Start;
                Vector2 side = along.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                float sway = (float)Math.Sin(bee.Phase + bee.Age * 0.15f) * bee.Wobble * (1f - bee.Progress);
                bee.Pos = Vector2.Lerp(bee.Start, target, bee.Progress) + side * sway;
            }

            List<Point> hives = scanner.ExposedHives;
            if (!running || hives.Count == 0)
                return;

            beeSpawnAccumulator = Math.Min(beeSpawnAccumulator + hives.Count * BeeSpawnPerHive, 5f);
            while (beeSpawnAccumulator >= 1f)
            {
                beeSpawnAccumulator -= 1f;
                if (bees.Count >= MaxBees)
                {
                    beeSpawnAccumulator = 0f;
                    break;
                }

                SpawnBee(hives, target);
            }
        }

        void SpawnBee(List<Point> hives, Vector2 target)
        {
            Point tile = hives[Main.rand.Next(hives.Count)];
            Vector2 start = new Vector2(tile.X * 16f + 8f, tile.Y * 16f + 8f) + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-8f, 8f));
            float length = Math.Max(16f, Vector2.Distance(start, target));

            float total = Math.Max(0f, BigBeeWeight) + Math.Max(0f, SmallBeeWeight);
            bool big = total > 0f && Main.rand.NextFloat() * total < Math.Max(0f, BigBeeWeight);

            bees.Add(new Bee
            {
                Start = start,
                Pos = start,
                Progress = 0f,
                Step = BeeSpeed / length,
                Length = length,
                Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                Wobble = BeeWobble * Main.rand.NextFloat(0.5f, 1f),
                Big = big,
                MovingRight = target.X > start.X
            });
        }

        void DrawBee(SpriteBatch spriteBatch, Bee bee)
        {
            if (!BeeWheelVisual.TryGetBeeTexture(bee.Big, CustomBeeFrames, out Texture2D texture, out int frames))
                return;

            int frameHeight = texture.Height / frames;
            int frame = (bee.Age / Math.Max(1, BeeFrameTicks)) % frames;
            Rectangle source = new Rectangle(0, frame * frameHeight, texture.Width, frameHeight);

            float alpha = MathHelper.Clamp(bee.Progress / Math.Max(0.01f, BeeFullOpacityAt), 0f, 1f);
            if (bee.Dying)
            {
                alpha *= bee.ApproachAlpha;
                alpha *= 1f - MathHelper.Clamp((bee.StoppedFor - bee.HoverTicks) / (float)Math.Max(1, bee.FadeTicks), 0f, 1f);
            }
            else
            {
                alpha *= ApproachFade(bee);
            }
            Color color = Lighting.GetColor(bee.Pos.ToTileCoordinates()) * alpha;
            SpriteEffects effects = bee.MovingRight == BeeSheetFacesLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            spriteBatch.Draw(texture, bee.Pos - Main.screenPosition, source, color, 0f, source.Size() / 2f, BeeScale, effects, 0f);
        }
    }
}