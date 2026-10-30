using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Detects the moment the local player starts a conversation (Player.talkNPC goes from none to an NPC) and runs OnTalkStart of the active
    /// entry once. NPC roadmap patch N2.1. Local player only: other players' conversations are not ours to run.
    /// </summary>
    public class ImportantDialoguePlayer : ModPlayer
    {
        int lastTalkNpc = -1;

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            int now = Player.talkNPC;
            if (now == lastTalkNpc)
                return;

            lastTalkNpc = now;
            if (now >= 0 && now < Main.maxNPCs && Main.npc[now].active)
                ImportantDialogueSystem.RunTalkStart(Player, Main.npc[now]);
        }
    }
}