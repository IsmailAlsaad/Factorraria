using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    // Shared logic of both poles. A pole is an electric consumer with a small constant draw (transport loss), no slots and no recipes.
    // Authority: the server (and single-player) runs the power grid and decides isOn; clients get isOn / isWorking through the normal machine snapshot.
    public abstract class PowerPoleTileEntityBase : ElectricConsumerMachine
    {
        protected override int InputSlotCount => 0;
        protected override int OutputSlotCount => 0;

        // Connection points (crossarm tips) in pixels, measured from the footprint's top-left corner (this entity's Position * 16).
        // Canvas pixel (cx, cy) of the art becomes (cx - originX * 16, cy - originY * 16), and both poles use originX 3, originY 0, so (cx - 48, cy).
        // Used by the link patches (P3 onward); nothing reads it in P2.
        public abstract Vector2[] LinkPointOffsets { get; }

        public int LinkPointCount => LinkPointOffsets.Length;

        public Vector2 GetLinkPointWorld(int index) => Position.ToVector2() * 16f + LinkPointOffsets[index];

        public override void Update()
        {
            base.Update();

            // BaseMachine only sets isWorking while a recipe runs, and a pole has none. PowerNetwork.Tick counts demand only from
            // machines that are working, so the pole sets it itself or its transport loss would never reach the grid.
            isWorking = true;
        }
    }
}