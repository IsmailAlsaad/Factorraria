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

        // Visual only (PipeFlowVisualSystem): the liquid type this ON pump moved in the last few ticks, -1 = none. FlowNetwork writes it on the
        // server (or in single-player), it is forgotten after FlowIdleLimitTicks without a transfer (so a pump that stalls for a tick or two does
        // not flicker the animation), and it reaches every client in this motor's light update. Never saved.
        public int FlowLiquidType = -1;
        public int FlowIdleTicks;
        public int FlowIdleLimitTicks = 20;

        // Visual only: how much liquid this pump moves, for the particle amount. FlowNetwork adds what a route moved this tick to
        // FlowMovedThisTick (server / single-player), Update smooths it into FlowRate (units per tick) and rounds that to FlowRateLevel
        // (units of FlowRateStep, 0..255), which is what the light update carries. Quantized on purpose: the payload is only re-sent when
        // the level changes. FlowRateStep must be the same on the server and the clients (it is a plain default, so it is).
        public float FlowMovedThisTick;
        public float FlowRate;
        public float FlowRateSmoothing = 0.1f;   // 0..1 per tick: how fast FlowRate follows the real amount
        public float FlowRateStep = 0.25f;       // units per tick one FlowRateLevel stands for
        public byte FlowRateLevel;

        // Sealed so future motor tiers override UpdateMotor() instead. A power flip (grid, brownout, wire toggle) needs nothing here:
        // FlowNetwork.Tick reads isOn and PumpStrength of every motor each tick and refreshes its routes when one changed.
        public sealed override void Update()
        {
            base.Update();
            UpdateMotor();

            // A client replica only mirrors what the server sent, so only the server (and single-player) forgets the flow.
            if (Terraria.Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient)
            {
                if (FlowLiquidType >= 0 && ++FlowIdleTicks > FlowIdleLimitTicks)
                {
                    FlowLiquidType = -1;
                    FlowRate = 0f;
                }

                FlowRate += (FlowMovedThisTick - FlowRate) * FlowRateSmoothing;
                FlowMovedThisTick = 0f;
                FlowRateLevel = (byte)System.Math.Clamp((int)System.Math.Round(FlowRate / System.Math.Max(0.01f, FlowRateStep)), 0, 255);
            }
        }

        protected virtual void UpdateMotor() { }

        public override void OnRightClick(int i, int j)
        {
            // Patch 8.3 (P3): the server owns Facing (its liquid networks read it). A multiplayer client only asks; the server turns the
            // motor and tells every client (MotorRotate), and the reframing of the neighbouring pipes happens when that answer arrives.
            if (Terraria.Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
            {
                global::Factorraria.Common.Net.MotorRotate.Request(this);
                return;
            }

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

        // The flow animation needs the liquid on clients that only get light updates (no snapshot, no UI). One small number, re-sent only when it changes.
        protected override void WriteLightExtra(BinaryWriter writer)
        {
            base.WriteLightExtra(writer);
            writer.Write7BitEncodedInt(FlowLiquidType + 1);
            writer.Write(FlowRateLevel);
        }

        protected override void ReadLightExtra(BinaryReader reader)
        {
            base.ReadLightExtra(reader);
            FlowLiquidType = reader.Read7BitEncodedInt() - 1;
            FlowRateLevel = reader.ReadByte();
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