using Factorraria.Common.Knowledge;
using Factorraria.Content.Configs;
using Terraria.ModLoader;

namespace Factorraria.Common.Machines
{
    /// <summary>
    /// One place that answers "should the player see this recipe in a machine browser?".
    /// Today: only recipes the world has learned, plus a debug reveal-all. Kept as its own class so a
    /// later phase can add more rules (creative mode, knowledge items, ...) without touching the UI.
    /// </summary>
    public static class RecipeVisibility
    {
        public static bool IsVisible(RecipeOutputGroup group) =>
            RecipeKnowledgeSystem.IsKnown(group) ||
            ModContent.GetInstance<FurnaceOffsetConfig>().RevealAllRecipes;   // debug: show everything
    }
}