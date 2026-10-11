using Factorraria.Common.Machines;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.MechanicalAssembler
{
    public class MechanicalAssemblerTileEntity : ElectricConsumerMachine
    {
        public override int ValidTileType => ModContent.TileType<MechanicalAssemblerTile>();
        public override RecipeBook Recipes => MechanicalAssemblerRecipeRegistry.Book;
        public override float PowerDemand => 200f;
        protected override int OutputSlotCount => 1;
    }
}