using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.VirtualItems
{
    public class VItemGlobalItem : GlobalItem
    {
        public override bool InstancePerEntity => true;

        // Patch 7.4: server / single-player only. Set in VirtualItemSystem.ConvertVItemsToItems, read in ConvertItemsToVItems (both
        // never run on a client) and counted down by Item.Update on whichever side runs the item. It is never sent, so a client's
        // copy always stays 0 and is never used.
        public int ConveyorImmunityTimer = 0;

        public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
        {
            if (ConveyorImmunityTimer > 0)
            {
                ConveyorImmunityTimer--;
            }
        }

        public override void SetDefaults(Item entity)
        {
            if(entity.type == ItemID.Coal)
            {
                entity.maxStack = 9999;
            }
        }
    }
}