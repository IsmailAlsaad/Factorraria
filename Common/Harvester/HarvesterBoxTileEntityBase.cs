using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Common.Systems;
using Factorraria.Common.UI;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // The powered entity of a Harvester Box ROW: one entity for every box of the same kind touching left and right, at the row's leftmost box
    // (HarvesterBoxRows / HarvesterBoxRowSystem keep that true). It draws power and joins the grid through custom wire on ANY box of the row. The gears, the harvest
    // cycle and the drops come in later phases (docs/harvester_box/ROADMAP.md, 6c).
    public abstract class HarvesterBoxTileEntityBase<TTile> : ElectricConsumerMachine, IRowMachine, IMachineDynamicDraw, IDropCaptureTarget, IHarvesterBoxRow where TTile : ModTile
    {
        public override int ValidTileType => ModContent.TileType<TTile>();

        public int RowFirstX => Position.X;

        public int RowLength => HarvesterBoxRows.RunLengthFrom(Position.X, Position.Y);

        public override float PowerDemand => 10f * System.Math.Max(1, RowLength);   // placeholder balance: 10 per box of the row

        public override void Update()
        {
            base.Update();
            isWorking = true;   // draws power the whole time it is placed (the Phase 2 gears turn whenever the box is powered)
            UpdateHarvest();
        }

        // ---- Gears (Phase 2, docs/harvester_box/ROADMAP.md 6d) ----
        // Client-only visuals: nothing here is saved or sent. The server never runs DynamicUpdate / DynamicDraw.
        // One gear per box. A gear's direction comes ONLY from the parity of its world tile x (even turns one way, odd the other), so any two
        // touching gears mesh, whatever the row, kind or entity they belong to. Every powered gear locks to ONE clock (GearClock), so they stay in step.

        // Tunables (expression-bodied so hot reload picks changes up)
        public string GearTexturePath => "Factorraria/Content/Tiles/Machines/ElectricalConsumers/HarvesterBox/HarvesterBoxGear";   // idle sprite
        public string GearActiveTexturePath => GearTexturePath + "_Active";   // sprite once the plant above is ripe (Phase 4 turns it on; falls back to the idle one)
        public float GearSpeedDegreesPerTick => 3f;       // full speed, shared by every gear
        public float GearMeshOffsetDegrees => 45f;        // extra turn of the ODD-x gears so their teeth fall between the even-x gears' teeth
        public int GearTeeth => 4;                        // teeth on the sprite: gears settle on multiples of 360 / this, so neighbours always mesh
        public int GearSpinUpTicks => 30;                 // 0 to full speed
        public int GearSpinDownTicks => 30;               // full speed to 0
        public float GearSyncEase => 0f;                  // 0 = a gear never gets nudged (stops wherever it stops, no startup hiccup). Above 0 (try 0.005) a full-speed gear slowly slides onto the shared clock so rows started at different times mesh with each other
        public Vector2 GearOffset => new Vector2(0f,4f);        // pixels from the middle of the box tile to the gear's centre
        public float GearScale => 0.925f;                     // the draw size is the texture size times this

        bool gearInitialized;
        float gearSpeed;       // 0 (stopped) .. 1 (full speed)
        float gearPhase;       // radians, the row's gear angle before the parity direction is applied
        bool gearAssetsTried;
        Asset<Texture2D> gearIdleAsset;
        Asset<Texture2D> gearActiveAsset;

        // The one shared clock: a pure function of the game tick, so every gear on this client agrees without any state or packet.
        float GearClock => (float)((Main.GameUpdateCount * (double)MathHelper.ToRadians(GearSpeedDegreesPerTick)) % MathHelper.TwoPi);

        // The active gear sprite shows from the moment the plant above is ripe until the strike is over (Ready and Strike).
        public virtual bool IsGearActive(int boxIndex) => boxIndex >= 0 && boxIndex < boxes.Length && boxes[boxIndex].Cycle != BoxCycle.Idle;

        // x is the WORLD tile x of the box.
        float GearAngle(int x)
        {
            return (x & 1) == 0 ? gearPhase : -gearPhase + MathHelper.ToRadians(GearMeshOffsetDegrees);
        }

        public void DynamicUpdate()
        {
            UpdateStrikeVisuals();

            float clock = GearClock;
            float step = MathHelper.ToRadians(GearSpeedDegreesPerTick);

            if (!gearInitialized)
            {
                gearInitialized = true;
                gearSpeed = isOn ? 1f : 0f;   // no spin-up blip when the entity is rebuilt after a row change
                gearPhase = clock;
            }

            float target = isOn ? 1f : 0f;
            float rate = 1f / Math.Max(1, isOn ? GearSpinUpTicks : GearSpinDownTicks);
            gearSpeed = gearSpeed < target ? Math.Min(target, gearSpeed + rate) : Math.Max(target, gearSpeed - rate);

            gearPhase += gearSpeed * step;

            // A gear slows down and stops wherever it is, and speeds up from there: nothing pulls it anywhere. Only when GearSyncEase is above 0
            // does a full-speed gear slowly slide onto the shared clock (so separately started rows mesh with each other).
            if (gearSpeed >= 1f && GearSyncEase > 0f)
                gearPhase = SnapPhase(gearPhase, clock);

            gearPhase = MathHelper.WrapAngle(gearPhase);
        }

        float SnapPhase(float phase, float origin)
        {
            float pitch = MathHelper.TwoPi / Math.Max(1, GearTeeth);
            float diff = MathHelper.WrapAngle(phase - origin);
            float snapped = (float)Math.Round(diff / pitch) * pitch;
            return origin + diff + (snapped - diff) * MathHelper.Clamp(GearSyncEase, 0f, 1f);
        }

        public bool TryGetDynamicBounds(out Vector2 center, out float radius)
        {
            int length = RowLength;
            center = new Vector2((Position.X + length * 0.5f) * 16f, (Position.Y + 0.5f) * 16f);
            radius = length * 8f + 64f;
            return length > 0;
        }

        bool TryGetGearTextures(out Texture2D idle, out Texture2D active)
        {
            if (!gearAssetsTried)
            {
                gearAssetsTried = true;
                ModContent.RequestIfExists<Texture2D>(GearTexturePath, out gearIdleAsset, AssetRequestMode.ImmediateLoad);
                ModContent.RequestIfExists<Texture2D>(GearActiveTexturePath, out gearActiveAsset, AssetRequestMode.ImmediateLoad);
            }

            idle = gearIdleAsset?.Value;
            active = gearActiveAsset?.Value ?? idle;   // until the active sprite exists, the idle one stands in
            return idle != null;
        }

        // In front of the tiles (and so in front of the plant above the box). Staffs first, so each one seems to grow out from under its gear.
        // No sprite yet: nothing is drawn.
        public void DynamicDraw(SpriteBatch spriteBatch)
        {
            DrawStaffs(spriteBatch);

            if (!gearInitialized || !TryGetGearTextures(out Texture2D idle, out Texture2D active))
                return;

            int length = RowLength;
            for (int k = 0; k < length; k++)
            {
                int x = Position.X + k;
                Vector2 center = new Vector2(x * 16f + 8f, Position.Y * 16f + 8f) + GearOffset;
                Color light = Lighting.GetColor(x, Position.Y);
                float angle = GearAngle(x);
                float blend = k < gearBlend.Length ? gearBlend[k] : 0f;

                if (blend <= 0f || ReferenceEquals(active, idle))
                {
                    DrawGear(spriteBatch, idle, center, light, angle);   // the draw size comes from the texture itself
                    continue;
                }

                // Crossfade: the active sprite fades in over the opaque idle one (first half), then the idle one fades out under it (second half),
                // so the gear never turns see-through in the middle.
                float activeAlpha = Math.Min(1f, 2f * blend);
                float idleAlpha = 1f - Math.Max(0f, 2f * blend - 1f);
                if (idleAlpha > 0f)
                    DrawGear(spriteBatch, idle, center, light * idleAlpha, angle);

                DrawGear(spriteBatch, active, center, light * activeAlpha, angle);
            }
        }

        void DrawGear(SpriteBatch spriteBatch, Texture2D texture, Vector2 center, Color color, float angle)
        {
            spriteBatch.Draw(texture, center - Main.screenPosition, null, color, angle, texture.Size() / 2f, GearScale, SpriteEffects.None, 0f);
        }

        // ---- Herb harvest and staff (Phase 3 and 4, docs/harvester_box/ROADMAP.md 6e and 6f) ----
        // The server and single-player run the cycle of every box: Idle -> Ready (plant ripe) -> Strike (the staff extends, sweeps over the plant,
        // retracts; the herb is harvested and replanted at the middle of the sweep) -> Idle. The per-box state is an array indexed by x - Position.X,
        // rebuilt from the row length and never saved: a ripe plant is found again by the next scan, so a reset only restarts its delay.
        // A multiplayer client keeps the same array only for the visuals: it works out Ready from its own tiles, and starts a Strike when the server's
        // HarvesterBoxStrike packet arrives, then replays it with the same tick counts. HarvestHerb stays the one place that harvests.

        // Tunables (expression-bodied so hot reload picks changes up)
        public int HarvestScanTicks => 20;          // how often an idle box looks at the plant above it
        public int HarvestDelayTicks => 30;         // powered ticks between "plant is ripe" and the start of the strike
        public int StaffExtendTicks => 20;          // the staff grows out of the gear
        public int StaffSweepTicks => 40;           // it turns at the gear's speed; the tip passes straight over the plant at the middle of this
        public int StaffRetractTicks => 20;         // it shrinks back into the gear (it keeps turning)
        public int StaffAbortMarginTicks => 3;      // a client keeps its strike this long past a vanished plant (the server's tile message can come a tick early)
        public string StaffTexturePath => "Factorraria/Content/Tiles/Machines/ElectricalConsumers/HarvesterBox/HarvesterStaffOfRegrowth";   // the sprite points diagonally to the top right, its pivot is the bottom left corner
        public Vector2 StaffOffset => Vector2.Zero;               // pixels from the gear's centre to the staff's pivot (world offset, not rotated)
        public Vector2 StaffPivotOffset => Vector2.Zero;          // moves the pivot inside the sprite, in texture pixels from the bottom left corner (x right, y down: (2, -1) = 2 px right of and 1 px above the corner)
        public float StaffSpriteRotationDegrees => -45f;          // turns the sprite so it points straight up at angle 0 (-45 for a sprite pointing to the top right)
        public float StaffScale => 1f;
        public float StaffDustChance => 0.35f;                    // per tick per moving staff, only while its tip is on screen
        public int StaffDustType => 2;                            // vanilla grass dust (DustID.Grass)
        public int GearActiveFadeTicks => 12;                     // the gear crossfades between its idle and active sprite this fast

        enum BoxCycle : byte { Idle, Ready, Strike }

        struct BoxState
        {
            public BoxCycle Cycle;
            public int Ticks;   // Ready: powered ticks waited. Strike: powered ticks since the staff started.
        }

        public int TreeGrowTicks => 600;            // powered ticks between two growth attempts of a sapling above a box (an attempt can still fail on space, like vanilla)

        BoxState[] boxes = Array.Empty<BoxState>();
        int[] saplingTicks = Array.Empty<int>();    // per box, powered ticks a sapling has waited (never saved)
        float[] gearBlend = Array.Empty<float>();   // per box, 0 = idle sprite .. 1 = active sprite (client visual only)
        int nextScanIn;
        int clientScanIn;
        Point dropTile;     // the herb tile being harvested right now (RouteDrop reads it)

        int StrikeCrossTick => Math.Max(1, StaffExtendTicks) + Math.Max(1, StaffSweepTicks) / 2;   // the tip is over the plant now
        int StrikeTotalTicks => Math.Max(1, StaffExtendTicks) + Math.Max(1, StaffSweepTicks) + Math.Max(1, StaffRetractTicks);

        void EnsureBoxes(int length)
        {
            if (boxes.Length != length)
                boxes = new BoxState[length];

            if (gearBlend.Length != length)
                gearBlend = new float[length];

            if (saplingTicks.Length != length)
                saplingTicks = new int[length];
        }

        // Server and single-player (a multiplayer client never runs Update).
        void UpdateHarvest()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !isOn)
                return;   // browned out or switched off: the whole cycle freezes (every count is in powered ticks)

            int length = RowLength;
            if (length <= 0)
                return;

            EnsureBoxes(length);

            bool scan = --nextScanIn <= 0;
            if (scan)
                nextScanIn = Math.Max(1, HarvestScanTicks);

            int y = Position.Y - 1;   // the plant sits on the tile above the box
            for (int k = 0; k < length; k++)
            {
                int x = Position.X + k;

                switch (boxes[k].Cycle)
                {
                    case BoxCycle.Idle:
                        if (scan && HarvesterBoxRules.IsReadyToHarvest(x, y, ValidTileType))
                            boxes[k] = new BoxState { Cycle = BoxCycle.Ready, Ticks = 0 };
                        else
                            UpdateSapling(k, x, y);
                        break;

                    case BoxCycle.Ready:
                        if (!HarvesterBoxRules.IsReadyToHarvest(x, y, ValidTileType))
                        {
                            boxes[k] = default;   // the plant was removed or changed while waiting
                            break;
                        }

                        if (++boxes[k].Ticks < HarvestDelayTicks)
                            break;

                        if (!CanDeliverDrops(x, y))
                        {
                            boxes[k].Ticks = HarvestDelayTicks;   // a full belt next to the plant: wait here until it has room (back-pressure, like the arm)
                            break;
                        }

                        boxes[k] = new BoxState { Cycle = BoxCycle.Strike, Ticks = 0 };
                        ModContent.GetInstance<HarvesterBoxNetSystem>().AnnounceStrike(x, Position.Y);
                        break;

                    case BoxCycle.Strike:
                        int before = boxes[k].Ticks;
                        int cross = StrikeCrossTick;

                        if (before < cross && !HarvesterBoxRules.IsReadyToHarvest(x, y, ValidTileType))
                        {
                            boxes[k] = default;   // the plant vanished before the staff got there
                            break;
                        }

                        boxes[k].Ticks = before + 1;

                        if (before < cross && boxes[k].Ticks >= cross)
                            HarvestHerb(x, y);    // the tip is over the plant

                        if (boxes[k].Ticks >= StrikeTotalTicks)
                            boxes[k] = default;
                        break;
                }
            }
        }

        // Called from DynamicUpdate (not on a dedicated server): a multiplayer client's copy of the cycle, then the staff dust.
        void UpdateStrikeVisuals()
        {
            int length = RowLength;
            if (length <= 0)
                return;

            EnsureBoxes(length);

            float fadeStep = 1f / Math.Max(1, GearActiveFadeTicks);
            for (int k = 0; k < length; k++)
            {
                float goal = IsGearActive(k) ? 1f : 0f;
                gearBlend[k] = gearBlend[k] < goal ? Math.Min(goal, gearBlend[k] + fadeStep) : Math.Max(goal, gearBlend[k] - fadeStep);
            }

            if (Main.netMode == NetmodeID.MultiplayerClient && isOn)
            {
                bool scan = --clientScanIn <= 0;
                if (scan)
                    clientScanIn = Math.Max(1, HarvestScanTicks);

                int y = Position.Y - 1;
                for (int k = 0; k < length; k++)
                {
                    int x = Position.X + k;
                    bool ripe;

                    switch (boxes[k].Cycle)
                    {
                        case BoxCycle.Idle:
                            if (scan && HarvesterBoxRules.IsReadyToHarvest(x, y, ValidTileType))
                                boxes[k] = new BoxState { Cycle = BoxCycle.Ready, Ticks = 0 };
                            break;

                        case BoxCycle.Ready:
                            // The server starts the strike (BeginClientStrike); until then the gear is active only while the plant stays ripe.
                            if (!HarvesterBoxRules.IsReadyToHarvest(x, y, ValidTileType))
                                boxes[k] = default;
                            break;

                        case BoxCycle.Strike:
                            boxes[k].Ticks++;
                            ripe = HarvesterBoxRules.IsReadyToHarvest(x, y, ValidTileType);
                            if ((boxes[k].Ticks < StrikeCrossTick - StaffAbortMarginTicks && !ripe) || boxes[k].Ticks >= StrikeTotalTicks)
                                boxes[k] = default;
                            break;
                    }
                }
            }

            SpawnStaffDust(length);
        }

        // Multiplayer client: the server's HarvesterBoxStrike packet (see HarvesterBoxNetSystem) for the box at world tile x.
        public void BeginClientStrike(int tileX)
        {
            int length = RowLength;
            int k = tileX - Position.X;
            if (k < 0 || k >= length)
                return;

            EnsureBoxes(length);
            boxes[k] = new BoxState { Cycle = BoxCycle.Strike, Ticks = 0 };
        }

        // ---- Staff drawing ----
        bool staffAssetTried;
        Asset<Texture2D> staffAsset;

        bool TryGetStaffTexture(out Texture2D texture)
        {
            if (!staffAssetTried)
            {
                staffAssetTried = true;
                ModContent.RequestIfExists<Texture2D>(StaffTexturePath, out staffAsset, AssetRequestMode.ImmediateLoad);
            }

            texture = staffAsset?.Value;
            return texture != null;
        }

        // 0..1: how much of the staff is out. Grows while extending, full during the sweep, shrinks while retracting.
        float StaffGrowth(int ticks)
        {
            int extend = Math.Max(1, StaffExtendTicks);
            int sweep = Math.Max(1, StaffSweepTicks);
            int retract = Math.Max(1, StaffRetractTicks);

            if (ticks < extend)
                return ticks / (float)extend;

            if (ticks < extend + sweep)
                return 1f;

            return MathHelper.Clamp(1f - (ticks - extend - sweep) / (float)retract, 0f, 1f);
        }

        // Radians, 0 = pointing up. Turns at the gear's own speed and in the gear's direction (x parity), and is exactly up at the cross tick.
        float StaffAngle(int x, int ticks)
        {
            float direction = (x & 1) == 0 ? 1f : -1f;
            return direction * MathHelper.ToRadians(GearSpeedDegreesPerTick) * (ticks - StrikeCrossTick);
        }

        Vector2 StaffBase(int x) => new Vector2(x * 16f + 8f, Position.Y * 16f + 8f) + GearOffset + StaffOffset;

        void DrawStaffs(SpriteBatch spriteBatch)
        {
            int count = Math.Min(RowLength, boxes.Length);
            Texture2D texture = null;

            for (int k = 0; k < count; k++)
            {
                if (boxes[k].Cycle != BoxCycle.Strike)
                    continue;

                if (texture == null && !TryGetStaffTexture(out texture))
                    return;   // no staff sprite yet

                float growth = StaffGrowth(boxes[k].Ticks);
                if (growth <= 0f)
                    continue;

                // The whole sprite is scaled up from its pivot, so it grows along the direction it faces.
                int x = Position.X + k;
                float angle = StaffAngle(x, boxes[k].Ticks) + MathHelper.ToRadians(StaffSpriteRotationDegrees);
                Vector2 origin = new Vector2(0f, texture.Height) + StaffPivotOffset;   // bottom left corner of the sprite, plus the tuning offset
                spriteBatch.Draw(texture, StaffBase(x) - Main.screenPosition, null, Lighting.GetColor(x, Position.Y), angle, origin, StaffScale * growth, SpriteEffects.None, 0f);
            }
        }

        void SpawnStaffDust(int length)
        {
            if (Main.dedServ || StaffDustChance <= 0f || !TryGetStaffTexture(out Texture2D texture))
                return;

            int count = Math.Min(length, boxes.Length);
            for (int k = 0; k < count; k++)
            {
                if (boxes[k].Cycle != BoxCycle.Strike)
                    continue;

                int ticks = boxes[k].Ticks;
                float growth = StaffGrowth(ticks);
                if (growth <= 0f || Main.rand.NextFloat() >= StaffDustChance)
                    continue;

                // Anywhere along the staff: from the pivot to the top right corner of the sprite, turned and scaled like the sprite itself.
                int x = Position.X + k;
                float angle = StaffAngle(x, ticks) + MathHelper.ToRadians(StaffSpriteRotationDegrees);
                Vector2 pivot = new Vector2(0f, texture.Height) + StaffPivotOffset;
                Vector2 along = (new Vector2(texture.Width, 0f) - pivot).RotatedBy(angle) * (StaffScale * growth);
                Vector2 position = StaffBase(x) + along * Main.rand.NextFloat() + Main.rand.NextVector2Circular(2f, 2f);
                if (!IsOnScreen(position))
                    continue;

                Dust.NewDustPerfect(position, StaffDustType, Main.rand.NextVector2Circular(1.2f, 1.2f), 0, default, 1f);
            }
        }

        // Harvests the herb at x, y exactly like breaking it (vanilla yield for its stage, drops captured and routed by RouteDrop), then replants
        // the same herb for free. Runs on the server / in single-player; tile changes are sent to clients here.
        void HarvestHerb(int x, int y)
        {
            if (Main.tile[x, y].TileType == TileID.Trees)
            {
                FellTree(x, y);
                return;
            }

            int style = Main.tile[x, y].TileFrameX / 18;
            dropTile = new Point(x, y);

            ModContent.GetInstance<RoboticArmDropCaptureSystem>().Run(this, () => WorldGen.KillTile(x, y, false, false, false));

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, x, y);

            WorldGen.PlaceAlch(x, y, style);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x, y);
        }

        // Phase 5. A sapling above the box makes a growth attempt every TreeGrowTicks powered ticks (server and single-player; the tree's tiles reach clients
        // through vanilla's own tile messages). Everything else about growing is in HarvesterBoxTrees.
        void UpdateSapling(int k, int x, int y)
        {
            if (!HarvesterBoxTrees.IsSapling(x, y))
            {
                saplingTicks[k] = 0;
                return;
            }

            if (++saplingTicks[k] < Math.Max(1, TreeGrowTicks))
                return;

            saplingTicks[k] = 0;
            HarvesterBoxTrees.TryGrowSapling(x, y);
        }

        // Fells the tree above the box like two axe chops (the second trunk tile first, so the whole tree above falls while the stump still lets vanilla
        // read the soil, then the stump), with the box standing in for its vanilla soil so the wood, the acorns and the bonus wood are the vanilla ones for that
        // soil. The drops go through the same capture and routing as a herb. A new sapling is planted for free. Server and single-player.
        void FellTree(int x, int y)
        {
            dropTile = new Point(x, y);
            int soil = HarvesterBoxRegistry.BoxTreeSoil.TryGetValue(Main.tile[x, y + 1].TileType, out int s) ? s : TileID.Grass;

            HarvesterBoxTrees.RunAsTreeSoil(x, y + 1, () => ModContent.GetInstance<RoboticArmDropCaptureSystem>().Run(this, () =>
            {
                WorldGen.KillTile(x, y - 1, false, false, false);

                if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Trees)
                    WorldGen.KillTile(x, y, false, false, false);
            }));

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x - 3, y - 24, 7, 25);   // the whole fallen tree, so no client keeps a piece of it

            if (HarvesterBoxTrees.CanPlantSapling(x, y))
                HarvesterBoxTrees.PlaceSapling(x, y, HarvesterBoxKinds.SaplingStyle(soil));
        }

        // The three tiles beside and above the plant: where a conveyor can take the drops, or a vItem can wait.
        IEnumerable<Point> DropNeighbours(int x, int y)
        {
            yield return new Point(x, y - 1);
            yield return new Point(x - 1, y);
            yield return new Point(x + 1, y);
        }

        bool HasConveyorAround(int x, int y)
        {
            foreach (Point t in DropNeighbours(x, y))
                if (VirtualItemSystem.IsConveyorTile(t.X, t.Y, out _, out _))
                    return true;

            return false;
        }

        bool TryFindFreeConveyorTile(int x, int y, out Point tile)
        {
            foreach (Point t in DropNeighbours(x, y))
            {
                if (VirtualItemSystem.IsConveyorTile(t.X, t.Y, out _, out _) && !VirtualItemSystem.IsTileOccupied(t.X, t.Y))
                {
                    tile = t;
                    return true;
                }
            }

            tile = default;
            return false;
        }

        // A free non-solid neighbour: a vItem put there falls to the floor and waits to be picked up.
        bool TryFindFreeGroundTile(int x, int y, out Point tile)
        {
            foreach (Point t in DropNeighbours(x, y))
            {
                if (!WorldGen.InWorld(t.X, t.Y))
                    continue;

                Tile neighbour = Main.tile[t.X, t.Y];
                if (neighbour.HasTile && Main.tileSolid[neighbour.TileType])
                    continue;

                if (VirtualItemSystem.IsTileOccupied(t.X, t.Y))
                    continue;

                tile = t;
                return true;
            }

            tile = default;
            return false;
        }

        // With a conveyor beside the plant a full belt pauses the harvest. Without one nothing can clog and the box never waits.
        bool CanDeliverDrops(int x, int y) => !HasConveyorAround(x, y) || TryFindFreeConveyorTile(x, y, out _);

        // Called by the shared drop capture for every item the harvest spawns (IDropCaptureTarget). Same rules as the Robotic Arm:
        //  - a conveyor beside the plant: onto a free conveyor tile as a vItem;
        //  - no conveyor and no player near (and not on this screen): onto a free neighbouring tile as a vItem, so unseen drops do not eat the world item cap;
        //  - no conveyor and someone is near: left alone, so it drops into the world like vanilla.
        public void RouteDrop(Item item)
        {
            if (item.IsAir)
                return;

            Point tile;
            bool found;

            if (HasConveyorAround(dropTile.X, dropTile.Y))
                found = TryFindFreeConveyorTile(dropTile.X, dropTile.Y, out tile);
            else if (!IsAnyPlayerNear(new Vector2(dropTile.X * 16f + 8f, dropTile.Y * 16f + 8f)) && !IsOnScreen(new Vector2(dropTile.X * 16f + 8f, dropTile.Y * 16f + 8f)))
                found = TryFindFreeGroundTile(dropTile.X, dropTile.Y, out tile);
            else
                return;

            if (!found)
                return;

            VirtualItem converted = VirtualItemSystem.SpawnVirtualItem(item.Clone(), tile.X, tile.Y);
            if (converted == null)
                return;

            item.TurnToAir();
            item.active = false;
        }

        // Single-player and server run this when the placing player is the one running the game (a multiplayer client only sends its request).
        // The row's entity is sorted out one tick later by HarvesterBoxRowSystem (joins, a new left end).
        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            int id = base.Hook_AfterPlacement(i, j, type, style, direction, alternate);
            HarvesterBoxRowSystem.MarkDirty(i, j);
            return id;
        }

        // Server: a client asked for an entity for the box it placed (message 87). A row has one entity: when this box joined a row that
        // already has one, nothing is created here; the reconcile moves or merges it if the new box became the left end.
        public override void NetPlaceEntityAttempt(int i, int j)
        {
            HarvesterBoxRowSystem.MarkDirty(i, j);

            if (HarvesterBoxRows.TryFindRowEntity(i, j, out _, out _))
                return;

            base.NetPlaceEntityAttempt(i, j);
        }

        // The entity sits at the row's first box. The panel always opens in the MIDDLE of the row, whichever box was clicked: on the middle box
        // of an odd row, and for an even row on its left-middle box shifted half a tile right, so the icon sits between the two middle boxes.
        // Every box of the row therefore gives the same panel position, so clicking any box of an open row closes it again.
        public override void OnRightClick(int i, int j)
        {
            if (!MachineUIRegistry.Definitions.TryGetValue(ValidTileType, out MachineUIStateBase state))
                return;

            int length = Math.Max(1, RowLength);
            if (state is HarvesterBoxUIState boxState)
                boxState.PanelShiftX = length % 2 == 0 ? 8f : 0f;

            ModContent.GetInstance<MachineUISystem>().OpenUI(i + (length - 1) / 2, j, ValidTileType, this);
        }
    }
}