using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Content.Items.Liquids.Motors;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Liquids
{
    // Every motor variant (lead, iron, ...) inherits this, parameterized by its
    // own MotorTileEntity subtype. Handles placement-hook wiring and telling the
    // liquid network a topology-relevant tile appeared/disappeared — mirrors
    // PipeTileBase's role, just without the auto-shaping (motors don't reframe
    // based on neighbors, they're a fixed sprite rotated via right-click instead).
    public abstract class MotorTileBase<TEntity> : ModTile, IMotorTile where TEntity : MotorTileEntityBase, new()
    {
        // A motor only joins pipes of the SAME material (see PipeTierRegistry.CanLink).
        protected abstract PipeMaterial Material { get; }

        // Dust the motor throws when it breaks.
        protected virtual int MotorDustId => DustID.Lead;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileLighted[Type] = true;
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true; // platform-like top: things can drop through from below, stand on top
            Main.tileNoAttach[Type] = false;

            HitSound = SoundID.Tink;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<TEntity>().Hook_AfterPlacement, -1, 0, true);
            TileObjectData.newTile.AnchorInvalidTiles = new int[] { };

            // Prevent DivideByZeroException when external mods query tile style
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.StyleWrapLimit = 1;

            // Clear default floor requirement
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;

            // Lava: survives it (no break on contact, no obsidian kill) and can be placed inside it.
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newTile.LavaPlacement = LiquidPlacement.Allowed;
            Main.tileLavaDeath[Type] = false;
            Main.tileObsidianKill[Type] = false;

            // MUST be called after setting AnchorBottom
            TileObjectData.addTile(Type);

            PipeTierRegistry.RegisterMotor(Type, Material);
            if (!Main.dedServ)
                ModContent.RequestIfExists(OverdrawTexturePath, out overdrawSheet);

            ModContent.GetInstance<TileOverdrawSystem>().Register(Type, OverdrawTexturePath, SelectOverdrawCell, inFront: false, previewSelector: SelectPreviewCell, scaler: SelectOverdrawScale);
        }

        // LeadMotorTile -> .../LeadMotorOverdraw (PNG optional: a missing sheet just draws nothing).
        protected virtual string OverdrawTexturePath => Texture.EndsWith("Tile") ? Texture.Substring(0, Texture.Length - 4) + "Overdraw" : Texture + "_Overdraw";

        // The overhang art swells with the pump (PipeFlowVisualSystem owns the wave; the pump is its path coordinate 0).
        static Vector2 SelectOverdrawScale(int i, int j, Tile tile) => ModContent.GetInstance<PipeFlowVisualSystem>().GetTileScale(i, j, tile);

        // Placement hologram: a new motor is Off and faces the way the player last turned a motor (MotorTileEntityBase.LastPlacedFacing, Right until then).
        static bool SelectPreviewCell(int i, int j, Tile tile, out int cell, out float rotation)
        {
            cell = 0;
            rotation = MachineVisualOverride.GetMotorRotation(MotorTileEntityBase.LastPlacedFacing);
            return true;
        }

        Asset<Texture2D> overdrawSheet;

        // Cell 0 = Off, every cell after it is one ON frame (a sheet with a single ON cell does not animate).
        int OnOverdrawFrames()
        {
            Texture2D sheet = overdrawSheet?.Value;
            if (sheet == null)
                return 1;

            int cellSize = 16 + 2 * ModContent.GetInstance<TileOverdrawSystem>().DefaultMarginPx;
            return System.Math.Max(1, sheet.Width / cellSize * (sheet.Height / cellSize) - 1);
        }

        bool SelectOverdrawCell(int i, int j, Tile tile, out int cell, out float rotation)
        {
            cell = 0;
            rotation = 0f;
            if (!MachineVisualRegistry.Definitions.TryGetValue(tile.TileType, out MachineVisualDefinition def))
                return false;

            BaseMachine entity = def.ResolveEntity(i, j);
            if (entity == null)
                return false;

            cell = entity.IsRunning ? 1 + TileEntityHelper.GetAnimationFrame(new Point16(i, j), OnOverdrawFrames()) : 0;
            if (entity is MotorTileEntityBase motor)
                rotation = MachineVisualOverride.GetMotorRotation(motor.Facing);
            return true;
        }

        public override bool CanPlace(int i, int j)
        {
            // 1. Valid if background wall exists
            if (Main.tile[i, j].WallType > WallID.None)
                return true;

            // 2. Valid if attached to ANY tile (solid blocks, pipes, or motors)
            if (Main.tile[i - 1, j].HasTile ||
                Main.tile[i + 1, j].HasTile ||
                Main.tile[i, j - 1].HasTile ||
                Main.tile[i, j + 1].HasTile)
            {
                return true;
            }

            return false; // Prevents floating placement in mid-air
        }

        // NEW: this is the piece that was missing. A motor is a bridge point in the
        // liquid network's topology (unlike other machines, which are dead ends), so
        // placing/breaking one needs to invalidate the network the same way a pipe does.
        // Patch 3.3: the rebuild flags on placement are set by MotorPlacedConsumer on the server (Common/Net/TilePlacedConsumers.cs).
        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            LiquidNetworkSystem.networkNeedsRebuilding = true;
            PowerGridSystem.gridNeedsRebuilding = true;
        }

        public override bool CreateDust(int i, int j, ref int type)
        {
            type = MotorDustId;
            return true;
        }

        // Unhammerable: no slopes, no half blocks (the sprite and the flow logic assume a full tile).
        public override bool Slope(int i, int j)
        {
            return false;
        }
    }
}