using Factorraria.Common.Liquids;
using Factorraria.Content.Items.Liquids.Pipes;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Liquids.Pipes
{
    public class PipeMK1Tile : PipeTileBase
    {
        protected override float MaxFlowRate => 100f; // your example number

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            RegisterItemDrop(ModContent.ItemType<PipeMK1Item>());
        }
    }
}
