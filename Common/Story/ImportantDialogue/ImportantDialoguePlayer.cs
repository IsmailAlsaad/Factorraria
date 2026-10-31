using Factorraria.Common.Net;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Local player side of the important dialogue (NPC roadmap patches N2.1, N8.2 and N10.2).
    ///  - Detects the moment the local player starts a conversation (Player.talkNPC goes from none to an NPC) and runs OnTalkStart of the active entry once.
    ///  - N8.2: any conversation with a Dryad is reported to the server (rule D20, flag DryadTalked).
    ///  - N10.2 (polish notes 2 and 3): remembers which important dialogues THIS character has seen, per world, saved in the character file. A dialogue counts
    ///    as seen when a conversation that opened with it ENDS (so the text of that conversation never changes underneath the player). The "!" is only drawn
    ///    for an entry the player has not seen, and an entry with a ReminderTextKey says the reminder from then on. Purely local and cosmetic: no packet, the
    ///    server never needs it, and another player's "seen" state is independent.
    /// Local player only: other players' conversations are not ours to run.
    /// </summary>
    public class ImportantDialoguePlayer : ModPlayer
    {
        int lastTalkNpc = -1;
        string talkEntryId;                                   // the entry that was active when the current conversation opened
        readonly HashSet<string> seen = new HashSet<string>(); // "<worldID>:<worldName>|<entry id>"

        // Seen entries are remembered per world, because one character can enter several worlds.
        static string WorldPrefix() => Main.worldID + ":" + Main.worldName + "|";

        public static bool HasSeen(Player player, string id)
        {
            if (player == null || id == null)
                return false;

            return player.GetModPlayer<ImportantDialoguePlayer>().seen.Contains(WorldPrefix() + id);
        }

        public static void MarkSeen(Player player, string id)
        {
            if (player == null || id == null)
                return;

            player.GetModPlayer<ImportantDialoguePlayer>().seen.Add(WorldPrefix() + id);
        }

        /// <summary>The entry ids this character has seen in the current world.</summary>
        public static List<string> SeenIds(Player player)
        {
            List<string> ids = new List<string>();
            if (player == null)
                return ids;

            string prefix = WorldPrefix();
            foreach (string key in player.GetModPlayer<ImportantDialoguePlayer>().seen)
                if (key.StartsWith(prefix, System.StringComparison.Ordinal))
                    ids.Add(key.Substring(prefix.Length));

            ids.Sort(System.StringComparer.Ordinal);
            return ids;
        }

        /// <summary>Forgets one entry (id) or every entry of the current world (id null). Returns how many were forgotten.</summary>
        public static int ForgetSeen(Player player, string id)
        {
            if (player == null)
                return 0;

            HashSet<string> set = player.GetModPlayer<ImportantDialoguePlayer>().seen;
            string prefix = WorldPrefix();
            return set.RemoveWhere(key => id == null ? key.StartsWith(prefix, System.StringComparison.Ordinal) : key == prefix + id);
        }

        public override void SaveData(TagCompound tag)
        {
            if (seen.Count > 0)
                tag["Seen"] = new List<string>(seen);
        }

        public override void LoadData(TagCompound tag)
        {
            seen.Clear();
            if (!tag.ContainsKey("Seen"))
                return;

            foreach (string key in tag.GetList<string>("Seen"))
                if (!string.IsNullOrEmpty(key))
                    seen.Add(key);
        }

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            int now = Player.talkNPC;
            if (now == lastTalkNpc)
                return;

            int previous = lastTalkNpc;
            lastTalkNpc = now;

            // N10.2: a conversation ended (or switched to another NPC): the entry it opened with counts as seen now.
            if (previous >= 0 && talkEntryId != null)
                MarkSeen(Player, talkEntryId);

            talkEntryId = null;

            if (now >= 0 && now < Main.maxNPCs && Main.npc[now].active)
            {
                NPC npc = Main.npc[now];
                talkEntryId = ImportantDialogueSystem.FindActive(Player, npc.type, true)?.Id;
                ImportantDialogueSystem.RunTalkStart(Player, npc);

                // N8.2 (rule D20): any conversation with a Dryad counts, also when she has no active important dialogue. The server decides.
                if (npc.type == NPCID.Dryad)
                    StoryNet.RequestDryadTalk(now);
            }
        }
    }
}