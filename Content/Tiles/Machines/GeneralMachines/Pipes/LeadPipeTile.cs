using Factorraria.Common.Liquids;
using Factorraria.Content.Items.Liquids.Pipes;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Pipes
{
    [LegacyName("PipeMK1Tile")]
    public class LeadPipeTile : PipeTileBase
    {
        protected override float MaxFlowRate => 20f * 3600f; // your example number
        protected override PipeMaterial Material => PipeMaterial.Lead;

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            RegisterItemDrop(ModContent.ItemType<LeadPipeItem>());
        }
    }
}
