using Factorraria.Content.Liquids.Fuel;
using Factorraria.Content.Liquids.Oil;
using ModLiquidExampleMod.Content.Items;
using ModLiquidLib.ID;
using ModLiquidLib.ModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;

namespace Factorraria.Content.Items.Liquids.Buckets
{
    public class FuelBucket: BucketBase
    {
        public FuelBucket()
        {
            BucketLiquidType = LiquidLoader.LiquidType<FuelLiquid>();
        }

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
            ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.LavaBucket;
            LiquidID_TLmod.Sets.CreateLiquidBucketItem[LiquidLoader.LiquidType<FuelLiquid>()] = Type;
        }
    }
}
