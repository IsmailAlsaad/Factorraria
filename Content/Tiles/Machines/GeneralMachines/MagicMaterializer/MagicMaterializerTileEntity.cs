using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Statues;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    public class MagicMaterializerTileEntity : BaseMachine, IStatueAwakener
    {
        public override int ValidTileType => TileID.ShimmerMonolith;

        // One shimmer tank, no recipes. The tank is filter-locked to the liquids in this table.
        protected override LiquidFuelTable AcceptedLiquidFuels => MagicMaterializerRegistry.LiquidFuels;

        public float ShimmerDrainPerSecond = 5f;

        public int AwakenRadiusTiles { get; } = 30;

        // IStatueAwakener
        public bool IsAwakening => isOn && isWorking && !removed;

        bool wasAwakening;
        bool removed;
        public Point16 AwakenerOrigin => Position;
        public int AwakenedStatueCount { get; set; }
        public int AwakenedEnemyCount { get; set; }

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

            KeepVanillaMonolithOff();

            LiquidStack tank = InputLiquids[0];
            bool hasShimmer = tank.LiquidType == LiquidTypeRegistry.Shimmer && tank.Amount > 0f;
            isWorking = hasShimmer;

            // The statue list is rebuilt only when this machine switches on or off.
            if (IsAwakening != wasAwakening)
            {
                wasAwakening = IsAwakening;
                ModContent.GetInstance<AwakenedStatueSystem>().RescanFor(this);
            }

            if (!hasShimmer) return;

            tank.Amount -= ShimmerDrainPerSecond / 60f;
            if (tank.Amount <= 0f)
            {
                tank.Amount = 0f;
                tank.LiquidType = -1;   // drained: the tank may take shimmer again
            }
        }

        // The tile is a vanilla monolith, so wires and vanilla right-click can flip it into its vanilla "on" frame.
        // This machine ignores that state, so the toggled half of the frame sheet is folded back every tick.
        void KeepVanillaMonolithOff()
        {
            TileObjectData data = TileObjectData.GetTileData(ValidTileType, 0);
            if (data == null) return;

            int toggleOffset = data.Width * 18;
            for (int x = 0; x < data.Width; x++)
            {
                for (int y = 0; y < data.Height; y++)
                {
                    Tile tile = Framing.GetTileSafely(Position.X + x, Position.Y + y);
                    if (tile.HasTile && tile.TileType == ValidTileType && tile.TileFrameX >= toggleOffset)
                        tile.TileFrameX -= (short)toggleOffset;
                }
            }
        }
    }
}