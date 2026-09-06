using Factorraria.Common.Liquids;
using Factorraria.Common.Systems;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Machines
{
    public abstract class MotorTileEntityBase : ElectricConsumerMachine
    {
        public abstract float PumpStrength { get; }

        // Throughput this motor offers when isOn == false — makes it behave like a
        // same-tier plain pipe segment instead of a dead stop in the network.
        public abstract float PipeEquivalentMaxFlowRate { get; }

        public Direction Facing = Direction.Right;

        bool wasOn;

        // Sealed so future motor tiers can't accidentally override Update() directly
        // and silently break isOn-change detection — they override UpdateMotor() instead.
        public sealed override void Update()
        {
            if (isOn != wasOn)
            {
                wasOn = isOn;
                LiquidNetworkSystem.flowNeedsRecalculating = true;
            }

            UpdateMotor();
        }

        protected virtual void UpdateMotor() { }

        public override void OnRightClick(int i, int j)
        {
            Facing = Facing.Rotate90();

            LiquidNetworkSystem.networkNeedsRebuilding = true;
            PipeTileBase.ReframeNeighbors(i, j);
        }

        public override void SaveData(TagCompound tag)
        {
            base.SaveData(tag);
            tag["Facing"] = (int)Facing;
        }

        public override void LoadData(TagCompound tag)
        {
            base.LoadData(tag);
            Facing = (Direction)tag.GetInt("Facing");
        }
    }
}