using Factorraria.Common.Machines;
using Factorraria.Content.Items.Materials;
using Factorraria.Content.Items.Wires;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.MechanicalAssembler
{
    public class MechanicalAssemblerRecipeRegistry
    {
        public static readonly RecipeBook Book = new();

        public static void BuildRecipes()
        {
            Book.Clear();

            RegisterManualRecipes();

            Book.ImportVanillaRecipes(TileID.HeavyWorkBench, null ,false);
            Book.ImportVanillaRecipes(TileID.WorkBenches, null, false);

            Book.Rebuild();
        }

        static void RegisterManualRecipes()
        {
            // Simple Circuit
            Recipe.Create(ModContent.ItemType<SimpleCircuitItem>(), 1)
                .AddIngredient(ModContent.ItemType<PureSiliconItem>(), 3)
                .AddIngredient(ModContent.ItemType<TinWireItem>(), 5)
                .AddTile(TileID.HeavyWorkBench)
                .Register();
            Recipe.Create(ModContent.ItemType<SimpleCircuitItem>(), 1)
                .AddIngredient(ModContent.ItemType<PureSiliconItem>(), 3)
                .AddIngredient(ModContent.ItemType<CopperWireItem>(), 5)
                .AddTile(TileID.HeavyWorkBench)
                .Register();

            // Gold Wire
            Recipe.Create(ModContent.ItemType<GoldWireItem>(), 2)
                .AddIngredient(ModContent.ItemType<GoldPlateItem>(), 1)
                .AddIngredient(ModContent.ItemType<TinWireItem>(), 6)
                .AddTile(TileID.HeavyWorkBench)
                .Register();
            Recipe.Create(ModContent.ItemType<GoldWireItem>(), 2)
                .AddIngredient(ModContent.ItemType<GoldPlateItem>(), 1)
                .AddIngredient(ModContent.ItemType<CopperWireItem>(), 6)
                .AddTile(TileID.HeavyWorkBench)
                .Register();

            Book.Add(new CustomRecipe(
                new List<Item> {
                    new Item(ModContent.ItemType<SteelBarItem>(), 1)},
                new Item(ModContent.ItemType<SteelScrewItem>(), 3)));
        }
    }
}