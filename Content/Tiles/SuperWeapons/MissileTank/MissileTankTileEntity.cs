using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Common.SuperWeapons;
using Factorraria.Content.Tiles.SuperWeapons.MissileDrill;
using Factorraria.Content.Tiles.SuperWeapons.MissileThruster;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileTank
{
    // The fuel tank of the missile (super weapons patch S2.2): a BaseMachine with no slots, no power and no recipes, only ONE input tank that
    // holds Fuel. It gets everything else from BaseMachine: pipes treat it as a sink (the tank is InputLiquids[0] with a Fuel-only filter),
    // bucket pour works through the same tank, and it is saved and synced like every machine (snapshot, light updates, detail channel).
    // Patch S3.1 adds the assembly check on this entity (see "Assembly" below); the primed state arrives in S3.2.
    public class MissileTankTileEntity : BaseMachine, IMachineDynamicDraw
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

        // ---- Fuel window (client only, cosmetic, nothing saved or sent) ----
        // MissileTank.png has a see-through window in the middle of the casing. The liquid is drawn BEHIND the tiles (IMachineDynamicDraw's
        // behind-tiles pass), so the casing art covers everything except the hole. The level follows the tank amount, read from the replica
        // the light update keeps current, and eases toward it so it does not jump. The window numbers are in pixels from the tank's top-left
        // (the 4x3 tank is 64 x 48 px, the art's 2 px sheet gaps are not counted); they are instance fields so hot reload can nudge them.

        public int WindowX = 18;
        public int WindowY = 18;
        public int WindowWidth = 28;
        public int WindowHeight = 16;

        /// <summary>How fast the drawn level eases toward the real one, per tick (0 to 1).</summary>
        public float WindowEase = 0.12f;

        float shownFill = -1f;       // runtime only: the fill fraction as drawn, -1 = not initialised yet
        int shownLiquidType = -1;    // runtime only: the last liquid seen in the tank, so the level can drain after the tank forgets its type

        public void DynamicUpdate()
        {
            LiquidStack tank = InputLiquids[0];
            if (!tank.IsEmpty)
                shownLiquidType = tank.LiquidType;

            float target = tank.IsEmpty ? 0f : MathHelper.Clamp(tank.Amount / Math.Max(1f, FuelCapacity), 0f, 1f);
            shownFill = shownFill < 0f ? target : MathHelper.Lerp(shownFill, target, MathHelper.Clamp(WindowEase, 0.01f, 1f));
            if (Math.Abs(shownFill - target) < 0.002f)
                shownFill = target;
        }

        public bool TryGetDynamicBounds(out Vector2 center, out float radius)
        {
            center = new Vector2(Position.X * 16f + SuperWeaponAssembly.TankWidth * 8f, Position.Y * 16f + SuperWeaponAssembly.TankHeight * 8f);
            radius = 48f;
            return shownFill > 0.001f && shownLiquidType >= 0;
        }

        public void DynamicDraw(SpriteBatch spriteBatch) { }

        public void DynamicDrawBehindTiles(SpriteBatch spriteBatch)
        {
            if (shownFill <= 0.001f || shownLiquidType < 0)
                return;

            int levelPx = Math.Max(1, (int)Math.Round(WindowHeight * shownFill));
            int originX = Position.X * 16;
            int originY = Position.Y * 16;

            // 1 px wider on the sides and at the bottom than the hole: the casing art covers the overlap, so a sub-pixel difference
            // between the casing and this draw (zoom, screen position) can never show a gap.
            int bottom = originY + WindowY + WindowHeight + 1;
            Rectangle fill = new Rectangle(originX + WindowX - 1, bottom - levelPx - 1, WindowWidth + 2, levelPx + 1);

            Color tint = LiquidTypeRegistry.Get(shownLiquidType).RenderColor;
            DrawWindowRect(spriteBatch, fill, tint, 0.85f);

            if (levelPx >= 3)
                DrawWindowRect(spriteBatch, new Rectangle(fill.X, fill.Y, fill.Width, 1), Color.Lerp(tint, Color.White, 0.4f), 0.95f);   // lighter surface line
        }

        // One rectangle in world pixels, split per tile so every part picks up its own tile's light.
        void DrawWindowRect(SpriteBatch spriteBatch, Rectangle area, Color tint, float alpha)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new Rectangle(0, 0, 1, 1);   // MagicPixel is not guaranteed to be 1x1

            for (int tx = Position.X; tx < Position.X + SuperWeaponAssembly.TankWidth; tx++)
            {
                for (int ty = Position.Y; ty < Position.Y + SuperWeaponAssembly.TankHeight; ty++)
                {
                    Rectangle part = Rectangle.Intersect(area, new Rectangle(tx * 16, ty * 16, 16, 16));
                    if (part.Width <= 0 || part.Height <= 0)
                        continue;

                    Color color = tint.MultiplyRGBA(Lighting.GetColor(tx, ty)) * alpha;
                    Rectangle screen = new Rectangle(part.X - (int)Main.screenPosition.X, part.Y - (int)Main.screenPosition.Y, part.Width, part.Height);
                    spriteBatch.Draw(pixel, screen, source, color);
                }
            }
        }
    }
}