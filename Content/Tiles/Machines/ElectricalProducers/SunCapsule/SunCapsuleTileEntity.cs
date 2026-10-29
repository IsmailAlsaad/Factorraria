using Factorraria.Common.Machines;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.SunCapsule
{
    // Battery. Takes surplus power from its grid (up to MaxChargeRate), gives it back when the grid is short (up to MaxDischargeRate).
    // Authority: the power grid runs on the server / in single-player only; PowerNetwork.Tick calls ApplyFlow there. Clients get
    // FlowState + a rounded charge through the light update (WriteLightExtra), and the exact charge through the snapshot.
    public class SunCapsuleTileEntity : ElectricProducerMachine, IElectricStorage, IMachineDynamicDraw
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

        // The battery looks "running" (On art) while it has energy in it, whatever the grid is doing. Going from empty to charging plays
        // the startup animation, going from some energy to empty plays the shutdown. Exact, not rounded: clients get it as its own flag.
        bool hasEnergy;
        public override bool IsRunning => hasEnergy;

        public float AvailableCharge() => Math.Min(MaxChargeRate, (EnergyCapacity - storedEnergy) * 60f);
        public float AvailableDischarge() => Math.Min(MaxDischargeRate, storedEnergy * 60f);

        // What the capsule's UI icon shows: the fill (0 to 1) and the hover text "stored/capacity", orange while charging, blue while discharging.
        // Clients read the same values from the synced charge, so nothing new is sent.
        public float GetChargeFraction() => EnergyCapacity > 0f ? Math.Clamp(storedEnergy / EnergyCapacity, 0f, 1f) : 0f;

        public (string text, Color color) GetChargeReadout()
        {
            string text = ((int)MathF.Round(storedEnergy)).ToString() + "/" + ((int)EnergyCapacity).ToString();
            Color color = FlowState == 1 ? new Color(255, 150, 40) : FlowState == 2 ? new Color(90, 170, 255) : Color.White;
            return (text, color);
        }

        // Particles, client side only (single-player included): orange sparks drawn in while charging, blue sparks thrown out while
        // discharging. FlowState already reaches every client through the light state, so every screen sees the same thing.
        public void DynamicUpdate()
        {
            if (FlowState == 0 || !IsOnScreen(MachineCenter, 150f)) return;
            if (!Main.rand.NextBool(3)) return;

            if (FlowState == 1)
            {
                Vector2 offset = Main.rand.NextFloat(0f, MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(28f, 44f);
                Dust charging = Dust.NewDustPerfect(MachineCenter + offset + new Vector2(0f,-16f), DustID.Torch, -offset * 0.04f, 100, default, Main.rand.NextFloat(1f, 1.4f));
                charging.noGravity = true;
            }
            else
            {
                Vector2 offset = Main.rand.NextFloat(0f, MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0f, 28f);
                Dust charging = Dust.NewDustPerfect(MachineCenter + offset + new Vector2(0f, -18f), DustID.Electric, offset * 0.04f, 100, default, Main.rand.NextFloat(0.6f, 1f));
                charging.noGravity = true;
                //Vector2 start = MachineCenter + Main.rand.NextVector2Circular(10f, 18f);
                //Vector2 velocity = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, -0.4f));
                //Dust discharging = Dust.NewDustPerfect(start + new Vector2(0f,-16f), DustID.Electric, velocity, 100, default, Main.rand.NextFloat(0.8f, 1.1f));
                //discharging.noGravity = true;
            }
        }

        public bool TryGetDynamicBounds(out Vector2 center, out float radius)
        {
            center = MachineCenter;
            radius = 0f;
            return false;   // nothing to draw, DynamicUpdate only
        }

        public void DynamicDraw(SpriteBatch spriteBatch) { }

        public void ApplyFlow(float power)
        {
            // power is units per second and a tick is 1/60 s.
            if (power > 0f)
            {
                storedEnergy = Math.Min(EnergyCapacity, storedEnergy + power / 60f);
                hasEnergy = storedEnergy > 0f;
                FlowState = 1;
            }
            else if (power < 0f)
            {
                storedEnergy = Math.Max(0f, storedEnergy + power / 60f);
                hasEnergy = storedEnergy > 0f;
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
            writer.Write(hasEnergy);
        }

        protected override void ReadLightExtra(BinaryReader reader)
        {
            base.ReadLightExtra(reader);
            FlowState = reader.ReadByte();
            int steps = reader.ReadByte();
            hasEnergy = reader.ReadBoolean();

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
            hasEnergy = storedEnergy > 0f;
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

            hasEnergy = storedEnergy > 0f;
        }
    }
}