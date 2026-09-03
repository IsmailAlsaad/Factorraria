using Factorraria.Common.UI;
using Factorraria.Content.Tiles.Liquids.Motors;
using Factorraria.Content.Tiles.Machines.Autohammer;
using Factorraria.Content.Tiles.Machines.Furnace;
using Factorraria.Content.Tiles.Machines.GelBurner;
using Factorraria.Content.Tiles.Machines.Solidifier;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Machines
{
    public class MachineRegisterationSystem : ModSystem
    {
        public override void Load()
        {
            // --- Register every machine's UI here
            MachineUIRegistry.Register(TileID.Furnaces, new FurnaceUIState());
            // MachineUIRegistry.Register(TileID.Autohammer, new AutohammerUIState());
            // etc...
        }

        public override void PostSetupContent()
        {
            // --- Register every machine's Texture here
            MachineVisualRegistry.Register<FurnaceTileEntity>(TileID.Furnaces,
                "Factorraria/Content/Tiles/Machines/General Machines/Furnace/Furnace_On",
                "Factorraria/Content/Tiles/Machines/General Machines/Furnace/Furnace_Off");

            MachineVisualRegistry.Register<AutohammerTileEntity>(TileID.Autohammer,
                "Factorraria/Content/Tiles/Machines/Electrical Consumers/Autohammer/Autohammer_On",
                "Factorraria/Content/Tiles/Machines/Electrical Consumers/Autohammer/Autohammer_Off");

            MachineVisualRegistry.Register<SolidifierTileEntity>(TileID.Solidifier,
                "Factorraria/Content/Tiles/Machines/Electrical Consumers/Solidifier/Solidifier_On",
                "Factorraria/Content/Tiles/Machines/Electrical Consumers/Solidifier/Solidifier_Off");

            MachineVisualRegistry.Register<GelBurnerTileEntity>(TileID.SteampunkBoiler,
                "Factorraria/Content/Tiles/Machines/Electrical Producers/GelBurner/GelBurner_On",
                "Factorraria/Content/Tiles/Machines/Electrical Producers/GelBurner/GelBurner_Off");

            MachineVisualRegistry.Register<MotorMK1TileEntity>(ModContent.TileType<MotorMK1Tile>(),
                "Factorraria/Content/Tiles/Liquids/Motors/MotorMK1_On",
                "Factorraria/Content/Tiles/Liquids/Motors/MotorMK1_Off");
        }

        public override void PostAddRecipes()
        {
            // --- Register every machine's Recipes here
            FurnaceRecipeRegistry.BuildFromExistingRecipes();
            // AutohammerRecipeRegistry.BuildFromExistingRecipes();
            // etc...
        }

        public override void Unload()
        {
            MachineVisualRegistry.Definitions.Clear();
        }
    }
}