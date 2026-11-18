using Factorraria.Common.Harvester;
using Factorraria.Common.Machines;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.HarvesterBox
{
    // Hub calls for the Harvester Boxes: MachineRegisterationSystem.PostSetupContent calls RegisterVisuals, RecipeSystem.AddRecipes calls AddRecipes.
    public static class HarvesterBoxRegistration
    {
        public static void RegisterVisuals()
        {
            RegisterVisual<DaybloomHarvesterBoxTile, DaybloomHarvesterBoxTileEntity>();
            RegisterVisual<MoonglowHarvesterBoxTile, MoonglowHarvesterBoxTileEntity>();
            RegisterVisual<CorruptHarvesterBoxTile, CorruptHarvesterBoxTileEntity>();
            RegisterVisual<CrimsonHarvesterBoxTile, CrimsonHarvesterBoxTileEntity>();
            RegisterVisual<BlinkrootHarvesterBoxTile, BlinkrootHarvesterBoxTileEntity>();
            RegisterVisual<WaterleafHarvesterBoxTile, WaterleafHarvesterBoxTileEntity>();
            RegisterVisual<ShiverthornHarvesterBoxTile, ShiverthornHarvesterBoxTileEntity>();
            RegisterVisual<FireblossomHarvesterBoxTile, FireblossomHarvesterBoxTileEntity>();
        }

        // A machine tile has to be in the visual registry to get its entity (MachinePlacement.ServerPlace). A Harvester Box is drawn as a normal framed tile (the merge frames
        // come from the tile data), so DrawNormalTile switches the machine draw override off for it. The box has no On/Off look, so the same sheet fills both slots.
        static void RegisterVisual<TTile, TEntity>() where TTile : ModTile where TEntity : BaseMachine, new()
        {
            int type = ModContent.TileType<TTile>();
            string sheet = ModContent.GetInstance<TTile>().Texture;
            MachineVisualRegistry.Register<TEntity>(type, sheet, sheet);
            MachineVisualRegistry.Definitions[type].DrawNormalTile = true;
        }

        public static void AddRecipes()
        {
            AddRecipe<DaybloomHarvesterBoxItem>(HarvesterBoxKind.Daybloom);
            AddRecipe<MoonglowHarvesterBoxItem>(HarvesterBoxKind.Moonglow);
            AddRecipe<CorruptHarvesterBoxItem>(HarvesterBoxKind.Corrupt);
            AddRecipe<CrimsonHarvesterBoxItem>(HarvesterBoxKind.Crimson);
            AddRecipe<BlinkrootHarvesterBoxItem>(HarvesterBoxKind.Blinkroot);
            AddRecipe<WaterleafHarvesterBoxItem>(HarvesterBoxKind.Waterleaf);
            AddRecipe<ShiverthornHarvesterBoxItem>(HarvesterBoxKind.Shiverthorn);
            AddRecipe<FireblossomHarvesterBoxItem>(HarvesterBoxKind.Fireblossom);
        }

        // PLACEHOLDER recipe (Ismail decides the real one): the matching vanilla planter box plus iron or lead bars.
        static void AddRecipe<TItem>(HarvesterBoxKind kind) where TItem : ModItem
        {
            if (!ItemID.Search.TryGetId(HarvesterBoxKinds.PlanterItemName(kind), out int planterBox))
                return;

            Recipe.Create(ModContent.ItemType<TItem>())
                .AddIngredient(planterBox)
                .AddRecipeGroup(RecipeGroupID.IronBar, 4)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}