using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.DecayChamber
{
    public class DecayChamberTileEntity : BaseMachine
    {
        public override int ValidTileType => TileID.LesionStation;   // vanilla "Decay Chamber"
        public override RecipeBook Recipes => DecayChamberRecipeRegistry.Book;

        // 1 ingredient slot (Recipes.MaxIngredientCount = 1), 1 liquid tank, 1 output slot. No fuel, no power.
        protected override int InputLiquidCount => 1;
        protected override int OutputSlotCount => 1;

        protected override void OnAnimationFrameChanged(int newFrame, int previousFrame)
        {

        }
    }

    public class DecayChamberItemTooltip : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.LesionStation;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            // Replace any vanilla tooltip lines with ours.
            tooltips.RemoveAll(t => t.Mod == "Terraria" && t.Name.StartsWith("Tooltip"));

            int insertAt = tooltips.FindLastIndex(t => t.Mod == "Terraria" && t.Name == "ItemName") + 1;
            tooltips.Insert(insertAt + 1, new TooltipLine(Mod, "FactorrariaDecayChamber", "Used to instill evil in metals"));
        }
    }
}