using Factorraria.Common.Machines;
using Factorraria.Content.Items.Materials;
using Factorraria.Content.Items.Wires;
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

            // Steel Screw
            Recipe.Create(ModContent.ItemType<SteelScrewItem>(), 3)
                .AddIngredient(ModContent.ItemType<SteelBarItem>(), 1)
                .AddTile(TileID.HeavyWorkBench)
                .Register();
            
            RegisterManualRecipes();

            Book.ImportVanillaRecipes(TileID.HeavyWorkBench);
            Book.ImportVanillaRecipes(TileID.WorkBenches, null, false);
            RegisterManualRecipes();

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
                .AddTile(TileID.HeavyWorkBench)
                .Register();
        }
    }
}