using Factorraria.Common.Liquids;
using Factorraria.Content.Items.Liquids.Pipes;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines
{
    public class PipeMK1Tile : PipeTileBase
    {
        protected override float MaxFlowRate => 20f * 3600f; // your example number

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            RegisterItemDrop(ModContent.ItemType<PipeMK1Item>());
        }
    }
}
