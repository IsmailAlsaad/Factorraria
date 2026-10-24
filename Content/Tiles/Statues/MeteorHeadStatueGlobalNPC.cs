using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Statues
{
    // Loot for every Meteor Head (natural, awakened). Wired-statue ones are already blocked by
    // NPCID.Sets.StatueSpawnedDropRarity in MeteorHeadStatueTile, which vanilla checks before any loot is rolled.
    public class MeteorHeadStatueGlobalNPC : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.MeteorHead;

        public override void SetDefaults(NPC npc)
        {
            npc.value = 80f;   // coins on death, in copper (100 = 1 silver)
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // Common(item, chanceDenominator, min, max): 1 = always, 3 = 1 in 3
            npcLoot.Add(ItemDropRule.Common(ItemID.Meteorite, 4, 1, 4)); // 25% 1-4 Meteorite
        }
    }
}