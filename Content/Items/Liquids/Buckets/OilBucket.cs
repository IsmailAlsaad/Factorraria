using Factorraria.Content.Tiles.Liquids.CustomLiquids;
using ModLiquidExampleMod.Content.Items;
using ModLiquidLib.ID;
using ModLiquidLib.ModLoader;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Liquids.Buckets
{
    public class OilBucket : BucketBase
    {
        public OilBucket()
        {
            BucketLiquidType = LiquidLoader.LiquidType<OilLiquid>();
        }

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
            ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.LavaBucket;
            LiquidID_TLmod.Sets.CreateLiquidBucketItem[LiquidLoader.LiquidType<OilLiquid>()] = Type;
        }
    }
}
