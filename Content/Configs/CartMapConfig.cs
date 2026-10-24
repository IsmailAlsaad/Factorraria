using Factorraria.Common.Carts;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Factorraria.Content.Configs
{
    // Cart map look (client-side: every player picks their own). Changing a value rebuilds the cached icons straight away.
    public class CartMapConfig : ModConfig
    {
        public static CartMapConfig Current => ModContent.GetInstance<CartMapConfig>();

        public override ConfigScope Mode => ConfigScope.ClientSide;

        // Size of a cart icon on the map, 1 = the item sprite at its normal size. The icon grows a little when the mouse is over it.
        [DefaultValue(0.85f)]
        [Range(0.2f, 2f)]
        [Increment(0.05f)]
        [Slider]
        public float IconSize = 0.6f;

        // Pixels of white, then the same number of black, around each icon. 0 = no outline.
        [DefaultValue(1)]
        [Range(0, 4)]
        [Slider]
        public float OutlineThickness = 1;

        // Where the none / all / motor button sits on the fullscreen map, in pixels from the top-left corner of the screen.
        [DefaultValue(24)]
        [Range(0, 1600)]
        [Slider]
        public int ButtonX = 24;

        [DefaultValue(24)]
        [Range(0, 1000)]
        [Slider]
        public int ButtonY = 24;

        // Tick this if the icons face the wrong way (the icon follows the cart's facing; this flips it).
        [DefaultValue(false)]
        public bool InvertIconDirection = false;

        public override void OnChanged()
        {
            CartMapIcons.Rebuild();
        }
    }
}