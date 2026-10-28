using Factorraria.Common.Machines;
using System;
using System.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.SunCapsule
{
    // Battery. Takes surplus power from its grid (up to MaxChargeRate), gives it back when the grid is short (up to MaxDischargeRate).
    // Authority: the power grid runs on the server / in single-player only; PowerNetwork.Tick calls ApplyFlow there. Clients get
    // FlowState + a rounded charge through the light update (WriteLightExtra), and the exact charge through the snapshot.
    public class SunCapsuleTileEntity : ElectricProducerMachine, IElectricStorage
    {
        public override int ValidTileType => ModContent.TileType<SunCapsuleTile>();

        // Not a generator: the discharge is added by PowerNetwork.Tick through IElectricStorage, never counted as normal supply.
        public override float PowerSupply => 0f;

        public float EnergyCapacity => 3000f;   // units
        public float MaxChargeRate => 60f;      // units per second
        public float MaxDischargeRate => 60f;   // units per second

        public float StoredEnergy => storedEnergy;
        float storedEnergy;

        // What the machine is doing this tick, for the drawing system: 0 idle, 1 charging, 2 discharging.
        // The visual registry's On states are registered in this order (SunCapsule_On, _On_Charging, _On_Discharging).
        public byte FlowState;
        public override int VisualState => FlowState;

        public float AvailableCharge() => Math.Min(MaxChargeRate, (EnergyCapacity - storedEnergy) * 60f);
        public float AvailableDischarge() => Math.Min(MaxDischargeRate, storedEnergy * 60f);

        public void ApplyFlow(float power)
        {
            // power is units per second and a tick is 1/60 s.
            if (power > 0f)
            {
                storedEnergy = Math.Min(EnergyCapacity, storedEnergy + power / 60f);
                FlowState = 1;
            }
            else if (power < 0f)
            {
                storedEnergy = Math.Max(0f, storedEnergy + power / 60f);
                FlowState = 2;
            }
            else
            {
                FlowState = 0;
            }
        }

        public override void Update()
        {
            base.Update();

            // A battery is always ready to take part in its grid. isWorking is masked by the wire switch, so a wire can still disconnect it.
            isWorking = true;
        }

        // Charge in 1/32 steps so the light update only changes (and only gets sent) when the charge crosses a step.
        int QuantizedCharge(float energy) => (int)Math.Clamp(energy / EnergyCapacity * 32f, 0f, 32f);

        protected override void WriteLightExtra(BinaryWriter writer)
        {
            base.WriteLightExtra(writer);
            writer.Write(FlowState);
            writer.Write((byte)QuantizedCharge(storedEnergy));
        }

        protected override void ReadLightExtra(BinaryReader reader)
        {
            base.ReadLightExtra(reader);
            FlowState = reader.ReadByte();
            int steps = reader.ReadByte();

            // Keep the exact value from the snapshot while it still rounds to what the server sent.
            if (QuantizedCharge(storedEnergy) != steps)
                storedEnergy = steps / 32f * EnergyCapacity;
        }

        protected override void WriteSnapshotExtra(BinaryWriter writer)
        {
            base.WriteSnapshotExtra(writer);
            writer.Write(FlowState);
            writer.Write(storedEnergy);
        }

        protected override void ReadSnapshotExtra(BinaryReader reader)
        {
            base.ReadSnapshotExtra(reader);
            FlowState = reader.ReadByte();
            storedEnergy = reader.ReadSingle();
        }

        protected override string DescribeSnapshotExtra() => base.DescribeSnapshotExtra() + " charge=" + storedEnergy.ToString("0.0") + "/" + EnergyCapacity + " flow=" + FlowState;

        public override void SaveData(TagCompound tag)
        {
            base.SaveData(tag);
            if (storedEnergy > 0f)
                tag["SunCharge"] = storedEnergy;
        }

        public override void LoadData(TagCompound tag)
        {
            base.LoadData(tag);
            if (tag.ContainsKey("SunCharge"))
                storedEnergy = Math.Clamp(tag.GetFloat("SunCharge"), 0f, EnergyCapacity);
        }
    }
}