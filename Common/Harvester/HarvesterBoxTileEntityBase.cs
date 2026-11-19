using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Common.UI;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // The powered entity of a Harvester Box ROW: one entity for every box of the same kind touching left and right, at the row's leftmost box
    // (HarvesterBoxRows / HarvesterBoxRowSystem keep that true). It draws power and joins the grid through custom wire on ANY box of the row. The gears, the harvest
    // cycle and the drops come in later phases (docs/harvester_box/ROADMAP.md, 6c).
    public abstract class HarvesterBoxTileEntityBase<TTile> : ElectricConsumerMachine, IRowMachine, IMachineDynamicDraw where TTile : ModTile
    {
        public override int ValidTileType => ModContent.TileType<TTile>();

        public int RowFirstX => Position.X;

        public int RowLength => HarvesterBoxRows.RunLengthFrom(Position.X, Position.Y);

        public override float PowerDemand => 10f * System.Math.Max(1, RowLength);   // placeholder balance: 10 per box of the row

        public override void Update()
        {
            base.Update();
            isWorking = true;   // draws power the whole time it is placed (the Phase 2 gears turn whenever the box is powered)
        }

        // ---- Gears (Phase 2, docs/harvester_box/ROADMAP.md 6d) ----
        // Client-only visuals: nothing here is saved or sent. The server never runs DynamicUpdate / DynamicDraw.
        // One gear per box. A gear's direction comes ONLY from the parity of its world tile x (even turns one way, odd the other), so any two
        // touching gears mesh, whatever the row, kind or entity they belong to. Every powered gear locks to ONE clock (GearClock), so they stay in step.

        // Tunables (expression-bodied so hot reload picks changes up)
        public string GearTexturePath => "Factorraria/Content/Tiles/Machines/ElectricalConsumers/HarvesterBox/HarvesterBoxGear";   // idle sprite
        public string GearActiveTexturePath => GearTexturePath + "_Active";   // sprite once the plant above is ripe (Phase 4 turns it on; falls back to the idle one)
        public float GearSpeedDegreesPerTick => 3f;       // full speed, shared by every gear
        public float GearMeshOffsetDegrees => 90f;        // extra turn of the ODD-x gears so their teeth fall between the even-x gears' teeth
        public int GearTeeth => 4;                        // teeth on the sprite: gears settle on multiples of 360 / this, so neighbours always mesh
        public int GearSpinUpTicks => 30;                 // 0 to full speed
        public int GearSpinDownTicks => 30;               // full speed to 0
        public float GearSnapEase => 0.12f;               // 0..1 per tick, how fast a gear slides onto the shared clock / its rest angle
        public Vector2 GearOffset => Vector2.Zero;        // pixels from the middle of the box tile to the gear's centre
        public float GearScale => 1f;                     // the draw size is the texture size times this

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

            // At full speed slide onto the shared clock; stopped, settle on the nearest tooth step. Either way all gears end up in
            // matching orientations, so touching rows mesh. In between (spinning up or down) the gear is left alone.
            if (gearSpeed >= 1f)
                gearPhase = SnapPhase(gearPhase, clock);
            else if (gearSpeed <= 0f)
                gearPhase = SnapPhase(gearPhase, 0f);

            gearPhase = MathHelper.WrapAngle(gearPhase);
        }

        float SnapPhase(float phase, float origin)
        {
            float pitch = MathHelper.TwoPi / Math.Max(1, GearTeeth);
            float diff = MathHelper.WrapAngle(phase - origin);
            float snapped = (float)Math.Round(diff / pitch) * pitch;
            return origin + diff + (snapped - diff) * MathHelper.Clamp(GearSnapEase, 0f, 1f);
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
                spriteBatch.Draw(texture, center - Main.screenPosition, null, Lighting.GetColor(x, Position.Y), GearAngle(x), texture.Size() / 2f, GearScale, SpriteEffects.None, 0f);
            }
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