using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Globals
{
    public class BossLootGlobal : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.BrainofCthulhu)
            {
                npcLoot.Add(ItemDropRule.Common(ItemID.LesionStation));
            }
            else if (npc.type == NPCID.EaterofWorldsHead
                  || npc.type == NPCID.EaterofWorldsBody
                  || npc.type == NPCID.EaterofWorldsTail)
            {
                var lastSegment = new LeadingConditionRule(new Conditions.LegacyHack_IsABoss());
                lastSegment.OnSuccess(ItemDropRule.Common(ItemID.LesionStation));
                npcLoot.Add(lastSegment);
            }
        }
    }
}
