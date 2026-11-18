using Factorraria.Common.Machines;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // The powered entity of a Harvester Box. Phase 1: it only draws power and joins the grid (through custom wire on its tile, like every machine). The gears, the harvest
    // cycle and the drops come in later phases (docs/harvester_box/ROADMAP.md).
    public abstract class HarvesterBoxTileEntityBase<TTile> : ElectricConsumerMachine where TTile : ModTile
    {
        public override int ValidTileType => ModContent.TileType<TTile>();

        public override float PowerDemand => 10f;   // placeholder balance

        public override void Update()
        {
            base.Update();
            isWorking = true;   // draws power the whole time it is placed (the Phase 2 gears turn whenever the box is powered)
        }
    }
}