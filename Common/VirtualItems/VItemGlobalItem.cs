using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.VirtualItems
{
    public class VItemGlobalItem : GlobalItem
    {
        public override bool InstancePerEntity => true;

        public int ConveyorImmunityTimer = 0;

        public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
        {
            if (ConveyorImmunityTimer > 0)
            {
                ConveyorImmunityTimer--;
            }
        }
    }
}