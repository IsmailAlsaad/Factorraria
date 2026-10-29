using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.SuperWeapons.MissileTank
{
    // The fuel tank of the missile (super weapons patch S2.2): a BaseMachine with no slots, no power and no recipes, only ONE input tank that
    // holds Fuel. It gets everything else from BaseMachine: pipes treat it as a sink (the tank is InputLiquids[0] with a Fuel-only filter),
    // bucket pour works through the same tank, and it is saved and synced like every machine (snapshot, light updates, detail channel).
    // The assembly check and the primed state arrive in patch S3.1 / S3.2 on this entity.
    public class MissileTankTileEntity : BaseMachine
    {
        public override int ValidTileType => ModContent.TileType<MissileTankTile>();

        // A liquid-fuel table that only knows Fuel is what makes the tank Fuel-only: it is the filter of InputLiquids[0].
        protected override LiquidFuelTable AcceptedLiquidFuels => MissileTankRegistry.LiquidFuels;

        // Tank size in liquid units; a bucket is 255, so this is about 19.6 buckets (open item O6).
        public float FuelCapacity = 5000f;

        protected override float InputTankCapacity(int tankIndex) => FuelCapacity;
    }
}