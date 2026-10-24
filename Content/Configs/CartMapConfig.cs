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
        // Fractions work: the outermost ring of each colour is drawn partly transparent (1.5 = one full pixel plus a half-strength one).
        [DefaultValue(1.65f)]
        [Range(0f, 4f)]
        [Increment(0.05f)]
        [Slider]
        public float OutlineThickness = 1f;

        // Where the none / all / motor button sits on the fullscreen map, in pixels from the BOTTOM-RIGHT corner of the screen
        // (ButtonX = gap to the right edge, ButtonY = gap to the bottom edge).
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

        // The Minecart and Mechanical Cart item sprites face LEFT (every other skin faces right), so their icon is flipped the other way.
        // Untick this if you would rather have them drawn like the rest.
        [DefaultValue(true)]
        public bool FlipLeftFacingSkins = true;

        // A town NPC riding in a cart is shown in the cart's icon, so its normal head icon is hidden on the map while it is carried.
        [DefaultValue(true)]
        public bool HideCarriedNpcHead = true;

        public override void OnChanged()
        {
            CartMapIcons.Rebuild();
        }
    }
}