using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Common.Statues;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    public class MagicMaterializerTileEntity : BaseMachine, IStatueAwakener
    {
        public override int ValidTileType => TileID.ShimmerMonolith;

        // One shimmer tank, no recipes. The tank is filter-locked to the liquids in this table.
        protected override LiquidFuelTable AcceptedLiquidFuels => MagicMaterializerRegistry.LiquidFuels;

        public float ShimmerDrainPerSecond = 3f;

        public int AwakenRadiusTiles { get; } = 30;

        // IStatueAwakener
        public bool IsAwakening => isOn && isWorking && !removed;

        bool wasAwakening;
        bool removed;

        // Runtime only. The statues in range set the drain whether or not they are awakened, so a starved machine still knows
        // its demand. Refreshed on a timer (server / single-player only). Hot-reloadable.
        int nearbyStatueCount;
        int statueScanTimer;
        public int StatueScanIntervalTicks = 20;
        public Point16 AwakenerOrigin => Position;
        public int AwakenedStatueCount { get; set; }
        public int AwakenedEnemyCount { get; set; }

        // Whether the starry shimmer background is switched on while the tank has shimmer. Right-click flips it (only with
        // shimmer in the tank). It is remembered when the tank runs dry and through saves. (Shift + right click flips it.)
        public bool BackgroundActive = true;

        public bool HasShimmer => InputLiquids[0].LiquidType == LiquidTypeRegistry.Shimmer && InputLiquids[0].Amount > 0f;

        public override void OnRightClick(int i, int j)
        {
            // Regular right click opens the tank UI (BaseMachine handles it, multiplayer included). Shift + right click flips the background.
            if (!Terraria.UI.ItemSlot.ShiftInUse)
            {
                base.OnRightClick(i, j);
                return;
            }

            // Patch 6.4: on a multiplayer client the server owns the value. The request carries the wanted value; the answer (a broadcast)
            // changes every replica and plays the click for the player who asked. Nothing is flipped here.
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                MaterializerToggle.Request(this);
                return;
            }

            if (!HasShimmer) return;

            BackgroundActive = !BackgroundActive;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        // Patch 5.1: the background toggle is part of the machine snapshot. The awakened statue and enemy counts stay server runtime state.
        protected override void WriteSnapshotExtra(BinaryWriter writer)
        {
            base.WriteSnapshotExtra(writer);
            writer.Write(BackgroundActive);
        }

        protected override void ReadSnapshotExtra(BinaryReader reader)
        {
            base.ReadSnapshotExtra(reader);
            BackgroundActive = reader.ReadBoolean();
        }

        protected override string DescribeSnapshotExtra() => base.DescribeSnapshotExtra() + " background=" + (BackgroundActive ? "on" : "off");

        public override void SaveData(TagCompound tag)
        {
            base.SaveData(tag);
            tag["BackgroundActive"] = BackgroundActive;
        }

        public override void LoadData(TagCompound tag)
        {
            base.LoadData(tag);
            if (tag.ContainsKey("BackgroundActive"))
                BackgroundActive = tag.GetBool("BackgroundActive");
        }

        // Pumps may treat a world shimmer pool as bottomless when this machine is the sink.
        public override bool TreatsWorldLiquidAsInfinite(int liquidType) => liquidType == LiquidTypeRegistry.Shimmer;

        public override void OnKill()
        {
            base.OnKill();

            removed = true;
            ModContent.GetInstance<AwakenedStatueSystem>().RescanFor(this);   // hand its statues to neighbouring machines
        }

        public override void Update()
        {
            base.Update();   // recipe-less: conveyor intake only

            LiquidStack tank = InputLiquids[0];
            bool hasShimmer = HasShimmer;

            // Multiplayer: the server (or single-player) owns the working state, which is synced to clients. A client never decides it.
            bool authority = Main.netMode != NetmodeID.MultiplayerClient;

            float decreaseAmount = 0f;
            if (authority)
            {
                // The drain is what the statues in range cost, awakened or not (the awakened set only holds them while working).
                if (!hasShimmer)
                    statueScanTimer = 0;   // rescan the moment shimmer is back
                else if (--statueScanTimer <= 0)
                {
                    statueScanTimer = StatueScanIntervalTicks;
                    nearbyStatueCount = ModContent.GetInstance<AwakenedStatueSystem>().CountStatuesInRadius(this);
                }

                decreaseAmount = ShimmerDrainPerSecond * nearbyStatueCount;

                // Working only while the tank can pay this tick's drain. A starved machine keeps draining (below), so the
                // tank only recovers when the supply is higher than the drain.
                isWorking = hasShimmer && tank.Amount >= decreaseAmount;
            }

            SyncMonolithFrames(isWorking && BackgroundActive);

            // The awakened list is rebuilt only when this machine switches on or off.
            if (IsAwakening != wasAwakening)
            {
                wasAwakening = IsAwakening;
                ModContent.GetInstance<AwakenedStatueSystem>().RescanFor(this);
            }

            if (!authority || !hasShimmer) return;

            tank.Amount -= decreaseAmount;
            if (tank.Amount <= 0f)
            {
                tank.Amount = 0f;
                tank.LiquidType = -1;   // drained: the tank may take shimmer again
            }
        }

        // Vanilla keeps the monolith's on/off state in TileFrameY (one tile-height block per state, read as frameY / 54 by
        // SceneMetrics). This machine owns it: the starry background is on only while backgroundOn is true, whatever vanilla
        // right-click or wires did to the frames.
        void SyncMonolithFrames(bool backgroundOn)
        {
            TileObjectData data = TileObjectData.GetTileData(ValidTileType, 0);
            if (data == null) return;

            int stateHeight = data.Height * 18;
            bool changed = false;
            for (int x = 0; x < data.Width; x++)
            {
                for (int y = 0; y < data.Height; y++)
                {
                    Tile tile = Framing.GetTileSafely(Position.X + x, Position.Y + y);
                    if (!tile.HasTile || tile.TileType != ValidTileType) continue;

                    short wanted = (short)(tile.TileFrameY % stateHeight + (backgroundOn ? stateHeight : 0));
                    if (tile.TileFrameY == wanted) continue;

                    tile.TileFrameY = wanted;
                    changed = true;
                }
            }

            if (changed && Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, Position.X, Position.Y, data.Width, data.Height);
        }
    }
}