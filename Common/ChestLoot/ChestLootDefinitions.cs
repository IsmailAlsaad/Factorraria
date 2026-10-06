using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.ModLoader;
using Factorraria.Content.Items.Materials;
using Factorraria.Content.Items.Discovery;

namespace Factorraria.Common.ChestLoot
{
    /// <summary>
    /// THIS IS THE ONLY FILE YOU NEED TO EDIT TO CHANGE CHEST LOOT.
    ///
    /// HOW IT WORKS
    ///   A "rule" = a FILTER (which chests) + optional WHERE (extra condition) + STEPS (what to do).
    ///   Rules run top to bottom on every matching chest, each on top of the previous result.
    ///   They run once, after world generation, so they only affect NEWLY generated worlds.
    ///
    /// FILTERS (pick exactly one per rule)
    ///   For(VanillaChest.Gold)                    one vanilla chest style
    ///   For(VanillaChest.Wood, VanillaChest.Ivy)  several vanilla styles
    ///   For(tileType, style)                      exact tile + style (Containers2, modded chests)
    ///   ForTile(tileType)                         every style of one chest tile
    ///   ForAll()                                  every chest in the world
    ///   ForMatching((tile, style) => bool)        your own test
    ///
    /// WHERE (optional, stackable, all must pass; evaluated ONCE per chest, BEFORE the steps run)
    ///   .Where(ChestConditions.Surface)           Surface / Underground / Cavern / Underworld / LeftHalf / RightHalf
    ///   .Where(c => c.y > 400)                    any lambda; c.x / c.y are the chest's tile position, c.item[] its slots
    ///
    /// STEPS (run in the order written)
    ///   .Clear()                                  wipe all vanilla loot (leave it out to just ADD)
    ///   .Remove(itemType)                         strip every stack of one item
    ///   .ReplaceItem(from, to)                    swap an item type, keeping the stack size
    ///   .Always(item, min, max)                   guaranteed. max omitted = exactly min; item only = exactly 1
    ///   .Chance(0.25f, item, min, max)            independent roll, 0..1
    ///   .OneOf(new LootItem(...), ...)            exactly ONE of the options, picked by weight
    ///   .OneOf(0.3f, new LootItem(...), ...)      30% chance to add one of the options
    ///   .Custom((chest, rand) => { ... })         escape hatch: any code, with the seeded random
    ///
    ///   new LootItem(type, min, max, weight)      weights are relative (6 vs 3 vs 1), not percentages
    ///
    /// GOOD TO KNOW
    ///   - Items go into the first empty slot; if the chest is full the extra is silently dropped.
    ///   - All steps in a rule share that rule's filter/Where. Need different conditions? Make two rules.
    ///   - Always use the `rand` passed to Custom (seed-deterministic), never Main.rand, during worldgen.
    ///
    /// HOW TO USE THE EXAMPLES BELOW
    ///   Every example is a real, compiled method (so typos are caught at build time) that is NOT called
    ///   by default. To enable one, uncomment its call inside Register(). Copy one as a starting point
    ///   for your own rules, or write rules straight into Register().
    /// </summary>
    public static class ChestLootDefinitions
    {
        public static void Register()
        {
            // ------------------------------------------------------------------------------------------
            // DISCOVERY: uncomment, generate a world, then read the tModLoader log (Logs\client.log).
            // You get lines like "[ChestLoot] 12x tile 21 style 1" = tile id 21 (Containers), style 1 (Gold).
            // Use those numbers with For(tileType, style) for any chest not in the VanillaChest enum.
            // ------------------------------------------------------------------------------------------
            // ChestLootRegistry.LogFoundChests = true;

            // ------------------------------------------------------------------------------------------
            // EXAMPLES: uncomment the ones you want. Order matters (see Example6).
            // ------------------------------------------------------------------------------------------
            // Example1_FullReplacement();
            // Example2_SurgicalEdits();
            // Example3_DepthLayers();
            // Example4_FilterOnContents();
            // Example5_StylesAndModdedChests();
            // Example6_RuleOrder();

            // ---- your own rules go here ----

            // RECIPE DISCOVERY: sealed scrolls. Chances are first-pass numbers, tune them in the polish phase.
            // Only NEW worlds get these (loot rules run after worldgen). Existing worlds: use "/recipes scroll".
            // A scroll carries a pool name (see ScrollPoolDefinitions); no pool = rolls from the whole book.
            //ChestLootRegistry.For(VanillaChest.Wood).Custom((chest, rand) => AddScroll(chest, rand, 0.06f));
            //ChestLootRegistry.For(VanillaChest.LivingWood, VanillaChest.Ivy, VanillaChest.Skyware, VanillaChest.Water).Custom((chest, rand) => AddScroll(chest, rand, 0.12f));
            //ChestLootRegistry.For(VanillaChest.Gold, VanillaChest.Frozen).Custom((chest, rand) => AddScroll(chest, rand, 0.20f));
            //ChestLootRegistry.For(VanillaChest.Shadow, VanillaChest.ShadowLocked, VanillaChest.Lihzahrd).Custom((chest, rand) => AddScroll(chest, rand, 0.30f));
            // Themed: locked gold chests (the dungeon ones) give a "Dungeon" scroll. Define that pool in ScrollPoolDefinitions first.
            //ChestLootRegistry.For(VanillaChest.GoldLocked).Custom((chest, rand) => AddScroll(chest, rand, 0.35f, "Dungeon"));
        }

