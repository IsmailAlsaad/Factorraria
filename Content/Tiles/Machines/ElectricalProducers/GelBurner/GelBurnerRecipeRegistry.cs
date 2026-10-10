using Factorraria.Common.Machines;
using Terraria;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GelBurner
{
    /// <summary>
    /// The one table of what the "burner" machines can burn and for how long. Shared by the Gel Burner tile and the
    /// Steampunk motor cart, so a fuel added here works in both. Units are ticks of burning (60 = 1 second).
    /// </summary>
    public static class GelBurnerRecipeRegistry
    {
        public static readonly FuelTable Fuels = CreateFuels();

        static FuelTable CreateFuels()
        {
            FuelTable table = new FuelTable()
                .Add(ItemID.Gel, 3 * 60)     // gel: 3 seconds
                .Add(ItemID.Coal, 10 * 60);   // coal: 10 seconds

            if (RecipeGroup.recipeGroups.TryGetValue(RecipeGroupID.Wood, out RecipeGroup woodGroup))
            {
                foreach (int woodID in woodGroup.ValidItems)
                {
                    table.Add(woodID, 5 * 60); // wood: 5 seconds
                }
            }
                    table.StackLimitRule = _ => 10;   // conveyors may buffer up to 10 of an item in the Gel Burner
            return table;
        }
    }
}