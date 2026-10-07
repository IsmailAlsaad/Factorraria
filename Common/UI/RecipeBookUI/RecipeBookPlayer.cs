using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>Remembers where this character dragged the recipe book button (offset from the accessory column anchor). Null = default placement.</summary>
    public class RecipeBookPlayer : ModPlayer
    {
        public Vector2? ButtonOffset;

        public override void SaveData(TagCompound tag)
        {
            if (!ButtonOffset.HasValue) return;
            tag["buttonColOffsetX"] = ButtonOffset.Value.X;
            tag["buttonColOffsetY"] = ButtonOffset.Value.Y;
        }

        public override void LoadData(TagCompound tag)
        {
            ButtonOffset = tag.ContainsKey("buttonColOffsetX")
                ? new Vector2(tag.GetFloat("buttonColOffsetX"), tag.GetFloat("buttonColOffsetY"))
                : null;
        }
    }
}