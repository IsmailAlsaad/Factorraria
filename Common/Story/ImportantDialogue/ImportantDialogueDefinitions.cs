using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// THE FILE TO EDIT: every important dialogue (NPC roadmap, docs/npcs/ROADMAP.md) is registered here, like ScrollPoolDefinitions.
    /// For each entry:
    ///   Id          unique name; the text lives in Localization under Dialogue: "&lt;Id&gt;.Text" (and "&lt;Id&gt;.Button1" / ".Button2")
    ///   NpcType     who says it
    ///   Priority    higher shows first when one NPC has several active entries
    ///   Condition   when it starts (runs on the local client, may look at the local inventory)
    ///   Exit        when it ends for good (world level, checked by the server); null = never ends by itself
    ///   OnTalkStart optional, once when the player starts talking to the NPC while the entry is active
    ///   Button1/2   optional chat buttons (label key + click callback)
    /// While Condition is true and the entry is not done, the NPC shows "!" and says the same text on every click.
    /// </summary>
    public static class ImportantDialogueDefinitions
    {
        public static void Register()
        {
            // ------------------------------------------------------------------
            // TEST ENTRIES (patch N2.1). Removed in patch N9.
            // Low priority: carry a Torch. Ends when the flag TestDialogueExit is set (/fstory flag TestDialogueExit on).
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Test.Guide",
                NpcType = NPCID.Guide,
                Priority = 0,
                Condition = player => player.HasItem(ItemID.Torch),
                Exit = () => StorySystem.Get(StoryFlags.TestDialogueExit),
                OnTalkStart = (player, npc) => Main.NewText("[Story test] talk start: Test.Guide")
            });

            // High priority: carry Wood. Has a chat button. No exit (retire it with /fstory flag Done.Test.Guide.High on).
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Test.Guide.High",
                NpcType = NPCID.Guide,
                Priority = 10,
                Condition = player => player.HasItem(ItemID.Wood),
                OnTalkStart = (player, npc) => Main.NewText("[Story test] talk start: Test.Guide.High"),
                Button1 = new ImportantDialogueButton
                {
                    OnClick = (player, npc) =>
                    {
                        Main.npcChatText = "[Story test] The test button was clicked.";
                        Main.NewText("[Story test] button clicked on NPC " + npc.whoAmI);
                    }
                }
            });
        }
    }
}