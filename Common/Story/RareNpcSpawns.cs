using Factorraria.Common.Net;
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
    /// World generation cleanup (rule M1) and the rescue watcher (rule S2). Server / single-player only.
    /// </summary>
    public class RareNpcSystem : ModSystem
    {
        // The vanilla dungeon tied Mechanic must not be rescuable before the story: remove every tied Mechanic that world generation made.
        // Logged either way, so the first new world shows whether vanilla really creates her as an NPC at this point (roadmap V1a).
        public override void PostWorldGen()
        {
            int removed = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != NPCID.BoundMechanic)
                    continue;

                npc.active = false;
                removed++;
            }

            FactorrariaNet.Log("[Story] world generation: removed " + removed + " vanilla tied Mechanic NPC(s)");
        }

        // S2: the first tick the rescue flag is true, latch MechanicRescued. On the Skeletron-first route the rescue also unlocks the whole shop.
        public override void PostUpdateWorld()
        {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
                return;

            if (!NPC.savedMech || StorySystem.Get(StoryFlags.MechanicRescued))
                return;

            StorySystem.ServerSet(StoryFlags.MechanicRescued, true);

            if (StorySystem.Get(StoryFlags.DungeonFork))
                StorySystem.ServerSet(StoryFlags.MechanicShopUnlocked, true);
        }
    }
}