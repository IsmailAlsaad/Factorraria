using Microsoft.Xna.Framework;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Factorraria.Content.Configs
{
    public class FurnaceOffsetConfig : ModConfig
    {
        // Client-side so it changes instantly without needing a server restart
        public override ConfigScope Mode => ConfigScope.ClientSide;
        [Slider]
        [Range(-32f, 32f)]
        public float OffsetX;
        [Slider]
        [Range(-32f, 32f)]
        public float OffsetY;
        [Slider]
        [Range(-180f, 180f)]
        public float AngleOffset;
        [Slider]
        [Range(-180f, 180f)]
        public float AngleOffset2;

        public int animationOffset;

        public float fireCropOffset = 0.15f;

        public int animationSpeedDivider = 4;

        public bool EnableDebugs;

        [Slider]
        [Range(0f,32f)]
        public float VItemOffset;
    }
}