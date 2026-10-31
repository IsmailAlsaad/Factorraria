using System;
using Terraria;
using Terraria.Localization;

namespace Factorraria.Common.Story
{
    /// <summary>One chat button an important dialogue adds to the NPC's chat window. Runs on the local client only.</summary>
    public class ImportantDialogueButton
    {
        /// <summary>Localization key of the label. Null = "Mods.Factorraria.Dialogue.&lt;Id&gt;.Button1" (or Button2).</summary>
        public string LabelKey;

        /// <summary>Called on the local client when the player clicks the button. Send a request to the server from here, do not change world state directly.</summary>
        public Action<Player, NPC> OnClick;

        /// <summary>
        /// False (default): the button takes the first FREE chat slot, a vanilla Shop / Help button stays first.
        /// True: the button REPLACES the vanilla first button (chat slot 1) while the entry is active, and the vanilla action of that button
        /// does not run (its click is cancelled). The vanilla second button and Close stay. Only one button of an entry can take the first slot;
        /// a second one that asks for it falls back to the first free slot. Example: the Mechanic's "Shop" becomes "Give the bag" (N5.1).
        /// </summary>
        public bool ReplaceVanillaFirstButton;
    }

    /// <summary>
    /// One important dialogue (NPC roadmap, docs/npcs/ROADMAP.md section 3). Registered in ImportantDialogueDefinitions.
    /// An NPC with an active entry shows "!" and says the entry's text on every click, until the entry's Exit turns true.
    /// </summary>
    public class ImportantDialogueEntry
    {
        public static string KeyPrefix = "Mods.Factorraria.Dialogue.";

        /// <summary>Unique id, for example "Dryad.RescueMechanic". The finished flag is "Done.&lt;Id&gt;".</summary>
        public string Id;

        /// <summary>The NPC type that says it (a vanilla NPCID or a ModContent.NPCType).</summary>
        public int NpcType;

        /// <summary>When several entries of one NPC are active, the highest Priority shows.</summary>
        public int Priority;

        /// <summary>When the entry starts. Runs on the LOCAL client (it may look at the local inventory). Must be cheap and free of side effects.</summary>
        public Func<Player, bool> Condition;

        /// <summary>
        /// When the entry ends for good. World level only (no player). Runs on the server or single-player every DialogueExitCheckTicks.
        /// When it returns true the server sets "Done.&lt;Id&gt;" and the entry never comes back. Null = it never ends by itself.
        /// </summary>
        public Func<bool> Exit;

        /// <summary>Optional. Runs once on the local client when the player starts a conversation with the NPC while this entry is active.</summary>
        public Action<Player, NPC> OnTalkStart;

        /// <summary>Optional chat buttons (up to two). A button takes the first free button slot of the chat window, so a vanilla button (Shop, Help) stays first, unless the button sets ReplaceVanillaFirstButton. The vanilla Close button is always there as well.</summary>
        public ImportantDialogueButton Button1;
        public ImportantDialogueButton Button2;

        /// <summary>Optional. Null = "Mods.Factorraria.Dialogue.&lt;Id&gt;.Text".</summary>
        public string TextKey;

        /// <summary>
        /// Optional (N10.3b, reworked in N10.8). True = this entry's text REPLACES the vanilla chat line of a bound NPC's rescue (the very first chat the
        /// freed NPC ever has). Done by MechanicRescueTracker on the local client; nothing else changes (no "!", no buttons are involved).
        /// Only meaningful for the Mechanic today (NPCID.BoundMechanic turning into NPCID.Mechanic).
        /// </summary>
        public bool ReplacesRescueChat;

        /// <summary>
        /// Optional (N10.8). Used instead of Condition when the rescue chat is replaced (ReplacesRescueChat). Null = Condition. It should describe the route
        /// only (world flags), not the entry's own progress: a rescue happens once, so "Done" and "seen" must not stop it (a repeated test rescue in the same
        /// world, or an exit that already latched, would otherwise skip it).
        /// </summary>
        public Func<Player, bool> RescueCondition;

        /// <summary>
        /// Optional (N10.2, polish note 2). Localization key of a REMINDER text: once the local player has seen this entry (a conversation that opened with
        /// it has ended), the chat says this instead of the normal text, until the entry's Exit turns true. Null = the normal text every time.
        /// The "!" itself disappears after the first conversation for every entry (polish note 3), with or without a reminder.
        /// </summary>
        public string ReminderTextKey;

        public bool IsDone => StorySystem.Get(StoryFlags.DoneKey(Id));

        public string ResolveTextKey() => TextKey ?? (KeyPrefix + Id + ".Text");

        /// <summary>The chat text, with {PlayerName} replaced.</summary>
        public string GetText(Player player)
        {
            string key = ResolveTextKey();
            if (ReminderTextKey != null && player != null && ImportantDialoguePlayer.HasSeen(player, Id))
                key = ReminderTextKey;
            string text = Language.Exists(key) ? Language.GetTextValue(key) : "[missing text: " + key + "]";
            return text.Replace("{PlayerName}", player != null ? player.name : "");
        }

        /// <summary>The label of button 0 (Button1) or 1 (Button2).</summary>
        public string GetButtonLabel(int index)
        {
            ImportantDialogueButton button = index == 0 ? Button1 : Button2;
            if (button == null)
                return "";

            string key = button.LabelKey ?? (KeyPrefix + Id + ".Button" + (index + 1));
            return Language.Exists(key) ? Language.GetTextValue(key) : "[" + key + "]";
        }
    }
}