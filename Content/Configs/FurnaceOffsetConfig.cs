using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Factorraria.Content.Configs
{
    public class FurnaceOffsetConfig : ModConfig
    {
        // Client-side so it changes instantly without needing a server restart
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Slider]
        [Range(-100, 100)]
        public int OffsetX;

        [Slider]
        [Range(-100, 100)]
        public int OffsetY;

        public int animationOffset;

        public float fireCropOffset;

        [Slider]
        [Range(1, 120)]
        public int animationSpeedDivider;

        public bool EnableDebugs;

        [Slider]
        [Range(0f,32f)]
        public float VItemOffset;
    }
}