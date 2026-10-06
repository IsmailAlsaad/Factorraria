using Factorraria.Common.Liquids;
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
            // ---- your own pools go here (or uncomment the example) ----
            // Example_Dungeon();
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