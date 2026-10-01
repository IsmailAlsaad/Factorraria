using Factorraria.Common.Machines;
using Terraria;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine
{
    public class IceMachineTileEntity : ElectricConsumerMachine
    {
        public override int ValidTileType => TileID.IceMachine;
        public override float PowerDemand => 100f;

        public override RecipeBook Recipes => IceMachineRecipeRegistry.Book;

        protected override int InputLiquidCount => 1;
        protected override int OutputSlotCount => 1;

        protected override bool CanStartCraft(CustomRecipe recipe)
        {
            return InsideBiome == MachineBiome.Snow;
        }
    }
}
