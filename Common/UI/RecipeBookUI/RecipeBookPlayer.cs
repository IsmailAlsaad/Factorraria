using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>Remembers where this character dragged the recipe book button (offset from the defense icon). Null = default placement.</summary>
    public class RecipeBookPlayer : ModPlayer
    {
        public Vector2? ButtonOffset;

        public override void SaveData(TagCompound tag)
        {
            if (!ButtonOffset.HasValue) return;
            // New key names: the old "buttonColOffset*" values were relative to the accessory column top and would be wrong now.
            tag["buttonDefOffsetX"] = ButtonOffset.Value.X;
            tag["buttonDefOffsetY"] = ButtonOffset.Value.Y;
        }

        public override void LoadData(TagCompound tag)
        {
            ButtonOffset = tag.ContainsKey("buttonDefOffsetX")
                ? new Vector2(tag.GetFloat("buttonDefOffsetX"), tag.GetFloat("buttonDefOffsetY"))
                : null;
        }
    }
}