using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Statues;
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

        public float ShimmerDrainPerSecond = 15f;

        public int AwakenRadiusTiles { get; } = 30;

        // IStatueAwakener
        public bool IsAwakening => isOn && isWorking && !removed;

        bool wasAwakening;
        bool removed;
        public Point16 AwakenerOrigin => Position;
        public int AwakenedStatueCount { get; set; }
        public int AwakenedEnemyCount { get; set; }

        // Whether the starry shimmer background is switched on while the tank has shimmer. Right-click flips it (only with
        // shimmer in the tank). It is remembered when the tank runs dry and through saves.
        public bool BackgroundActive = true;

        public bool HasShimmer => InputLiquids[0].LiquidType == LiquidTypeRegistry.Shimmer && InputLiquids[0].Amount > 0f;

        public override void OnRightClick(int i, int j)
        {
            if (!HasShimmer) return;

            BackgroundActive = !BackgroundActive;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

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
            isWorking = hasShimmer;

            SyncMonolithFrames(hasShimmer && BackgroundActive);

            // The statue list is rebuilt only when this machine switches on or off.
            if (IsAwakening != wasAwakening)
            {
                wasAwakening = IsAwakening;
                ModContent.GetInstance<AwakenedStatueSystem>().RescanFor(this);
            }

            if (!hasShimmer) return;

            tank.Amount -= ShimmerDrainPerSecond * AwakenedStatueCount / 60f;
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