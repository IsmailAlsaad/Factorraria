using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    /// <summary>
    /// When Skeletron's loot spawns a Skeletron Hand item, move it from his skull to one of his living hands (picked at random).
    /// The hands are still active in the tick the head dies. With no living hand the item stays where vanilla spawned it.
    /// </summary>
    public class SkeletronHandDropGlobalItem : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.SkeletronHand;
        }

        public override void OnSpawn(Item item, IEntitySource source)
        {
            if (source is not EntitySource_Loot loot || loot.Entity is not NPC head || head.type != NPCID.SkeletronHead)
                return;

            float searchRadius = 1200f;
            List<NPC> hands = new List<NPC>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == NPCID.SkeletronHand && npc.Distance(head.Center) <= searchRadius)
                    hands.Add(npc);
            }

            if (hands.Count == 0)
                return;

            NPC hand = hands[Main.rand.Next(hands.Count)];
            item.position = hand.Center - item.Size / 2f;
        }
    }
}