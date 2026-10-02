using Factorraria.Common.Machines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.LiquidDistillator
{
    public class LiquidDistillatorTileEntity : BaseMachine
    {
        public override int ValidTileType => TileID.ImbuingStation;
        public override RecipeBook Recipes => LiquidDistillatorRecipeRegistry.Book;

        protected override int InputLiquidCount => 1;
        protected override int OutputLiquidCount => 1;
    }
}
