using Factorraria.Common.Machines;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // One input slot (a pickaxe, axe, hammer or a block) and one output slot. No recipes.
    public class RoboticArmTileEntity : ElectricConsumerMachine
    {
        public override int ValidTileType => ModContent.TileType<RoboticArmTile>();
        public override float PowerDemand => 100f;
        protected override int InputSlotCount => 1;
        protected override int OutputSlotCount => 1;

        public override void Update()
        {
            base.Update();

            // Phase 1 stand-in so power draw and brownout can be tested before the arm has any work to do:
            // it counts as working whenever the slot is loaded. Phase 4 replaces this with a real "has a valid job" check.
            isWorking = !InputSlots[0].IsAir;
        }

        public bool IsTool(Item item) => item.pick > 0 || item.axe > 0 || item.hammer > 0;

        // v1 only places plain solid blocks (the filter the spike validated): no furniture, torches, platforms, seeds,
        // grass-type or moss tiles. Keeping the slot and the conveyor intake to the same rule means nothing can sit in the
        // slot that the arm will never place.
        public bool IsBlock(Item item)
        {
            int tile = item.createTile;
            if (tile < 0 || IsTool(item))
                return false;

            if (!Main.tileSolid[tile] || Main.tileFrameImportant[tile] || Main.tileMoss[tile])
                return false;

            return tile is not (2 or 23 or 109 or 199 or 477 or 492 or 633 or 661 or 662 or 60 or 70);
        }

        // What the input slot accepts from the cursor.
        public bool IsAcceptedItem(Item item)
        {
            if (item == null || item.IsAir)
                return false;

            return IsTool(item) || IsBlock(item);
        }

        // Conveyors only ever hand over blocks, and only while the slot is empty or already holds that block;
        // a loaded tool is never replaced.
        protected override bool TryGetIntakeSlot(int itemType, out int slot, out int limit)
        {
            slot = 0;
            limit = 0;

            if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item sample) || !IsBlock(sample))
                return false;

            Item held = InputSlots[0];
            if (!held.IsAir && held.type != itemType)
                return false;

            limit = sample.maxStack;
            return true;
        }
    }
}