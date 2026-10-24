using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    // The Magic Materializer is the vanilla Shimmer Monolith, so its tooltip is added through a GlobalItem.
    public class MagicMaterializerItemTooltip : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.ShimmerMonolith;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "FactorrariaMaterializerUI", "Right click to open the shimmer tank"));
            tooltips.Add(new TooltipLine(Mod, "FactorrariaMaterializerToggle", "Shift + Right click to turn the shimmer background on or off"));
        }
    }
}