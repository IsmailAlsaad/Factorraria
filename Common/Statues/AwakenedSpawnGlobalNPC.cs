using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Statues
{
    // Tags enemies spawned by an awakened statue. While the statue is still awakened, CheckActive keeps them from
    // despawning (also when no player is near). Once the statue or its machine is gone the tag does nothing and the
    // enemy despawns the normal way.
    public class AwakenedSpawnGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public bool Tagged;
        public Point16 Origin;
        public AwakenedStatueEntry Entry;   // the statue that spawned this enemy; its Loot list is rolled on death

        public override bool CheckActive(NPC npc)
        {
            if (!Tagged) return true;

            var statues = ModContent.GetInstance<AwakenedStatueSystem>();
            bool held = statues.Awakened.TryGetValue(Origin, out AwakenedStatue statue)
                && statue.Entry.Spawns(npc.type)
                && statues.IsStillAwakened(statue);

            return !held;
        }

        // Statue loot is rolled from the entry stored at spawn time, so it still drops if the statue is gone by then.
        public override void OnKill(NPC npc)
        {
            if (!Tagged || Entry == null || Entry.Loot.Count == 0) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            foreach (AwakenedLootDrop drop in Entry.Loot)
            {
                if (Main.rand.NextFloat() >= drop.Chance) continue;
                Item.NewItem(npc.GetSource_Loot(), npc.getRect(), drop.ItemType, Main.rand.Next(drop.Min, drop.Max + 1));
            }
        }
    }
}