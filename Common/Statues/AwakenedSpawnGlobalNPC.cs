using Terraria;
using Terraria.DataStructures;
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

        public override bool CheckActive(NPC npc)
        {
            if (!Tagged) return true;

            var statues = ModContent.GetInstance<AwakenedStatueSystem>();
            bool held = statues.Awakened.TryGetValue(Origin, out AwakenedStatue statue)
                && statue.Entry.NpcType == npc.type
                && statues.IsStillAwakened(statue);

            return !held;
        }
    }
}