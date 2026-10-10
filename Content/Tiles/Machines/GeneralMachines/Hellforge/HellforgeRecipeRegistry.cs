using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Content.Items.Materials;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Hellforge
{
    public static class HellforgeRecipeRegistry
    {
        public static readonly RecipeBook Book = new();
        public static readonly LiquidFuelTable LiquidFuels = new();

        public static void BuildRecipes()
        {
            Book.Clear();
            Book.ImportVanillaRecipes(TileID.Hellforge);
            RegisterManualRecipes();

            Book.ImportVanillaRecipes(TileID.Furnaces);
            FurnaceRecipeRegistry.RegisterManualRecipes(Book);

            // Disable the vanilla recipe for Demonite and Crimtane bars
            Book.DisableRecipe(ItemID.DemoniteBar);
            Book.DisableRecipe(ItemID.CrimtaneBar);

            Book.Rebuild();

            LiquidFuels.Clear();
            RegisterLiquidFuels();
        }

        static void RegisterManualRecipes()
        {
            Book.Add(new CustomRecipe(
                new List<Item> { 
                    new Item(ItemID.IronBar, 2),
                    new Item(ModContent.ItemType<CokeItem>(), 5)},
                new Item(ModContent.ItemType<SteelBarItem>(), 1)
                ));
        }

        static void RegisterLiquidFuels()
        {
            // 75 lava = 1 fuel unit, and the forge burns 1 unit per craft -> 75 lava per craft.
            // To also accept another liquid, add a line, e.g. LiquidFuels.Add(LiquidTypeRegistry.Fuel, 10f);
            LiquidFuels.Add(LiquidTypeRegistry.Lava, 75f);
        }
    }
}
