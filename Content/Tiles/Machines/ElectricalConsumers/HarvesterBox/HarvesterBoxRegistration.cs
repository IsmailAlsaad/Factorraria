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
    }
}