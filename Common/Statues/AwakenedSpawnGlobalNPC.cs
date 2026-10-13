using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Statues
{
    // Tags enemies spawned by an awakened statue. While the statue is still awakened, CheckActive keeps them from
    // despawning (also when no player is near). Once the statue stops being awakened (machine broken or switched off,
    // statue broken or replaced) the tag is removed and the enemy is a normal enemy again, so it despawns normally and
    // drops no statue loot.
    public class AwakenedSpawnGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public bool Tagged;
        public Point16 Origin;
        public AwakenedStatueEntry Entry;   // the statue that spawned this enemy; its Loot list is rolled on death

        // Turns the enemy back into a normal one.
        public void Release()
        {
            Tagged = false;
            Entry = null;
        }

        public override bool CheckActive(NPC npc)
        {
            if (!Tagged) return true;

            var statues = ModContent.GetInstance<AwakenedStatueSystem>();
            bool held = statues.Awakened.TryGetValue(Origin, out AwakenedStatue statue)
                && statue.Entry.Spawns(npc.type)
                && statues.IsStillAwakened(statue);

            // Safety net: if the explicit release in AwakenedStatueSystem.RescanFor missed this enemy, drop the tag here.
            if (!held) Release();

            return !held;
        }

        // Statue loot is rolled from the entry stored at spawn time. Releasing the tag clears the entry, so it stops with the tag.
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