        /// <summary>
        /// Adds one Sealed Scroll of the given pool (null = whole book) to the chest's first empty slot with the given chance.
        /// Use inside .Custom((chest, rand) => ...). Uses the seeded worldgen random passed in.
        /// </summary>
        private static void AddScroll(Chest chest, UnifiedRandom rand, float chance, string pool = null)
        {
            if (rand.NextFloat() >= chance) return;
            for (int i = 0; i < Chest.maxItems; i++)
            {
                if (!chest.item[i].IsAir) continue;
                chest.item[i].SetDefaults(ModContent.ItemType<ClosedScrollItem>());
                if (chest.item[i].ModItem is ClosedScrollItem scroll) scroll.PoolName = pool;
                return;
            }
        }

        // ==========================================================================================
        // EXAMPLE 1: FULL REPLACEMENT WITH WEIGHTED TIERS
        // Shows: Clear, Always, Chance, OneOf (weighted), OneOf with a chance, multi-style filter.
        // Result for every Gold / locked Gold chest: vanilla loot is gone and replaced by:
        //   - 4-8 Steel Bars and 1-3 Gold Coins (guaranteed)
        //   - exactly one "main" item (Cog 60%, Wire 30%, Coke 10% by weight 6:3:1)
        //   - a 15% chance of a Golden Key
        //   - a 30% chance of 2-4 healing OR mana potions (50/50)
        // ==========================================================================================
        private static void Example1_FullReplacement()
        {
            ChestLootRegistry.For(VanillaChest.Gold, VanillaChest.GoldLocked)
                .Clear()                                                    // wipe vanilla loot first
                .Always(ModContent.ItemType<SteelBarItem>(), 4, 8)          // guaranteed, random stack 4..8
                .Always(ItemID.GoldCoin, 1, 3)                              // guaranteed, 1..3
                .OneOf(                                                     // exactly one of these three
                    new LootItem(ItemID.Cog, 3, 6, weight: 6f),
                    new LootItem(ItemID.Wire, 20, 40, weight: 3f),
                    new LootItem(ModContent.ItemType<CokeItem>(), 5, 10, weight: 1f))
                .Chance(0.15f, ItemID.GoldenKey)                            // no min/max = exactly 1
                .OneOf(0.3f,                                                // 30% to add ONE of these
                    new LootItem(ItemID.HealingPotion, 2, 4),               // weight defaults to 1
                    new LootItem(ItemID.ManaPotion, 2, 4));
        }

        // ==========================================================================================
        // EXAMPLE 2: SURGICAL EDITS (KEEP VANILLA LOOT, TWEAK IT)
        // Shows: Remove, ReplaceItem, Custom. There is NO Clear(), so vanilla contents stay.
        // ==========================================================================================
        private static void Example2_SurgicalEdits()
        {
            ChestLootRegistry.ForAll()
                .Remove(ItemID.Spear)                           // strip one vanilla drop from every chest
                .ReplaceItem(ItemID.IronBar, ItemID.LeadBar)    // swap item type, stack size is kept
                .Custom((chest, rand) =>                        // Custom: do anything to the chest
                {
                    // double every gold coin stack (capped at the item's max stack)
                    for (int i = 0; i < Chest.maxItems; i++)
                    {
                        Item slot = chest.item[i];
                        if (!slot.IsAir && slot.type == ItemID.GoldCoin)
                            slot.stack = System.Math.Min(slot.stack * 2, slot.maxStack);
                    }
                })
                .Custom((chest, rand) =>
                {
                    // roll a random prefix on every weapon (stackable single items with damage)
                    for (int i = 0; i < Chest.maxItems; i++)
                    {
                        Item slot = chest.item[i];
                        if (!slot.IsAir && slot.damage > 0 && slot.maxStack == 1)
                            slot.Prefix(-1);                    // -1 = random prefix
                    }
                });
        }

