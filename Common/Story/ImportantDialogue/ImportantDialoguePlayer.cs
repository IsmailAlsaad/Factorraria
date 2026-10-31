using Factorraria.Common.Net;
using Terraria;
using Terraria.ID;
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
            {
                ImportantDialogueSystem.RunTalkStart(Player, Main.npc[now]);

                // N8.2 (rule D20): any conversation with a Dryad counts, also when she has no active important dialogue. The server decides.
                if (Main.npc[now].type == NPCID.Dryad)
                    StoryNet.RequestDryadTalk(now);
            }
        }
    }
}