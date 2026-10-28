using Factorraria.Common.Liquids;
using Factorraria.Content.Items.Conveyors;
using Factorraria.Content.Items.Hooks;
using Factorraria.Content.Items.Liquids.Pipes;
using Factorraria.Content.Items.Machines;
using Factorraria.Content.Items.Materials;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// THIS IS THE FILE YOU EDIT TO CONFIGURE SCROLL POOLS.
    ///
    /// A scroll / parchment carries a pool NAME. Reading a parchment rolls ONE recipe:
    ///   1. from that pool's recipes the world has not unlocked or crafted yet (weighted)
    ///   2. once the pool is used up, from EVERY recipe in the book
    /// A scroll with no pool (null) always rolls from the whole book.
    ///
    ///   ScrollPools.Define("Dungeon", "hint shown in the tooltip")
    ///       .Add("Furnace", ItemID.IronBar)                          machine name as registered in MachineRegisterationSystem
    ///       .Add("Hellforge", ModContent.ItemType&lt;SteelBarItem&gt;(), weight: 2f)
    ///       .AddLiquid("Solidifier", LiquidTypeRegistry.Oil);        liquid products use the LiquidTypeRegistry ids
    ///       .AddArt("ExampleNote");                                  a recipe book art PNG (registered in RecipeArtDefinitions), by file name
    ///
    /// Machine names: Furnace, Hellforge, Solidifier, IceMachine, LiquidDistillator, Autohammer.
    /// Entries that match no recipe are logged as warnings at load (Logs\client.log, search "[ScrollPools]") and skipped.
    /// Register() runs in PostAddRecipes, so ModContent.ItemType and LiquidTypeRegistry ids are safe to use here.
    ///
    /// To put a pool's scroll in chests see ChestLootDefinitions (AddScroll). To test: /recipes pools, /recipes scroll &lt;pool&gt;.
    /// </summary>
    public static class ScrollPoolDefinitions
    {
        public static void Register()
        {
            DefaultPool();

            // ---- your own pools go here (or uncomment the example) ----
            // Example_Dungeon();
        }

        // ==========================================================================================
        // THE DEFAULT POOL: EDIT THIS LIST TO CHANGE WHAT A NORMAL CHEST SCROLL CAN TEACH.
        // Every chest in the world has a 15% chance of a scroll of this pool (see ChestLootDefinitions).
        // One line = one recipe. First argument = the machine that crafts it (catalog name), second = the product.
        // Weights are relative (default 1). A recipe leaves the pool once it is unlocked or crafted; when the
        // whole pool is used up the scroll falls back to any recipe in the book.
        // To drop a recipe: delete or comment out its line. To make one rarer/more common: add weight: 0.5f / 2f.
        // ==========================================================================================
        static void DefaultPool()
        {
            ScrollPools.Define("PlaceholderArt", "Placeholder art").AddArt("Placeholder", weight: 1f);

            ScrollPools.Define("Default", "Notes scribbled by someone who knew their way around a factory.")
                // Hellforge
                .Add("Hellforge", ItemID.Meteorite)                                   // meteorite ore
                .Add("Hellforge", ModContent.ItemType<SteelBarItem>())
                .Add("Hellforge", ModContent.ItemType<ByriteAlloyItem>())

                // Autohammer
                .Add("Autohammer", ModContent.ItemType<ClockwisePriorityConveyor>(), weight: 0.5f)         // the two priority conveyors
                .Add("Autohammer", ModContent.ItemType<CounterClockwisePriorityConveyor>(), weight: 0.5f)  // share ONE normal slot (0.5 + 0.5)
                .Add("Autohammer", ModContent.ItemType<MechanicalArmItem>())
                .Add("Autohammer", ItemID.SkeletronHand)
                .Add("Autohammer", ItemID.Autohammer)
                .Add("Autohammer", ItemID.Cog)
                .Add("Autohammer", ModContent.ItemType<MechanicalAssemblerItem>())
                .Add("Autohammer", ItemID.Extractinator)
                .Add("Autohammer", ItemID.Solidifier)
                .Add("Autohammer", ItemID.IceMachine)
                .Add("Autohammer", ItemID.SteampunkBoiler)
                .Add("Autohammer", ModContent.ItemType<WoodenPowerPoleItem>())
                .Add("Autohammer", ModContent.ItemType<PlatinumPowerPoleItem>())
                .Add("Autohammer", ModContent.ItemType<BeeWheelItem>())
                .Add("Autohammer", ModContent.ItemType<MeteoritePanelItem>())

                // Mechanical Assembler
                .Add("MechanicalAssembler", ModContent.ItemType<SimpleCircuitItem>())
                .Add("MechanicalAssembler", ModContent.ItemType<GoldWireItem>())
                .Add("MechanicalAssembler", ModContent.ItemType<PipeMK1Item>())      // lead pipe

                // Ice Machine / Liquid Distillator / Decay Chamber
                .Add("IceMachine", ModContent.ItemType<CokeItem>())
                .AddLiquid("LiquidDistillator", LiquidTypeRegistry.Fuel)             // fuel liquid
                .Add("DecayChamber", ItemID.CrimtaneBar)
                .Add("DecayChamber", ItemID.DemoniteBar);

            // NOT IN THE POOL: the Decay Chamber item itself (Lesion Station). Its recipe is a hand-craft at the
            // Demon Altar (RecipeSystem.cs), and no machine book contains it, so there is no catalog recipe to teach.
        }

        // Real, compiled example that is NOT called by default. Adjust the entries to recipes that exist, then call it from Register().
        static void Example_Dungeon()
        {
            ScrollPools.Define("Dungeon", "Smells of old stone and damp.")
                .Add("Furnace", ItemID.IronBar)
                .Add("Hellforge", ModContent.ItemType<SteelBarItem>(), weight: 2f)
                .AddLiquid("Solidifier", LiquidTypeRegistry.Oil);
        }
    }
}