using Factorraria.Common.Net;
using Factorraria.Content.Items.Story;
using Factorraria.Content.NPCs.HerBackpack;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// The spawn rules of the two rare story NPCs (NPC roadmap patch N4.1, rules D15 to D18 and M1 to M7): the tied Mechanic
    /// (vanilla NPCID.BoundMechanic) and Her Backpack. One place decides both, so the spawn code, /fstory dump and the tests agree.
    /// Server / single-player only (the spawn hooks run there). Each method returns a weight (0 = not allowed) and says why not.
    /// </summary>
    public static class RareNpcRules
    {
        // Tunables (not const, so hot reload can change them; /fstory weight changes them while playing).
        // Vanilla spawn weights are around 1 for a common monster, so these small values make the NPCs rare.
        public static float MechanicSpawnWeight = 0.5f;
        public static float DungeonMechanicSpawnWeight = 0.5f;
        public static float BackpackSpawnWeight = 0.5f;
        public static int RareNpcRarity = 1;
        public static int BackpackRespawnDelayTicks = 600;   // the Bag must be missing this long before BackpackTaken is cleared (the Backpack can spawn again)

        /// <summary>The tied Mechanic: Cavern layer only (the dungeon after the Skeletron-first fork), while she was never rescued and no Mechanic of any kind exists.</summary>
        public static float MechanicWeight(Player player, out string reason)
        {
            reason = null;

            if (player == null)
                return Deny("no player", out reason);

            if (StorySystem.Get(StoryFlags.MechanicRescued))
                return Deny("MechanicRescued is on", out reason);

            if (NPC.savedMech)
                return Deny("NPC.savedMech is true", out reason);

            if (NPC.AnyNPCs(NPCID.BoundMechanic))
                return Deny("a tied Mechanic already exists", out reason);

            if (NPC.AnyNPCs(NPCID.Mechanic))
                return Deny("a town Mechanic already exists", out reason);

            if (StorySystem.Get(StoryFlags.DungeonFork))
            {
                if (!player.ZoneDungeon)
                    return Deny("DungeonFork is on and the player is not in the dungeon", out reason);

                return Allow(DungeonMechanicSpawnWeight, "DungeonMechanicSpawnWeight is 0", out reason);
            }

            // Route A: she only starts spawning underground once the Eye of Cthulhu was defeated (vanilla's own flag).
            // Route B (Skeletron first) is the DungeonFork branch above, which does not need the Eye.
            if (!NPC.downedBoss1)
                return Deny("the Eye of Cthulhu was not defeated yet (route A needs it; route B starts when Skeletron falls first)", out reason);

            if (player.ZoneDungeon)
                return Deny("the player is in the dungeon (the Mechanic waits for the Cavern layer)", out reason);

            if (player.ZoneUnderworldHeight)
                return Deny("the player is in the Underworld", out reason);

            if (!player.ZoneRockLayerHeight)
                return Deny("the player is not in the Cavern layer", out reason);

            return Allow(MechanicSpawnWeight, "MechanicSpawnWeight is 0", out reason);
        }

        /// <summary>Her Backpack: dungeon only, after Skeletron, until the Bag was taken or the shop unlocked, never on the Skeletron-first route, one at a time.</summary>
        public static float BackpackWeight(Player player, out string reason)
        {
            reason = null;

            if (player == null)
                return Deny("no player", out reason);

            if (!NPC.downedBoss3)
                return Deny("Skeletron was not defeated", out reason);

            if (!player.ZoneDungeon)
                return Deny("the player is not in the dungeon", out reason);

            if (!StorySystem.Get(StoryFlags.MechanicRescued))
                return Deny("the Mechanic was not rescued yet", out reason);

            if (StorySystem.Get(StoryFlags.BackpackTaken))
                return Deny("BackpackTaken is on", out reason);

            if (StorySystem.Get(StoryFlags.MechanicShopUnlocked))
                return Deny("MechanicShopUnlocked is on", out reason);

            if (StorySystem.Get(StoryFlags.DungeonFork))
                return Deny("DungeonFork is on (the Backpack rule is off)", out reason);

            if (NPC.AnyNPCs(ModContent.NPCType<HerBackpackNPC>()))
                return Deny("a Backpack already exists", out reason);

            return Allow(BackpackSpawnWeight, "BackpackSpawnWeight is 0", out reason);
        }

        static float Deny(string why, out string reason)
        {
            reason = why;
            return 0f;
        }

        static float Allow(float weight, string whyZero, out string reason)
        {
            reason = weight > 0f ? null : whyZero;
            return weight > 0f ? weight : 0f;
        }
    }

    /// <summary>
    /// Route B, the Skeletron-first fork (NPC roadmap rule S5, decisions D11 and D18, patches N4.3 and N8.1). One place decides when the fork
    /// starts, so the watcher, /fstory fork and the tests agree. Server / single-player only (the watcher runs there); clients only read the
    /// DungeonFork flag. What the fork changes (nothing is removed or replaced):
    ///  - the tied Mechanic's spawn rule moves from the Cavern layer to the dungeon (RareNpcRules.MechanicWeight),
    ///  - her rescue unlocks the whole shop at once (RareNpcSystem.WatchRescue),
    ///  - Her Backpack never spawns (RareNpcRules.BackpackWeight) and the bag-loss watcher rests,
    ///  - the Dryad dialogues are off (neither starts, ImportantDialogueDefinitions); the scripted Dryad at the Eye is kept (DryadStory.SpawnOnFork), because the Mechanic's scrolls need a real conversation with a Dryad (flag DryadTalked, patch N8.2).
    /// The Mechanic's own scroll dialogue still works after her rescue.
    /// </summary>
    public static class StoryForkRules
    {
        // Tunable (not const, so hot reload and /fstory fork can change it). Reset to true on every world load.
        public static bool WatcherEnabled = true;

        /// <summary>True when the fork should start right now. When false, reason says why not (the first failing rule).</summary>
        public static bool ShouldLatch(out string reason)
        {
            reason = null;

            if (StorySystem.Get(StoryFlags.DungeonFork))
            {
                reason = "DungeonFork is already on (a later Skeletron kill does nothing)";
                return false;
            }

            if (!NPC.downedBoss3)
            {
                reason = "Skeletron was not defeated yet";
                return false;
            }

            // Either flag means the Eye came first: our own saved flag, or vanilla's (they are set in the same tick on a normal kill).
            if (StorySystem.Get(StoryFlags.EyeKillSeen) || NPC.downedBoss1)
            {
                reason = "the Eye of Cthulhu was defeated first (route A)";
                return false;
            }

            if (StorySystem.Get(StoryFlags.MechanicRescued) || NPC.savedMech)
            {
                reason = "the Mechanic was already rescued (route A)";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Adds the tied Mechanic to the natural spawn pool and gives her a rarity so the Lifeform Analyzer can list her.
    /// EditSpawnPool runs on the server / single-player only. The spawn spot is vanilla's (near the player, off screen).
    /// </summary>
    public class RareNpcGlobalNPC : GlobalNPC
    {
        public override void SetDefaults(NPC npc)
        {
            // The Lifeform Analyzer lists creatures with a rarity above 0 (to be confirmed in game, roadmap V12). Only raise it, never lower vanilla's own value.
            if (npc.type == NPCID.BoundMechanic && npc.rarity < RareNpcRules.RareNpcRarity)
                npc.rarity = RareNpcRules.RareNpcRarity;
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            float weight = RareNpcRules.MechanicWeight(spawnInfo.Player, out _);
            if (weight > 0f)
                pool[NPCID.BoundMechanic] = weight;
        }
    }

    /// <summary>
    /// The rescue watcher (rule S2) and the bag-loss watcher (patch N4.2). Server / single-player only.
    /// </summary>
    public class RareNpcSystem : ModSystem
    {
        static int bagMissingTicks;

        public override void OnWorldLoad()
        {
            bagMissingTicks = 0;
            StoryForkRules.WatcherEnabled = true;
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
                return;

            WatchRescue();
            WatchFork();
            WatchBag();
        }

        // S2: the first tick the rescue flag is true, latch MechanicRescued. On the Skeletron-first route the rescue also unlocks the whole shop.
        // N8.1: that unlock is a standing rule (fork on + rescued = shop unlocked), not a one-time step at the rescue tick, so it also happens
        // when the two flags became true in another order (a restored world, a debug flag).
        static void WatchRescue()
        {
            if (NPC.savedMech && !StorySystem.Get(StoryFlags.MechanicRescued))
                StorySystem.ServerSet(StoryFlags.MechanicRescued, true);

            if (StorySystem.Get(StoryFlags.DungeonFork) && StorySystem.Get(StoryFlags.MechanicRescued) && !StorySystem.Get(StoryFlags.MechanicShopUnlocked))
                StorySystem.ServerSet(StoryFlags.MechanicShopUnlocked, true);
        }

        // Route B (rule S5, done here as a world watcher instead of an OnKill hook): the first tick StoryForkRules.ShouldLatch says yes (Skeletron is
        // down while the Eye of Cthulhu never was and the Mechanic was not rescued), DungeonFork is latched. Once it is on, ShouldLatch is false, so
        // a second Skeletron kill does nothing. From then on the tied Mechanic spawns in the dungeon (not underground), her rescue unlocks the whole
        // shop at once (WatchRescue), Her Backpack never spawns and the Dryad dialogues are off. /fstory fork off pauses this watcher for testing.
        static void WatchFork()
        {
            if (!StoryForkRules.WatcherEnabled || !StoryForkRules.ShouldLatch(out _))
                return;

            StorySystem.ServerSet(StoryFlags.DungeonFork, true);
            FactorrariaNet.Log("[Story] Skeletron fell before the Eye of Cthulhu: DungeonFork on (route B)");
        }

        // Bag-loss watcher: while a Bag was taken and the shop is still locked, some player (any inventory, bank, trash) or the ground must hold it.
        // If none does for BackpackRespawnDelayTicks in a row (it burned, fell out of the world, the holder deleted it), BackpackTaken is cleared
        // so Her Backpack can spawn again. Known limit: a Bag stored in a chest counts as lost (chests are not scanned).
        static void WatchBag()
        {
            // N8.1: on the Skeletron-first route there is no Backpack and no Bag to respawn, so the watcher rests there too.
            if (!StorySystem.Get(StoryFlags.BackpackTaken) || StorySystem.Get(StoryFlags.MechanicShopUnlocked) || StorySystem.Get(StoryFlags.DungeonFork))
            {
                bagMissingTicks = 0;
                return;
            }

            bool anyPlayer = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (Main.player[i].active)
                {
                    anyPlayer = true;
                    break;
                }
            }

            if (!anyPlayer || BagExists())
            {
                bagMissingTicks = 0;
                return;
            }

            bagMissingTicks++;
            if (bagMissingTicks < System.Math.Max(1, RareNpcRules.BackpackRespawnDelayTicks))
                return;

            bagMissingTicks = 0;
            StorySystem.ServerSet(StoryFlags.BackpackTaken, false);
            FactorrariaNet.Log("[Story] the Mechanic's Bag is lost: BackpackTaken cleared, Her Backpack can spawn again");
        }

        static bool BagExists()
        {
            int bag = ModContent.ItemType<MechanicsBagItem>();

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (!player.active)
                    continue;

                if (Holds(player.inventory, bag) || Holds(player.armor, bag) || Holds(player.miscEquips, bag)
                    || Holds(player.bank.item, bag) || Holds(player.bank2.item, bag) || Holds(player.bank3.item, bag) || Holds(player.bank4.item, bag)
                    || (player.trashItem != null && !player.trashItem.IsAir && player.trashItem.type == bag))
                    return true;
            }

            for (int i = 0; i < Main.maxItems; i++)
            {
                if (Main.item[i].active && !Main.item[i].IsAir && Main.item[i].type == bag)
                    return true;
            }

            return false;
        }

        static bool Holds(Item[] items, int type)
        {
            foreach (Item item in items)
            {
                if (item != null && !item.IsAir && item.type == type)
                    return true;
            }

            return false;
        }
    }
}