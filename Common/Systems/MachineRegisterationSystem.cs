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
using Factorraria.Content.Tiles.Machines.GeneralMachines.DecayChamber;
using Factorraria.Content.Tiles.Machines.GeneralMachines.Hellforge;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.Autohammer;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.Extractinator;

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
            MachineUIRegistry.Register(TileID.Extractinator, new ExtractinatorUIState());
            MachineUIRegistry.Register(TileID.LesionStation, new DecayChamberUIState());
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


            MachineVisualRegistry.Register<DecayChamberTileEntity>(TileID.LesionStation,
                "Factorraria/Content/Tiles/Machines/GeneralMachines/DecayChamber/DecayChamber_On",
                "Factorraria/Content/Tiles/Machines/GeneralMachines/DecayChamber/DecayChamber_Off");


            MachineVisualRegistry.Register<GelBurnerTileEntity>(TileID.SteampunkBoiler,
                "Factorraria/Content/Tiles/Machines/ElectricalProducers/GelBurner/GelBurner_On",
                "Factorraria/Content/Tiles/Machines/ElectricalProducers/GelBurner/GelBurner_Off");


            MachineVisualRegistry.Register<ExtractinatorTileEntity>(TileID.Extractinator,
                "Factorraria/Content/Tiles/Machines/ElectricalConsumers/Extractinator/Extractinator_On",
                "Factorraria/Content/Tiles/Machines/ElectricalConsumers/Extractinator/Extractinator_Off");


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
            DecayChamberRecipeRegistry.BuildRecipes();
            AutohammerRecipeRegistry.BuildRecipes();
            ExtractinatorRecipeRegistry.BuildRecipes();


            // Recipe discovery: lets scrolls roll a recipe and resolve a saved key back to (machine, group).
            RecipeCatalog.Clear();
            RecipeCatalog.Register("Furnace", FurnaceRecipeRegistry.Book);
            RecipeCatalog.Register("Hellforge", HellforgeRecipeRegistry.Book);
            RecipeCatalog.Register("Solidifier", SolidifierRecipeRegistry.Book);
            RecipeCatalog.Register("IceMachine", IceMachineRecipeRegistry.Book);
            RecipeCatalog.Register("LiquidDistillator", LiquidDistillatorRecipeRegistry.Book);
            RecipeCatalog.Register("DecayChamber", DecayChamberRecipeRegistry.Book);
            RecipeCatalog.Register("Autohammer", AutohammerRecipeRegistry.Book);

            // Auto-fill recipe times: any recipe without its own TakesTicks gets the time of the machine that owns its book,
            // so the recipe book always has a time to show. Runs on every load, nothing is saved. If two machines ever
            // share one book, the first machine found wins (none do today).
            int machinesWithRecipes = 0;
            foreach (BaseMachine machine in ModContent.GetContent<BaseMachine>())
            {
                RecipeBook machineBook = machine.Recipes;
                if (machineBook == null) continue;      // motors, burners, ... have no recipe book
                machinesWithRecipes++;
                foreach (CustomRecipe recipe in machineBook.All)
                    recipe.DurationTicks ??= machine.ResolveRecipeDuration(recipe);
            }
            if (machinesWithRecipes == 0)
                Mod.Logger.Warn("Recipe time auto-fill found no machine templates; recipe times will be missing in the book.");

            // Reverse index itemType -> recipes, for the "holding an ingredient" state. Needs the catalog above.
            RecipeVisibility.BuildIngredientIndex();

            // Scroll pools (configured in ScrollPoolDefinitions). Needs the catalog above to validate entries.
            ScrollPools.Clear();
            ScrollPoolDefinitions.Register();
            ScrollPools.Validate(msg => Mod.Logger.Warn(msg));
        }

        public override void Unload()
        {
            RecipeCatalog.Clear();
            RecipeVisibility.ClearIndex();
            ScrollPools.Clear();
            MachineVisualRegistry.Definitions.Clear();
        }
    }
}