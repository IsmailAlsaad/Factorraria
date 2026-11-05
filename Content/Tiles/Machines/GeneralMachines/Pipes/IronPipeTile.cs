using Factorraria.Common.Liquids;
using Factorraria.Content.Items.Liquids.Pipes;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Pipes
{
    public class IronPipeTile : PipeTileBase
    {
        protected override float MaxFlowRate => 40f * 3600f;   // placeholder balance: twice the lead pipe
        protected override PipeMaterial Material => PipeMaterial.Iron;
        protected override int PipeDustId => DustID.Iron;

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            RegisterItemDrop(ModContent.ItemType<IronPipeItem>());
        }
    }
}