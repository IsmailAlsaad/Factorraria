using Factorraria.Common.Machines;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.Extractinator
{
    /// <summary>
    /// Electric extractinator: one input slot, no output slots. Every finished cycle eats one input,
    /// rolls its drop (ExtractinatorRecipeRegistry) and puts the result on a free tile of the 3x3 footprint as a vItem.
    /// With no free footprint tile the machine stops working until one frees up.
    /// </summary>
    public class ExtractinatorTileEntity : ElectricConsumerMachine
    {
        public override int ValidTileType => TileID.Extractinator;
        public override float PowerDemand => 150f;       // placeholder, tune while playtesting
        protected override int InputSlotCount => 1;
        protected override int WorkDuration => 15;      // ticks per input item (vanilla use time), placeholder
        const int InputBuffer = 50;                     // how many items conveyors may stack in the slot

        // Recipes stays null on purpose: the extractinator rolls random drops, which the CustomRecipe pipeline can't express.

        public override void Update()
        {
            base.Update();   // conveyor intake + grid switching

            Item slot = InputSlots[0];
            if (slot.IsAir || !ExtractinatorRecipeRegistry.TryGet(slot.type, out ExtractinatorRecipe recipe) || !TryFindOutputTile(out Point outputTile))
            {
                WorkProgress = 0;
                isWorking = false;
                return;
            }

            isWorking = true;     // counts toward the grid's demand even during a brownout
            if (!isOn) return;    // browned out: freeze progress

            WorkProgress++;
            if (WorkProgress < WorkDuration) return;
            WorkProgress = 0;

            slot.stack--;
            if (slot.stack <= 0) InputSlots[0] = new Item();

            if (recipe.TryRoll(out int itemType, out int stack))
                VirtualItemSystem.SpawnVirtualItem(itemType, stack, outputTile.X, outputTile.Y);
        }

        protected override bool TryGetIntakeSlot(int itemType, out int slot, out int limit)
        {
            slot = 0;
            limit = InputBuffer;
            if (!ExtractinatorRecipeRegistry.TryGet(itemType, out _)) return false;

            Item held = InputSlots[0];
            return held.IsAir || held.type == itemType;
        }

        // A random free tile of the footprint, bottom row first (so a conveyor under the machine gets fed).
        bool TryFindOutputTile(out Point result)
        {
            result = default;
            if (!MachineInitialized || MachineWidth <= 0 || MachineHeight <= 0) return false;

            int free = 0;
            for (int row = MachineHeight - 1; row >= 0 && free == 0; row--)
            {
                for (int col = 0; col < MachineWidth; col++)
                {
                    int x = cornerPosition.X + col;
                    int y = cornerPosition.Y + row;
                    if (VirtualItemSystem.IsTileOccupied(x, y)) continue;

                    free++;
                    if (Main.rand.Next(free) == 0) result = new Point(x, y);
                }
            }
            return free > 0;
        }
    }
}