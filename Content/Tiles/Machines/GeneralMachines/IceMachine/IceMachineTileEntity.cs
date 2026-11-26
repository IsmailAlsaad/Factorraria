using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine
{
    public class IceMachineTileEntity : BaseMachine
    {
        public override int ValidTileType => TileID.IceMachine;
        public override RecipeBook Recipes => IceMachineRecipeRegistry.Book;

        protected override int InputLiquidCount => 1;
        protected override int OutputSlotCount => 1;

        // The tank takes any water (plain water and every biome variant), on top of whatever the recipes consume.
        // Only Snow Water matches a recipe (Snow Water -> Slush), so any other water just sits in the tank and nothing crafts.
        protected override bool AcceptsInputLiquid(int tankIndex, int liquidType) =>
            LiquidTypeRegistry.IsWater(liquidType) || base.AcceptsInputLiquid(tankIndex, liquidType);

        protected override bool CanStartCraft(CustomRecipe recipe)
        {
            return InsideBiome == MachineBiome.Snow;
        }

        protected override void OnAnimationFrameChanged(int newFrame, int previousFrame)
        {

        }
    }

    // The Ice Machine is the vanilla Ice Machine item, so its extra tooltip line is added through a GlobalItem (vanilla lines stay).
    public class IceMachineItemTooltip : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.IceMachine;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.RemoveAll(line => line.Mod == "Terraria" && line.Name.StartsWith("Tooltip"));
            tooltips.Add(new TooltipLine(Mod, "FactorrariaIceMachineCold", "Requires a cold environment to operate"));
        }
    }
}
