using Factorraria.Common.Net;
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
    ///   Button1/2   optional chat buttons (label key + click callback; ReplaceVanillaFirstButton = true takes the place of the vanilla first button)
    /// While Condition is true and the entry is not done, the NPC shows "!" and says the same text on every click.
    /// </summary>
    public static class ImportantDialogueDefinitions
    {
        public static void Register()
        {
            // ------------------------------------------------------------------
            // Mechanic.Bag (patch N5.1): the player carries the Mechanic's Bag and her shop is still locked. She asks "Have you found my bag?".
            // The vanilla first button ("Shop") is REPLACED by "Give the bag" while this entry is active; it sends the TurnInBag request and the
            // server decides (it consumes one Bag and sets MechanicShopUnlocked). Exit: the shop is unlocked (latches Done.Mechanic.Bag).
            // Priority 5: the scroll intro of N7.1 will get its own priority; the Bag is the later step of the story.
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Mechanic.Bag",
                NpcType = NPCID.Mechanic,
                Priority = 5,
                Condition = player => !StorySystem.Get(StoryFlags.MechanicShopUnlocked) && StoryNet.HasBag(player),
                Exit = () => StorySystem.Get(StoryFlags.MechanicShopUnlocked),
                Button1 = new ImportantDialogueButton
                {
                    ReplaceVanillaFirstButton = true,
                    OnClick = (player, npc) => StoryNet.RequestTurnInBag(npc.whoAmI)
                }
            });

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

            // Merchant: carry Wood. Same button test on an NPC that normally has a free second chat slot (the Guide may use both for Help and Crafting).
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Test.Merchant",
                NpcType = NPCID.Merchant,
                Priority = 0,
                Condition = player => player.HasItem(ItemID.Wood),
                Button1 = new ImportantDialogueButton
                {
                    OnClick = (player, npc) =>
                    {
                        Main.npcChatText = "[Story test] The Merchant test button was clicked.";
                        Main.NewText("[Story test] button clicked on NPC " + npc.whoAmI);
                    }
                }
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