        // ==========================================================================================
        // EXAMPLE 3: DEPTH LAYERING WITH Where
        // Shows: the built-in ChestConditions and stacking several Where calls (they AND together).
        // Three separate rules because each layer wants different steps (see "Good to know" above).
        // ==========================================================================================
        private static void Example3_DepthLayers()
        {
            ChestLootRegistry.ForAll()
                .Where(ChestConditions.Surface)
                .Chance(0.5f, ItemID.Torch, 5, 15);

            ChestLootRegistry.ForAll()
                .Where(ChestConditions.Underground)
                .Chance(0.25f, ItemID.Cog, 1, 3);

            ChestLootRegistry.ForAll()
                .Where(ChestConditions.Underworld)
                .Always(ModContent.ItemType<CokeItem>(), 10, 20)
                .Chance(0.1f, ItemID.Hellforge);

            // Stacked Where: wooden chests in the cavern layer AND within 400 tiles of a world edge.
            ChestLootRegistry.For(VanillaChest.Wood)
                .Where(ChestConditions.Cavern)
                .Where(c => c.x < 400 || c.x > Main.maxTilesX - 400)
                .Chance(0.4f, ItemID.WaterWalkingBoots);
        }

        // ==========================================================================================
        // EXAMPLE 4: FILTER ON CURRENT CONTENTS
        // Where runs once per chest BEFORE its steps, so it sees the vanilla (or earlier-rule) contents.
        // Use it for "if the chest rolled X, then also do Y".
        // ==========================================================================================
        private static void Example4_FilterOnContents()
        {
            ChestLootRegistry.ForAll()
                .Where(c => System.Array.Exists(c.item, it => !it.IsAir && it.type == ItemID.CloudinaBottle))
                .Always(ItemID.Cog, 5);     // bonus for chests that rolled a Cloud in a Bottle
        }

        // ==========================================================================================
        // EXAMPLE 5: STYLES, OTHER TILES AND MODDED CHESTS
        // Shows every filter. Style numbers for non-enum chests come from LogFoundChests (top of Register).
        // ==========================================================================================
        private static void Example5_StylesAndModdedChests()
        {
            // several vanilla styles in one rule
            ChestLootRegistry.For(VanillaChest.Ivy, VanillaChest.LivingWood, VanillaChest.Skyware)
                .Chance(0.2f, ItemID.Cog, 2, 4);

            // exact tile + style, e.g. a Containers2 chest (verify the style number with the log)
            ChestLootRegistry.For(TileID.Containers2, 0)
                .Clear()
                .Always(ItemID.GoldCoin, 5, 10);

            // every style of one chest tile
            ChestLootRegistry.ForTile(TileID.Containers2)
                .Chance(0.1f, ItemID.GoldenKey);

            // fully custom match: only locked gold (style 2) or locked shadow (style 4) chests
            ChestLootRegistry.ForMatching((tile, style) =>
                    tile == TileID.Containers && (style == 2 || style == 4))
                .Chance(0.5f, ItemID.GoldenKey);

            // a chest from your own or another mod (any tile with TileID.Sets.BasicChest is picked up):
            // ChestLootRegistry.For(ModContent.TileType<MyChestTile>(), 0)
            //     .Clear()
            //     .Always(ItemID.Cog, 10);
        }

        // ==========================================================================================
        // EXAMPLE 6: RULE ORDER IS A PIPELINE
        // Rules run in registration order on the same chest.
        //   1. strip Spears from everything
        //   2. gold chests are wiped and given coins
        //   3. EVERY chest (gold ones too, since this comes after the wipe) gets a 10% Cog
        // TRAP: a ForAll().Clear() placed AFTER other rules erases everything they added.
        // ==========================================================================================
        private static void Example6_RuleOrder()
        {
            ChestLootRegistry.ForAll().Remove(ItemID.Spear);

            ChestLootRegistry.For(VanillaChest.Gold)
                .Clear()
                .Always(ItemID.GoldCoin, 3, 6);

            ChestLootRegistry.ForAll().Chance(0.1f, ItemID.Cog);
        }

        // ==========================================================================================
        // NOTES
        //   Existing worlds: ChestLootRegistry.ApplyAll(Main.rand) re-applies everything (e.g. from your own
        //   command). Rules that only add (no Clear) will DUPLICATE their items if applied twice.
        //
        //   Stateful Custom closures (e.g. a counter for "only one chest per world gets this") live as long
        //   as the game session, because Register() runs once per mod load, not per world. Reset such state
        //   yourself, for example in a ModSystem.PreWorldGen override.
        // ==========================================================================================
    }
}