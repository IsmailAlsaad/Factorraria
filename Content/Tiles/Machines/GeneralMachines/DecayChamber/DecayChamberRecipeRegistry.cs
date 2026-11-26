using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Content.Items.Materials;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.DecayChamber
{
    public static class DecayChamberRecipeRegistry
    {
        public static readonly RecipeBook Book = new();

        const float LiquidPerCraft = 50f;

        public static void BuildRecipes()
        {
            Book.Clear();

            // Crimtane bar
            AddInfusion(ItemID.GoldBar, 1, LiquidTypeRegistry.CrimsonWater, ItemID.CrimtaneBar);
            AddInfusion(ItemID.PlatinumBar, 1, LiquidTypeRegistry.CrimsonWater, ItemID.CrimtaneBar);

            AddInfusion(ItemID.TungstenBar, 5, LiquidTypeRegistry.CrimsonWater, ItemID.CrimtaneBar);
            AddInfusion(ItemID.SilverBar, 5, LiquidTypeRegistry.CrimsonWater, ItemID.CrimtaneBar);
            AddInfusion(ModContent.ItemType<PureSiliconItem>(), 5, LiquidTypeRegistry.CrimsonWater, ItemID.CrimtaneBar);

            AddInfusion(ItemID.TinBar, 10, LiquidTypeRegistry.CrimsonWater, ItemID.CrimtaneBar);
            AddInfusion(ItemID.CopperBar, 10, LiquidTypeRegistry.CrimsonWater, ItemID.CrimtaneBar);

            // Demonite bar
            AddInfusion(ItemID.GoldBar, 1, LiquidTypeRegistry.CorruptionWater, ItemID.DemoniteBar);
            AddInfusion(ItemID.PlatinumBar, 1, LiquidTypeRegistry.CorruptionWater, ItemID.DemoniteBar);

            AddInfusion(ItemID.TungstenBar, 5, LiquidTypeRegistry.CorruptionWater, ItemID.DemoniteBar);
            AddInfusion(ItemID.SilverBar, 5, LiquidTypeRegistry.CorruptionWater, ItemID.DemoniteBar);
            AddInfusion(ModContent.ItemType<PureSiliconItem>(), 5, LiquidTypeRegistry.CorruptionWater, ItemID.DemoniteBar);

            AddInfusion(ItemID.TinBar, 10, LiquidTypeRegistry.CorruptionWater, ItemID.DemoniteBar);
            AddInfusion(ItemID.CopperBar, 10, LiquidTypeRegistry.CorruptionWater, ItemID.DemoniteBar);

            Book.Rebuild();
        }

        static void AddInfusion(int inputBar, int count, int liquidType, int outputBar)
        {
            Book.Add(new CustomRecipe()
                .WithInput(inputBar, count)
                .WithLiquidInput(liquidType, LiquidPerCraft)
                .WithOutput(outputBar, 1));
        }
    }
}