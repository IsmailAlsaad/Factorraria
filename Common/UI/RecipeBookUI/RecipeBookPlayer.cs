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
            tag["buttonOffsetX"] = ButtonOffset.Value.X;
            tag["buttonOffsetY"] = ButtonOffset.Value.Y;
        }

        public override void LoadData(TagCompound tag)
        {
            ButtonOffset = tag.ContainsKey("buttonOffsetX")
                ? new Vector2(tag.GetFloat("buttonOffsetX"), tag.GetFloat("buttonOffsetY"))
                : null;
        }
    }
}