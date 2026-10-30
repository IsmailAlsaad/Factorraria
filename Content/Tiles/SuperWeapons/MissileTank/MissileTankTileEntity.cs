using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Common.SuperWeapons;
using Factorraria.Content.Tiles.SuperWeapons.MissileDrill;
using Factorraria.Content.Tiles.SuperWeapons.MissileThruster;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileTank
{
    // The fuel tank of the missile (super weapons patch S2.2): a BaseMachine with no slots, no power and no recipes, only ONE input tank that
    // holds Fuel. It gets everything else from BaseMachine: pipes treat it as a sink (the tank is InputLiquids[0] with a Fuel-only filter),
    // bucket pour works through the same tank, and it is saved and synced like every machine (snapshot, light updates, detail channel).
    // Patch S3.1 adds the assembly check on this entity (see "Assembly" below); the primed state arrives in S3.2.
    public class MissileTankTileEntity : BaseMachine
    {
        public override int ValidTileType => ModContent.TileType<MissileTankTile>();

        // A liquid-fuel table that only knows Fuel is what makes the tank Fuel-only: it is the filter of InputLiquids[0].
        protected override LiquidFuelTable AcceptedLiquidFuels => MissileTankRegistry.LiquidFuels;

        // Tank size in liquid units; a bucket is 255, so this is about 19.6 buckets (open item O6).
        public float FuelCapacity = 5000f;

        protected override float InputTankCapacity(int tankIndex) => FuelCapacity;

        // ---- Assembly (patch S3.1) ----
        // AUTHORITY: the server (or single-player) re-reads the tiles around the tank every AssemblyCheckTicks and owns the result. A client
        // replica never computes it: it receives it in the snapshot (join) and the light updates (changes) and uses it for the debug overlay
        // (and, from S3.2, the UI). Nothing here is saved: it is recomputed from the tiles, so a reload always agrees with the world.
        // The rules live in the pure SuperWeaponAssembly (tested in tools/sw_tests); this class only reads tiles into it.

        /// <summary>Ticks between two checks of the world around the tank (a check reads about 30 tiles).</summary>
        public int AssemblyCheckTicks = 10;

        public SuperWeaponState WeaponState;   // Incomplete / Assembled from the tiles; Igniting / Launched are driven by SuperWeaponSystem (S4.1)
        public int WeaponFacing;               // +1 right, -1 left, 0 = no drill found yet
        public AssemblyIssue WeaponIssue;
        public int WeaponIssueRow;             // thruster issues: the tank row 0..2
        public int WeaponIssueDx;              // tile offset from the tank's top-left of the missing / offending piece
        public int WeaponIssueDy;

        public bool IsAssembled => WeaponState >= SuperWeaponState.Assembled;

        /// <summary>One line saying what is wrong with the weapon, or that it is complete.</summary>
        public string AssemblyText => SuperWeaponAssembly.Describe(IsAssembled, WeaponFacing, WeaponIssue, WeaponIssueRow);

        int assemblyTimer;   // runtime only: ticks until the next check, 0 = check on the next tick

        /// <summary>S4.1, server / single-player only, never saved or sent (clients only see the Igniting state): ticks left of the ignition countdown.</summary>
        public int IgnitionTicksLeft;

        /// <summary>True while the tank holds a full load of Fuel. Read on the server / single-player (the tank amount is the real one there).</summary>
        public bool IsFull => InputLiquids[0].Amount >= FuelCapacity - 0.01f;

        /// <summary>S4.1: the weapon can fire = assembled correctly (and not already igniting / launched) and the tank is full. There is no separate Primed state.</summary>
        public bool IsReadyToFire => WeaponState == SuperWeaponState.Assembled && IsFull;

        static readonly Func<int, int, PartCell> partReader = ReadPartCell;

        public override void Update()
        {
            base.Update();

            // Server / single-player only (multiplayer working rule 10): a client replica gets the result from the server.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (--assemblyTimer > 0)
                return;

            assemblyTimer = Math.Max(1, AssemblyCheckTicks);
            RefreshAssembly();
        }

        /// <summary>Re-reads the world around the tank now. Server / single-player only. The light update carries a change to the clients.</summary>
        public void RefreshAssembly()
        {
            AssemblyResult result = SuperWeaponAssembly.Evaluate(Position.X, Position.Y, partReader);

            // S4.1: Igniting and Launched belong to SuperWeaponSystem. While the weapon stays a valid assembly they are left alone; a removed
            // part drops the tank back to a normal state (and so aborts the countdown).
            if (WeaponState >= SuperWeaponState.Igniting)
            {
                if (result.Assembled)
                    return;

                IgnitionTicksLeft = 0;
            }

            SuperWeaponState state = result.Assembled ? SuperWeaponState.Assembled : SuperWeaponState.Incomplete;
            int dx = result.Assembled ? 0 : result.IssueX - Position.X;
            int dy = result.Assembled ? 0 : result.IssueY - Position.Y;

            if (state == WeaponState && result.Facing == WeaponFacing && result.Issue == WeaponIssue && result.IssueRow == WeaponIssueRow
                && dx == WeaponIssueDx && dy == WeaponIssueDy)
                return;

            WeaponState = state;
            WeaponFacing = result.Facing;
            WeaponIssue = result.Issue;
            WeaponIssueRow = result.IssueRow;
            WeaponIssueDx = dx;
            WeaponIssueDy = dy;
        }

        /// <summary>S4.1, server / single-player only: Assembled + full tank -> Igniting. Returns false (and does nothing) when the tank is not ready.</summary>
        public bool BeginIgnition(int ticks)
        {
            if (!IsReadyToFire)
                return false;

            WeaponState = SuperWeaponState.Igniting;
            IgnitionTicksLeft = Math.Max(1, ticks);
            return true;
        }

        /// <summary>S4.1, server / single-player only: Igniting -> Launched (S4.2 spawns the missile and removes the parts here).</summary>
        public void CompleteIgnition()
        {
            if (WeaponState != SuperWeaponState.Igniting)
                return;

            WeaponState = SuperWeaponState.Launched;
            IgnitionTicksLeft = 0;
        }

        /// <summary>S4.1, server / single-player only: leaves Igniting / Launched and goes back to the state the tiles say.</summary>
        public void ClearLaunch()
        {
            if (WeaponState < SuperWeaponState.Igniting)
                return;

            IgnitionTicksLeft = 0;
            WeaponState = SuperWeaponState.Incomplete;
            RefreshAssembly();
        }

        // What the world holds at one tile, for the pure check. The facing comes from the tile frame (style 0 = right, style 1 = left, see
        // OverhangDrawSystem.GetFacing); the drill's top-left tile is the one with frame (0, 0) inside its own style.
        static PartCell ReadPartCell(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return default;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile)
                return default;

            if (tile.TileType == ModContent.TileType<MissileDrillTile>())
            {
                bool origin = tile.TileFrameY == 0 && tile.TileFrameX % (SuperWeaponAssembly.DrillWidth * 18) == 0;
                return new PartCell(SuperWeaponPartKind.Drill, OverhangDrawSystem.GetFacing(tile, SuperWeaponAssembly.DrillWidth), origin);
            }

            if (tile.TileType == ModContent.TileType<MissileThrusterTile>())
                return new PartCell(SuperWeaponPartKind.Thruster, OverhangDrawSystem.GetFacing(tile, 1), true);

            return default;
        }

        // Six small bytes, written the same way for the snapshot and the light update (the light update only goes out when the bytes change).
        void WriteAssembly(BinaryWriter writer)
        {
            writer.Write((byte)WeaponState);
            writer.Write((sbyte)WeaponFacing);
            writer.Write((byte)WeaponIssue);
            writer.Write((byte)WeaponIssueRow);
            writer.Write((sbyte)WeaponIssueDx);
            writer.Write((sbyte)WeaponIssueDy);
        }

        void ReadAssembly(BinaryReader reader)
        {
            WeaponState = (SuperWeaponState)reader.ReadByte();
            WeaponFacing = reader.ReadSByte();
            WeaponIssue = (AssemblyIssue)reader.ReadByte();
            WeaponIssueRow = reader.ReadByte();
            WeaponIssueDx = reader.ReadSByte();
            WeaponIssueDy = reader.ReadSByte();
        }

        protected override void WriteSnapshotExtra(BinaryWriter writer)
        {
            base.WriteSnapshotExtra(writer);
            WriteAssembly(writer);
        }

        protected override void ReadSnapshotExtra(BinaryReader reader)
        {
            base.ReadSnapshotExtra(reader);
            ReadAssembly(reader);
        }

        protected override void WriteLightExtra(BinaryWriter writer)
        {
            base.WriteLightExtra(writer);
            WriteAssembly(writer);
        }

        protected override void ReadLightExtra(BinaryReader reader)
        {
            base.ReadLightExtra(reader);
            ReadAssembly(reader);
        }

        protected override string DescribeSnapshotExtra() => base.DescribeSnapshotExtra() + " weapon=" + WeaponState + " facing=" + WeaponFacing + " issue=" + WeaponIssue;
    }
}