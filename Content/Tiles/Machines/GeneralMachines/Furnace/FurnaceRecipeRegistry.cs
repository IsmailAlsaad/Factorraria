using Factorraria.Common.Machines;
using Factorraria.Content.Items.Materials;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace
{
    public static class FurnaceRecipeRegistry
    {
        public static readonly RecipeBook Book = new();
        public static readonly FuelTable Fuels = new();

        public static void BuildRecipes()
        {
            Book.Clear();

            Book.ImportVanillaRecipes(TileID.Furnaces);
            RegisterManualRecipes(Book);

            // Disable the vanilla recipe for Demonite and Crimtane bars
            Book.DisableRecipe(ItemID.DemoniteBar);
            Book.DisableRecipe(ItemID.CrimtaneBar);

            Book.Rebuild();

            Fuels.Clear();
            RegisterManualFuels();
        }

        public static void RegisterManualRecipes(RecipeBook Book)
        {
            if (RecipeGroup.recipeGroups.TryGetValue(RecipeGroupID.Wood, out RecipeGroup woodGroup))
            {
                foreach (int itemID in woodGroup.ValidItems)
                {
                    Book.Add(new CustomRecipe(
                        new List<Item> { new Item(itemID, 1) },
                        new Item(ItemID.Coal, 1)));
                }
            }

            Book.Add(new CustomRecipe(
                new List<Item> { new Item(ModContent.ItemType<SiliconOreItem>(), 3) },
                new Item(ModContent.ItemType<PureSiliconItem>(), 1)));
        }

        static void RegisterManualFuels()
        {
            Fuels.Add(ItemID.Gel, 3);
            Fuels.Add(ItemID.Coal, 10);

            if (RecipeGroup.recipeGroups.TryGetValue(RecipeGroupID.Wood, out RecipeGroup woodGroup))
            {
                foreach (int itemID in woodGroup.ValidItems)
                {
                    Fuels.Add(itemID, 5); // wood: 5 seconds
                }
            }
        }
    }
}
