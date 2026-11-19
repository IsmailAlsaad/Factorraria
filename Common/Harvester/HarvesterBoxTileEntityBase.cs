using Factorraria.Common.Machines;
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
    public abstract class HarvesterBoxTileEntityBase<TTile> : ElectricConsumerMachine, IRowMachine, IMachineDynamicDraw, IDropCaptureTarget where TTile : ModTile
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

        // Phase 4 turns this on for the boxes whose plant is ripe.
        public virtual bool IsGearActive(int boxIndex) => false;

        // x is the WORLD tile x of the box.
        float GearAngle(int x)
        {
            return (x & 1) == 0 ? gearPhase : -gearPhase + MathHelper.ToRadians(GearMeshOffsetDegrees);
        }

        public void DynamicUpdate()
        {
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

        // In front of the tiles (and so in front of the plant above the box). No gear sprite yet: nothing is drawn.
        public void DynamicDraw(SpriteBatch spriteBatch)
        {
            if (!gearInitialized || !TryGetGearTextures(out Texture2D idle, out Texture2D active))
                return;

            int length = RowLength;
            for (int k = 0; k < length; k++)
            {
                int x = Position.X + k;
                Texture2D texture = IsGearActive(k) ? active : idle;   // the draw size comes from the texture itself
                Vector2 center = new Vector2(x * 16f + 8f, Position.Y * 16f + 8f) + GearOffset;
                spriteBatch.Draw(texture, center - Main.screenPosition, null, Lighting.GetColor(x, Position.Y) * 1f, GearAngle(x), texture.Size() / 2f, GearScale, SpriteEffects.None, 0f);
            }
        }

        // ---- Herb harvest (Phase 3, docs/harvester_box/ROADMAP.md 6e) ----
        // Server and single-player only (a multiplayer client never runs Update). The per-box state is an array indexed by x - Position.X,
        // rebuilt from the row length, never saved: a ripe plant is found again by the next scan, so a reset only restarts its delay.
        // Phase 4 replaces the plain delay with the staff's Extending / Sweeping / Retracting steps; HarvestHerb stays the one place that harvests.

        // Tunables (expression-bodied so hot reload picks changes up)
        public int HarvestScanTicks => 20;          // how often the row looks at the plant above each idle box
        public int HarvestDelayTicks => 60;         // powered ticks between "plant is ripe" and the harvest (stand-in for the staff sweep)

        enum BoxCycle : byte { Idle, Ready }

        struct BoxState
        {
            public BoxCycle Cycle;
            public int Ticks;   // powered ticks spent in Ready
        }

        BoxState[] boxes = Array.Empty<BoxState>();
        int nextScanIn;
        Point dropTile;     // the herb tile being harvested right now (RouteDrop reads it)

        void UpdateHarvest()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !isOn)
                return;   // browned out or switched off: the whole cycle freezes (delays only count powered ticks)

            int length = RowLength;
            if (length <= 0)
                return;

            if (boxes.Length != length)
                boxes = new BoxState[length];

            bool scan = --nextScanIn <= 0;
            if (scan)
                nextScanIn = Math.Max(1, HarvestScanTicks);

            int y = Position.Y - 1;   // the plant sits on the tile above the box
            for (int k = 0; k < length; k++)
            {
                int x = Position.X + k;

                if (boxes[k].Cycle == BoxCycle.Idle)
                {
                    if (scan && HarvesterBoxRules.IsReadyToHarvest(x, y, ValidTileType))
                        boxes[k] = new BoxState { Cycle = BoxCycle.Ready, Ticks = 0 };

                    continue;
                }

                if (!HarvesterBoxRules.IsReadyToHarvest(x, y, ValidTileType))
                {
                    boxes[k] = default;   // the plant was removed or changed while waiting
                    continue;
                }

                if (++boxes[k].Ticks < HarvestDelayTicks)
                    continue;

                if (!CanDeliverDrops(x, y))
                {
                    boxes[k].Ticks = HarvestDelayTicks;   // a full belt next to the plant: wait here until it has room (back-pressure, like the arm)
                    continue;
                }

                HarvestHerb(x, y);
                boxes[k] = default;
            }
        }

        // Harvests the herb at x, y exactly like breaking it (vanilla yield for its stage, drops captured and routed by RouteDrop), then replants
        // the same herb for free. Runs on the server / in single-player; tile changes are sent to clients here.
        void HarvestHerb(int x, int y)
        {
            int style = Main.tile[x, y].TileFrameX / 18;
            dropTile = new Point(x, y);

            ModContent.GetInstance<RoboticArmDropCaptureSystem>().Run(this, () => WorldGen.KillTile(x, y, false, false, false));

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, x, y);

            WorldGen.PlaceAlch(x, y, style);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x, y);
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