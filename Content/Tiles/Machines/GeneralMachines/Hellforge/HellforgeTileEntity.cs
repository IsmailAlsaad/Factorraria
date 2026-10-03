using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Hellforge
{
    public class HellforgeTileEntity : BaseMachine
    {
        public override int ValidTileType => TileID.Hellforge;
        public override RecipeBook Recipes => HellforgeRecipeRegistry.Book;

        // One liquid-fuel tank (lava). No item-fuel slot, so ingredient slots start at 0.
        protected override LiquidFuelTable AcceptedLiquidFuels => HellforgeRecipeRegistry.LiquidFuels;

        protected override int OutputSlotCount => 1;

        public override void Update()
        {
            base.Update();

            if (InputLiquids[0].Amount == 0f)
            {
                InputLiquids[0] =  new LiquidStack(LiquidTypeRegistry.Lava,1f);
            }
        }
    }
}
