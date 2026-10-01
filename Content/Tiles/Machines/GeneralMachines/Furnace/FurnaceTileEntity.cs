using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace
{
    public class FurnaceTileEntity : BaseMachine
    {
        public override int ValidTileType => TileID.Furnaces;
        protected override FuelTable AcceptedFuels => FurnaceRecipeRegistry.Fuels;
        public override RecipeBook Recipes => FurnaceRecipeRegistry.Book;
        protected override int OutputSlotCount => 1;
    }
}