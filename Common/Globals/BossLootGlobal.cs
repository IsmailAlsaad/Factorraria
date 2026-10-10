using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.NPCs
{
    public class BossLootGlobal : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // Eye of Cthulhu
            if (npc.type == NPCID.EyeofCthulhu)
            {
                npcLoot.RemoveLoot(ItemID.DemoniteOre);
                npcLoot.RemoveLoot(ItemID.CrimtaneOre);
            }

            // Brain of Cthulhu
            if (npc.type == NPCID.BrainofCthulhu)
            {
                npcLoot.RemoveLoot(ItemID.DemoniteOre);
                npcLoot.RemoveLoot(ItemID.CrimtaneOre);

                npcLoot.Add(ItemDropRule.Common(ItemID.LesionStation));
            }
            // Creepers
            if (npc.type == NPCID.Creeper)
            {
                npcLoot.RemoveLoot(ItemID.CrimtaneOre);
            }

            // Eater of Worlds (All Segments)
            if (npc.type == NPCID.EaterofWorldsHead ||
                npc.type == NPCID.EaterofWorldsBody ||
                npc.type == NPCID.EaterofWorldsTail)
            {
                npcLoot.RemoveLoot(ItemID.DemoniteOre);
                npcLoot.RemoveLoot(ItemID.CrimtaneOre);

                var lastSegment = new LeadingConditionRule(new Conditions.LegacyHack_IsABoss());
                lastSegment.OnSuccess(ItemDropRule.Common(ItemID.LesionStation));
                npcLoot.Add(lastSegment);
            }

            // King Slime
            if (npc.type == NPCID.KingSlime)
            {
                npcLoot.Add(ItemDropRule.Common(ItemID.SlimeStatue, 1, 1, 3));
            }
        }
    }

    public static class NPCLootExtensions
    {
        /// <summary>
        /// General helper to remove any drop rule matching the specified item ID from an NPCLoot table.
        /// Works across all tModLoader versions by inspecting item fields and nested child rules via reflection.
        /// </summary>
        public static void RemoveLoot(this NPCLoot npcLoot, int itemType)
        {
            List<IItemDropRule> rules = npcLoot.Get(false);

            for (int i = rules.Count - 1; i >= 0; i--)
            {
                IItemDropRule rule = rules[i];

                if (RuleMatchesItemType(rule, itemType))
                {
                    npcLoot.Remove(rule);
                }
            }
        }

        private static bool RuleMatchesItemType(IItemDropRule rule, int itemType)
        {
            if (rule == null) return false;

            // Direct check for standard CommonDrop and subclasses
            if (rule is CommonDrop commonDrop && commonDrop.itemId == itemType)
                return true;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // 1. Check if this rule has an 'itemId' field or property
            FieldInfo itemIdField = rule.GetType().GetField("itemId", flags);
            if (itemIdField != null && itemIdField.GetValue(rule) is int fieldId && fieldId == itemType)
                return true;

            PropertyInfo itemIdProp = rule.GetType().GetProperty("itemId", flags);
            if (itemIdProp != null && itemIdProp.GetValue(rule) is int propId && propId == itemType)
                return true;

            // 2. Check for nested/child rules (e.g., Master/Expert mode wrappers or chained rules)
            foreach (FieldInfo field in rule.GetType().GetFields(flags))
            {
                if (typeof(IItemDropRule).IsAssignableFrom(field.FieldType))
                {
                    if (RuleMatchesItemType(field.GetValue(rule) as IItemDropRule, itemType))
                        return true;
                }
            }

            foreach (PropertyInfo prop in rule.GetType().GetProperties(flags))
            {
                if (typeof(IItemDropRule).IsAssignableFrom(prop.PropertyType) && prop.CanRead)
                {
                    if (RuleMatchesItemType(prop.GetValue(rule) as IItemDropRule, itemType))
                        return true;
                }
            }

            return false;
        }
    }
}