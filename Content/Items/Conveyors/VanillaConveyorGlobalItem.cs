using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Conveyors
{
    public class VanillaConveyorGlobalItem : GlobalItem
    {
        // Restrict this GlobalItem exclusively to vanilla conveyor belt items
        public override bool AppliesToEntity(Item entity, bool lateSearch)
        {
            return entity.type == ItemID.ConveyorBeltLeft || entity.type == ItemID.ConveyorBeltRight;
        }

        public override bool CanRightClick(Item item) => true;

        public override void RightClick(Item item, Player player)
        {
            int currentStack = item.stack;

            // Swap between vanilla left and right conveyor IDs
            int nextType = (item.type == ItemID.ConveyorBeltLeft) ? ItemID.ConveyorBeltRight : ItemID.ConveyorBeltLeft;

            // Re-initialize item properties to match the new item type
            item.SetDefaults(nextType);
            item.stack = currentStack;

            Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override bool ConsumeItem(Item item, Player player) => false;
    }
}