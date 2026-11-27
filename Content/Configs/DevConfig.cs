using Microsoft.Xna.Framework;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Factorraria.Content.Configs
{
    // Developer settings, client-side so a change shows at once. Everything a player would tune is in the other configs; this one only
    // holds the debug toggles and two animation tweaks. EnableDebugs switches on every debug overlay and debug chat line.
    public class DevConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [DefaultValue(false)]
        public bool EnableDebugs;

        // Recipe Discovery debug: show EVERY recipe in machine browsers instead of only learned ones.
        [DefaultValue(false)]
        public bool RevealAllRecipes;

        // Where the fire icon's fill starts (0 = empty, 1 = full) so a machine with a little progress already shows some flame.
        [DefaultValue(0.15f)]
        [Range(0f, 1f)]
        public float fireCropOffset = 0.15f;

        // Machine animation frame advances once per this many ticks (bigger = slower).
        [DefaultValue(4)]
        [Range(1, 32)]
        public int animationSpeedDivider = 4;
    }
}