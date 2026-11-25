using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.Solidifier
{
    public class SolidifierTileEntity : ElectricConsumerMachine
    {
        public override int ValidTileType => TileID.Solidifier;
        public override float PowerDemand => 100f;

        public override RecipeBook Recipes => SolidifierRecipeRegistry.Book;

        protected override int InputLiquidCount => 2;
        protected override int OutputSlotCount => 1;

        protected override void OnAnimationFrameChanged(int newFrame, int previousFrame)
        {
        
        }
    }

    public class SolidifierItemTooltip : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.Solidifier;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.RemoveAll(line => line.Mod == "Terraria" && line.Name.StartsWith("Tooltip"));
            tooltips.Add(new TooltipLine(Mod, "FactorrariaSolidifier", "Merges two liquids to form a solid"));
        }
    }
}
