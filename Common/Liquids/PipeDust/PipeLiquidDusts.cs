using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Liquids
{
    // The dust the pipes spray for a liquid. It behaves exactly like vanilla water dust (UpdateType copies its update), but the picture is
    // ours: Common/Liquids/PipeDust/<ClassName>.png, 10x30 (three 10x10 frames stacked, the game picks one at random). The art carries the
    // colour, so tune it there. PipeFlowVisualSystem.PipeDustTypeFor says which liquid uses which.
    public abstract class PipeLiquidDust : ModDust
    {
        // Until the PNG exists the game's own dust sheet is used, so a missing picture is a wrong look and not a crash on load.
        public override string Texture
        {
            get
            {
                string path = "Factorraria/Common/Liquids/PipeDust/" + GetType().Name;
                return ModContent.HasAsset(path) ? path : "Terraria/Images/Dust";
            }
        }

        public override void SetStaticDefaults()
        {
            UpdateType = DustID.Water;
        }
    }

    public class LavaPipeDust : PipeLiquidDust { }
    public class HoneyPipeDust : PipeLiquidDust { }
    public class ShimmerPipeDust : PipeLiquidDust { }
    public class OilPipeDust : PipeLiquidDust { }
    public class FuelPipeDust : PipeLiquidDust { }
}