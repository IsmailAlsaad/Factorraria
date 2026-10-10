using Factorraria.Common.Machines;
using Terraria.ID;

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
}