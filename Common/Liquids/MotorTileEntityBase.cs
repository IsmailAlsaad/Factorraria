using Factorraria.Common.Liquids;
using Factorraria.Common.Systems;
using System.IO;
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
            base.Update();

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

        // Patch 5.1: the facing is part of the machine snapshot (the pipe network itself stays server-side until Phase 8).
        protected override void WriteSnapshotExtra(BinaryWriter writer)
        {
            base.WriteSnapshotExtra(writer);
            writer.Write((byte)Facing);
        }

        protected override void ReadSnapshotExtra(BinaryReader reader)
        {
            base.ReadSnapshotExtra(reader);
            int facing = reader.ReadByte();
            if (facing <= (int)Direction.Right)
                Facing = (Direction)facing;
        }

        protected override string DescribeSnapshotExtra() => base.DescribeSnapshotExtra() + " facing=" + Facing;

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