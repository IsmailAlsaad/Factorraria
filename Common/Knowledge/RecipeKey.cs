using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// A stable, save-safe identifier for one recipe group (one output type).
    /// Uses the item's full internal name (e.g. "Terraria/IronBar" or "Factorraria/SteelBarItem"),
    /// never its runtime id, so learned recipes survive mod load-order changes and localization.
    /// </summary>
    public static class RecipeKey
    {
        public static string For(RecipeOutputGroup group) => For(group.Key);

        public static string For(RecipeOutputKey key) =>
            key.IsLiquid
                ? "l|" + key.Id                       // liquid ids come from the append-only LiquidTypeRegistry
                : "i|" + Terraria.ID.ItemID.Search.GetName(key.Id);
    }
}