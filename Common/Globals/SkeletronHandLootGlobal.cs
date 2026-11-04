using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.NPCs
{
    /// <summary>
    /// The Skeletron Hand item no longer drops from Skeletron's head. Each living hand has its own chance when you kill it.
    /// Loot runs on the server / single-player only, the dropped item syncs like any other drop.
    /// </summary>
    public class SkeletronHandLootGlobal : GlobalNPC
    {

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.SkeletronHead || entity.type == NPCID.SkeletronHand;
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.SkeletronHead)
                SkeletronHandLoot.Strip(npcLoot.Get(false));
            else if (npc.type == NPCID.SkeletronHand)
            {
                int handDropOneIn = 1; // per hand: 6 = 1 in 6 (two hands is about 31% for at least one)
                npcLoot.Add(ItemDropRule.Common(ItemID.SkeletronHand, handDropOneIn));
            }
        }
    }

    /// <summary>Expert/Master: the Skeletron Treasure Bag gave the Hand too. Every boss bag is scanned, only a Skeletron Hand entry is ever touched.</summary>
    public class SkeletronHandBagLootGlobal : GlobalItem
    {
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            if (ItemID.Sets.BossBag[item.type])
                SkeletronHandLoot.Strip(itemLoot.Get(false));
        }
    }

    public static class SkeletronHandLoot
    {
        /// <summary>Removes the Skeletron Hand from "one of these" lists and zeroes plain Hand drops, including rules chained onto other rules.</summary>
        public static void Strip(List<IItemDropRule> rules)
        {
            foreach (IItemDropRule rule in rules)
                StripRule(rule);
        }

        private static void StripRule(IItemDropRule rule)
        {
            if (rule == null)
                return;

            if (rule is OneFromOptionsDropRule a)
                a.dropIds = a.dropIds.Where(id => id != ItemID.SkeletronHand).ToArray();
            else if (rule is OneFromOptionsNotScaledWithLuckDropRule b)
                b.dropIds = b.dropIds.Where(id => id != ItemID.SkeletronHand).ToArray();
            else if (rule is CommonDrop c && c.itemId == ItemID.SkeletronHand)
                c.chanceNumerator = 0;

            if (rule.ChainedRules != null)
            {
                foreach (IItemDropRuleChainAttempt chain in rule.ChainedRules)
                    StripRule(chain.RuleToChain);
            }
        }
    }
}