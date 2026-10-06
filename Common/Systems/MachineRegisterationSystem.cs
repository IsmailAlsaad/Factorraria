using Factorraria.Common.Knowledge;
using Factorraria.Common.UI;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Motors;
using Factorraria.Content.Tiles.Machines.Autohammer;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace;
using Factorraria.Content.Tiles.Machines.GelBurner;
using Factorraria.Content.Tiles.Machines.Solidifier;
using Terraria.ID;
using Terraria.ModLoader;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.Solidifier;

using Factorraria.Content.Tiles.Machines.ElectricalConsumers.IceMachine;
using Factorraria.Content.Tiles.Machines.GeneralMachines.LiquidDistillator;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Hellforge;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.Autohammer;

namespace Factorraria.Common.Machines
{
    public class MachineRegisterationSystem : ModSystem
    {
        public override void Load()
        {
            // Register every machine's UI here
            MachineUIRegistry.Register(TileID.Furnaces, new FurnaceUIState());
            MachineUIRegistry.Register(TileID.Solidifier, new SolidifierUIState());
            MachineUIRegistry.Register(TileID.SteampunkBoiler, new GelBurnerUIState());
            MachineUIRegistry.Register(TileID.Autohammer, new AutohammerUIState());
            MachineUIRegistry.Register(TileID.IceMachine, new IceMachineUIState());
            MachineUIRegistry.Register(TileID.ImbuingStation, new LiquidDistillatorUIState());
            MachineUIRegistry.Register(TileID.Hellforge, new HellforgeUIState());
            // etc...
        }

        public override void PostSetupContent()
        {
            // Register every machine's Texture here
            MachineVisualRegistry.Register<FurnaceTileEntity>(TileID.Furnaces,
                "Factorraria/Content/Tiles/Machines/GeneralMachines/Furnace/Furnace_On",
                "Factorraria/Content/Tiles/Machines/GeneralMachines/Furnace/Furnace_Off");


            MachineVisualRegistry.Register<AutohammerTileEntity>(TileID.Autohammer,
                "Factorraria/Content/Tiles/Machines/ElectricalConsumers/Autohammer/Autohammer_On",
                "Factorraria/Content/Tiles/Machines/ElectricalConsumers/Autohammer/Autohammer_Off");


            MachineVisualRegistry.Register<SolidifierTileEntity>(TileID.Solidifier,
                "Factorraria/Content/Tiles/Machines/ElectricalConsumers/Solidifier/Solidifier_On",
                "Factorraria/Content/Tiles/Machines/ElectricalConsumers/Solidifier/Solidifier_Off");
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.Solidifier,
                "Factorraria/Content/Tiles/Machines/ElectricalConsumers/Solidifier/Solidifier_On_FirstLiquid",
                entity => entity.InputLiquids[0]);
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.Solidifier,
                "Factorraria/Content/Tiles/Machines/ElectricalConsumers/Solidifier/Solidifier_On_SecondLiquid",
                entity => entity.InputLiquids[1]);


            MachineVisualRegistry.Register<IceMachineTileEntity>(TileID.IceMachine,
                "Factorraria/Content/Tiles/Machines/GeneralMachines/IceMachine/IceMachine_On",
                "Factorraria/Content/Tiles/Machines/GeneralMachines/IceMachine/IceMachine_Off");
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.IceMachine,
                "Factorraria/Content/Tiles/Machines/GeneralMachines/IceMachine/IceMachine_On_FirstLiquid",
                entity => entity.InputLiquids[0]);


            MachineVisualRegistry.Register<LiquidDistillatorTileEntity>(TileID.ImbuingStation,
                "Factorraria/Content/Tiles/Machines/GeneralMachines/LiquidDistillator/LiquidDistillator_On",
                "Factorraria/Content/Tiles/Machines/GeneralMachines/LiquidDistillator/LiquidDistillator_Off");
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.ImbuingStation,
                "Factorraria/Content/Tiles/Machines/GeneralMachines/LiquidDistillator/LiquidDistillator_On_FirstLiquid",
                entity => entity.InputLiquids[0]);
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.ImbuingStation,
                "Factorraria/Content/Tiles/Machines/GeneralMachines/LiquidDistillator/LiquidDistillator_On_SecondLiquid",
                entity => entity.OutputLiquids[0]);


            MachineVisualRegistry.Register<HellforgeTileEntity>(TileID.Hellforge,
                "Factorraria/Content/Tiles/Machines/GeneralMachines/Hellforge/Hellforge_On",
                "Factorraria/Content/Tiles/Machines/GeneralMachines/Hellforge/Hellforge_Off");


            MachineVisualRegistry.Register<GelBurnerTileEntity>(TileID.SteampunkBoiler,
                "Factorraria/Content/Tiles/Machines/ElectricalProducers/GelBurner/GelBurner_On",
                "Factorraria/Content/Tiles/Machines/ElectricalProducers/GelBurner/GelBurner_Off");


            MachineVisualRegistry.Register<MotorMK1TileEntity>(ModContent.TileType<MotorMK1Tile>(),
                "Factorraria/Content/Tiles/Machines/GeneralMachines/Motors/MotorMK1_On",
                "Factorraria/Content/Tiles/Machines/GeneralMachines/Motors/MotorMK1_Off");
        }

        public override void PostAddRecipes()
        {
            // Register every machine's Recipes here
            FurnaceRecipeRegistry.BuildRecipes();
            HellforgeRecipeRegistry.BuildRecipes();
            SolidifierRecipeRegistry.BuildRecipes();
            IceMachineRecipeRegistry.BuildRecipes();
            LiquidDistillatorRecipeRegistry.BuildRecipes();
            AutohammerRecipeRegistry.BuildRecipes();

            // Recipe discovery: the world starts out knowing every recipe that already existed
            // before this system was added, so nothing the player could already make disappears.
            RecipeKnowledgeSystem.LearnBook(FurnaceRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(HellforgeRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(SolidifierRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(IceMachineRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(LiquidDistillatorRecipeRegistry.Book);
            RecipeKnowledgeSystem.LearnBook(AutohammerRecipeRegistry.Book);
            // etc...

            // Recipe discovery: lets scrolls roll a recipe and resolve a saved key back to (machine, group).
            RecipeCatalog.Clear();
            RecipeCatalog.Register("Furnace", FurnaceRecipeRegistry.Book);
            RecipeCatalog.Register("Hellforge", HellforgeRecipeRegistry.Book);
            RecipeCatalog.Register("Solidifier", SolidifierRecipeRegistry.Book);
            RecipeCatalog.Register("IceMachine", IceMachineRecipeRegistry.Book);
            RecipeCatalog.Register("LiquidDistillator", LiquidDistillatorRecipeRegistry.Book);
            RecipeCatalog.Register("Autohammer", AutohammerRecipeRegistry.Book);
        }

        public override void Unload()
        {
            RecipeCatalog.Clear();
            MachineVisualRegistry.Definitions.Clear();
        }
    }
}