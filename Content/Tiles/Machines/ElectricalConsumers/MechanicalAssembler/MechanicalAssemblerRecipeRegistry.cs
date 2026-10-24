using Factorraria.Common.Machines;
using Factorraria.Content.Items.Liquids.Motors;
using Factorraria.Content.Items.Liquids.Pipes;
using Factorraria.Content.Items.Materials;
using Factorraria.Content.Items.Statues;
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
            // Meteor Head Statue
            Recipe.Create(ModContent.ItemType<MeteorHeadStatueItem>(), 1)
                .AddIngredient(ItemID.Meteorite, 15)
                .AddIngredient(ItemID.StoneBlock, 50)
                .AddTile(TileID.HeavyWorkBench)
                .Register();

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

            // Lead pipe
            Recipe.Create(ModContent.ItemType<PipeMK1Item>(), 2)
                .AddIngredient(ItemID.LeadBar, 3)
                .AddTile(TileID.HeavyWorkBench)
                .Register();

            // Lead motor
            Recipe.Create(ModContent.ItemType<MotorMK1Item>(), 1)
                .AddIngredient(ModContent.ItemType<SimpleCircuitItem>(), 2)
                .AddIngredient(ModContent.ItemType<PipeMK1Item>(), 2)
                .AddTile(TileID.HeavyWorkBench)
                .Register();

            //// Iron pipe
            //Recipe.Create(ModContent.ItemType<GoldWireItem>(), 2)
            //    .AddIngredient(ModContent.ItemType<GoldPlateItem>(), 1)
            //    .AddIngredient(ModContent.ItemType<TinWireItem>(), 6)
            //    .AddTile(TileID.HeavyWorkBench)
            //    .Register();
            //Recipe.Create(ModContent.ItemType<GoldWireItem>(), 2)
            //    .AddIngredient(ModContent.ItemType<GoldPlateItem>(), 1)
            //    .AddIngredient(ModContent.ItemType<CopperWireItem>(), 6)
            //    .AddTile(TileID.HeavyWorkBench)
            //    .Register();

            Book.Add(new CustomRecipe(
                new List<Item> {
                    new Item(ModContent.ItemType<SteelBarItem>(), 1)},
                new Item(ModContent.ItemType<SteelScrewItem>(), 3)));
        }
    }
}