using Factorraria.Common.Liquids;
using Factorraria.Common.Systems;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Machines
{
    // Every motor TIER (MK1, future MK2, etc) inherits this and only has to say its
    // own strength and power draw — placement, facing, and save/load are shared here,
    // same relationship PipeTileBase has to PipeTileMK1/MK2.
    public abstract class MotorTileEntityBase : ElectricConsumerMachine
    {
        // How strongly this motor pushes liquid — the flow-rate contribution used
        // later in the tug-of-war/headlift calculation. Not wired into Tick() logic
        // yet, per your "future us problem" call — this is just the number each tier
        // declares, ready for that step when we get to it.
        public abstract float PumpStrength { get; } // terraria has 0 = no liquid 255 = full liquid so the units for flow rate are this

        // Which way this motor is trying to push liquid. Defaults to Right on
        // placement; DECISION NEEDED (flagging, not deciding silently): right now
        // the only way to change it is right-clicking to rotate 90°, since motors
        // don't have a UI to open. If you'd rather this be set by placement
        // direction (like vanilla directional tiles) or a dedicated wrench-style
        // tool item instead, this is the one method to swap out.
        public Direction Facing = Direction.Right;

        public override void OnRightClick(int i, int j)
        {
            Facing = Facing.Rotate90();

            // Facing changed → sprite rotation (MachineVisualOverride reads this.Facing)
            // and which pipe sides are even allowed to attach both need re-evaluating.
            LiquidNetworkSystem.networkNeedsRebuilding = true;
            PipeTileBase.ReframeNeighbors(i, j);
        }

        public override void SaveData(TagCompound tag)
        {
            base.SaveData(tag); // saves InputSlots/OutputSlots if this motor ever has any
            tag["Facing"] = (int)Facing;
        }

        public override void LoadData(TagCompound tag)
        {
            base.LoadData(tag);
            Facing = (Direction)tag.GetInt("Facing");
        }
    }
}