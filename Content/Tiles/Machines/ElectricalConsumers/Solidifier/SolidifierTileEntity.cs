using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.Solidifier
{
    public class SolidifierTileEntity : ElectricConsumerMachine
    {
        public override int ValidTileType => TileID.Solidifier;
        public override float PowerDemand => 100f;

        public override RecipeBook Recipes => SolidifierRecipeRegistry.Book;

        protected override int InputLiquidCount => 2;
        protected override int OutputSlotCount => 1;
    }
}
