using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // The vanilla Corrupt/Crimson (Deathweed) and Blinkroot planter boxes spread / convert, so they get a tooltip line through a GlobalItem.
    public class PlanterBoxTooltipGlobalItem : GlobalItem
    {
        static string Line(int type)
        {
            if (type == ItemID.CorruptPlanterBox)
                return "Can spread the corruption to nearby blocks";
            if (type == ItemID.CrimsonPlanterBox)
                return "Can spread the crimson to nearby blocks";
            if (type == ItemID.BlinkrootPlanterBox)
                return "Can convert to other planter boxes when grass spreads on it";
            return null;
        }

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return Line(entity.type) != null;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            string line = Line(item.type);
            if (line != null)
                tooltips.Add(new TooltipLine(Mod, "FactorrariaPlanterBoxSpread", line));
        }
    }
}