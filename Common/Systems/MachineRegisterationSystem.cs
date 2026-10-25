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
using Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.Autohammer;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.Extractinator;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.MechanicalAssembler;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;

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
            MachineUIRegistry.Register(TileID.ShimmerMonolith, new MagicMaterializerUIState());
            // ModTile machines register their UI in PostSetupContent (their tile ids don't exist yet here)
            // etc...
        }

        public override void PostSetupContent()
        {
            // Register every machine's Texture here
            string basePath = "Factorraria/Content/Tiles/Machines/";

            MachineVisualRegistry.Register<FurnaceTileEntity>(TileID.Furnaces,
                basePath + "GeneralMachines/Furnace/Furnace_On",
                basePath + "GeneralMachines/Furnace/Furnace_Off");


            MachineVisualRegistry.Register<AutohammerTileEntity>(TileID.Autohammer,
                basePath + "ElectricalConsumers/Autohammer/Autohammer_On",
                basePath + "ElectricalConsumers/Autohammer/Autohammer_Off");


            MachineVisualRegistry.Register<SolidifierTileEntity>(TileID.Solidifier,
                basePath + "ElectricalConsumers/Solidifier/Solidifier_On",
                basePath + "ElectricalConsumers/Solidifier/Solidifier_Off");
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.Solidifier,
                basePath + "ElectricalConsumers/Solidifier/Solidifier_On_FirstLiquid",
                entity => entity.InputLiquids[0]);
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.Solidifier,
                basePath + "ElectricalConsumers/Solidifier/Solidifier_On_SecondLiquid",
                entity => entity.InputLiquids[1]);


            MachineVisualRegistry.Register<IceMachineTileEntity>(TileID.IceMachine,
                basePath + "GeneralMachines/IceMachine/IceMachine_On",
                basePath + "GeneralMachines/IceMachine/IceMachine_Off");
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.IceMachine,
                basePath + "GeneralMachines/IceMachine/IceMachine_On_FirstLiquid",
                entity => entity.InputLiquids[0]);


            MachineVisualRegistry.Register<LiquidDistillatorTileEntity>(TileID.ImbuingStation,
                basePath + "GeneralMachines/LiquidDistillator/LiquidDistillator_On",
                basePath + "GeneralMachines/LiquidDistillator/LiquidDistillator_Off");
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.ImbuingStation,
                basePath + "GeneralMachines/LiquidDistillator/LiquidDistillator_On_FirstLiquid",
                entity => entity.InputLiquids[0]);
            MachineVisualRegistry.RegisterLiquidOverlay(TileID.ImbuingStation,
                basePath + "GeneralMachines/LiquidDistillator/LiquidDistillator_On_SecondLiquid",
                entity => entity.OutputLiquids[0]);


            MachineVisualRegistry.Register<HellforgeTileEntity>(TileID.Hellforge,
                basePath + "GeneralMachines/Hellforge/Hellforge_On",
                basePath + "GeneralMachines/Hellforge/Hellforge_Off");


            MachineVisualRegistry.Register<DecayChamberTileEntity>(TileID.LesionStation,
                basePath + "GeneralMachines/DecayChamber/DecayChamber_On",
                basePath + "GeneralMachines/DecayChamber/DecayChamber_Off");


            MachineVisualRegistry.Register<MagicMaterializerTileEntity>(TileID.ShimmerMonolith,
                basePath + "GeneralMachines/MagicMaterializer/MagicMaterializer_On_Active",
                basePath + "GeneralMachines/MagicMaterializer/MagicMaterializer_Off");
            // MagicMaterializer_On_Active is the Active look (starry background on); _On_Inactive is shown when the player cycled it off.
            MachineVisualRegistry.RegisterAlternateOn(TileID.ShimmerMonolith,
                basePath + "GeneralMachines/MagicMaterializer/MagicMaterializer_On_Inactive",
                machine => machine is MagicMaterializerTileEntity materializer && !materializer.BackgroundActive);


            MachineVisualRegistry.Register<GelBurnerTileEntity>(TileID.SteampunkBoiler,
                basePath + "ElectricalProducers/GelBurner/GelBurner_On",
                basePath + "ElectricalProducers/GelBurner/GelBurner_Off",
                basePath + "ElectricalProducers/GelBurner/GelBurner_Startup",
                basePath + "ElectricalProducers/GelBurner/GelBurner_Shutdown");


            MachineVisualRegistry.Register<ExtractinatorTileEntity>(TileID.Extractinator,
                basePath + "ElectricalConsumers/Extractinator/Extractinator_On",
                basePath + "ElectricalConsumers/Extractinator/Extractinator_Off");


            MachineVisualRegistry.Register<MotorMK1TileEntity>(ModContent.TileType<MotorMK1Tile>(),
                basePath + "GeneralMachines/Motors/MotorMK1_On",
                basePath + "GeneralMachines/Motors/MotorMK1_Off");


            MachineUIRegistry.Register(ModContent.TileType<MechanicalAssemblerTile>(), new MechanicalAssemblerUIState());
            MachineVisualRegistry.RegisterOverhang<MechanicalAssemblerTileEntity>(ModContent.TileType<MechanicalAssemblerTile>(),
                basePath + "ElectricalConsumers/MechanicalAssembler/MechanicalAssembler_On",
                basePath + "ElectricalConsumers/MechanicalAssembler/MechanicalAssembler_Off",
                footprintWidth: 3, footprintHeight: 3,
                originX: 3, originY: 2,
                onFrames: 5, offFrames: 1,
                yOffset: 2f);

            MachineUIRegistry.Register(ModContent.TileType<RoboticArmTile>(), new RoboticArmUIState());
            MachineVisualRegistry.Register<RoboticArmTileEntity>(ModContent.TileType<RoboticArmTile>(),
                basePath + "ElectricalConsumers/RoboticArm/RoboticArm_On",
                basePath + "ElectricalConsumers/RoboticArm/RoboticArm_Off");

            // Power poles: one shared UI (electric icon only). Canvas = 7 tiles wide, footprint in the middle column (originX 3), one frame each.
            MachineUIRegistry.Register(ModContent.TileType<WoodenPowerPoleTile>(), new PowerPoleUIState());
            MachineUIRegistry.Register(ModContent.TileType<PlatinumPowerPoleTile>(), new PowerPoleUIState());

            MachineVisualRegistry.RegisterOverhang<WoodenPowerPoleTileEntity>(ModContent.TileType<WoodenPowerPoleTile>(),
                basePath + "ElectricalConsumers/PowerPoles/WoodenPowerPole",
                basePath + "ElectricalConsumers/PowerPoles/WoodenPowerPole",
                footprintWidth: 1, footprintHeight: 14,
                originX: 3, originY: 0);

            MachineVisualRegistry.RegisterOverhang<PlatinumPowerPoleTileEntity>(ModContent.TileType<PlatinumPowerPoleTile>(),
                basePath + "ElectricalConsumers/PowerPoles/PlatinumPowerPole",
                basePath + "ElectricalConsumers/PowerPoles/PlatinumPowerPole",
                footprintWidth: 1, footprintHeight: 16,
                originX: 3, originY: 0);
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
            MechanicalAssemblerRecipeRegistry.BuildRecipes();
            MagicMaterializerRegistry.BuildRecipes();


            // Recipe discovery: lets scrolls roll a recipe and resolve a saved key back to (machine, group).
            RecipeCatalog.Clear();
            RecipeCatalog.Register("Furnace", FurnaceRecipeRegistry.Book);
            RecipeCatalog.Register("Hellforge", HellforgeRecipeRegistry.Book);
            RecipeCatalog.Register("Solidifier", SolidifierRecipeRegistry.Book);
            RecipeCatalog.Register("IceMachine", IceMachineRecipeRegistry.Book);
            RecipeCatalog.Register("LiquidDistillator", LiquidDistillatorRecipeRegistry.Book);
            RecipeCatalog.Register("DecayChamber", DecayChamberRecipeRegistry.Book);
            RecipeCatalog.Register("Autohammer", AutohammerRecipeRegistry.Book);
            RecipeCatalog.Register("MechanicalAssembler", MechanicalAssemblerRecipeRegistry.Book);

            // Fuels the recipe book shows beside the arrow. Read from each machine's registry tables, not from CustomRecipe.
            // Every wood of the vanilla wood group is shown as ONE fuel entry ("Any Wood", Wood sprite). To change the list, edit woodItems.
            var woodItems = new System.Collections.Generic.List<int>();
            if (Terraria.RecipeGroup.recipeGroups.TryGetValue(RecipeGroupID.Wood, out Terraria.RecipeGroup woodGroup))
                woodItems.AddRange(woodGroup.ValidItems);
            RecipeCatalog.RegisterFuels("Furnace", FurnaceRecipeRegistry.Fuels, null,
                new RecipeCatalog.FuelGroup("AnyWood", ItemID.Wood, woodItems));
            RecipeCatalog.RegisterFuels("Hellforge", null, HellforgeRecipeRegistry.LiquidFuels);

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

            // Recipe book art entries (configured in RecipeArtDefinitions). Before the pools, which may reference them.
            RecipeArtRegistry.Clear();
            RecipeArtDefinitions.Register();

            // Which biome reveals which liquid in the book (configured in LiquidBiomeVisits). Needs the liquid ids.
            LiquidBiomeVisits.Register();

            // Scroll pools (configured in ScrollPoolDefinitions). Needs the catalog above to validate entries.
            ScrollPools.Clear();
            ScrollPoolDefinitions.Register();
            ScrollPools.Validate(msg => Mod.Logger.Warn(msg));
        }

        public override void Unload()
        {
            RecipeCatalog.Clear();
            RecipeArtRegistry.Clear();
            LiquidBiomeVisits.Clear();
            RecipeVisibility.ClearIndex();
            ScrollPools.Clear();
            MachineVisualRegistry.Definitions.Clear();
        }
    }
}