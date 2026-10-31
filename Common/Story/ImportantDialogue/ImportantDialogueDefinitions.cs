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
    ///   ReminderTextKey optional (N10.2): the chat text once this player has seen the entry (a conversation that opened with it has ended); null = the same text
    ///   Button1/2   optional chat buttons (label key + click callback; ReplaceVanillaFirstButton = true takes the place of the vanilla first button)
    /// While Condition is true and the entry is not done, the NPC says the entry's text on every click (or the reminder, N10.2) and shows "!" until the local player has seen the entry once (N10.2).
    /// </summary>
    public static class ImportantDialogueDefinitions
    {
        public static void Register()
        {
            // ------------------------------------------------------------------
            // Dryad dialogues (patch N6.1). Text only (no chat buttons), so her vanilla buttons stay as they are.
            // Dryad.RescueMechanic (dialogue 1): N10.1d: the version WITH the thanks, only for the Dryad who arrived through the Eye of Cthulhu route (our flag DryadFromEye,
            // N10.1). Every other Dryad says Dryad.RescueMechanicOther below instead (same dialogue, no thanks). Neither shows on the Skeletron-first route. Ends when the Mechanic is rescued AND housed (D8; DryadStory.RequireHouseForDryadExit).
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Dryad.RescueMechanic",
                ReminderTextKey = ImportantDialogueEntry.KeyPrefix + "Dryad.RescueReminder",
                NpcType = NPCID.Dryad,
                Priority = 5,
                Condition = player => StorySystem.Get(StoryFlags.DryadFromEye) && !StorySystem.Get(StoryFlags.DungeonFork),
                Exit = () => StorySystem.Get(StoryFlags.MechanicRescued) && (!DryadStory.RequireHouseForDryadExit || DryadStory.MechanicHoused())
            });

            // Dryad.RescueMechanicOther (dialogue 1 for every Dryad who did NOT come through the Eye route, N10.1d): the same request without the thanks
            // (nobody saved her). Condition is the exact opposite of Dryad.RescueMechanic, so only one of the two is ever active. Same exit.
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Dryad.RescueMechanicOther",
                ReminderTextKey = ImportantDialogueEntry.KeyPrefix + "Dryad.RescueReminder",
                NpcType = NPCID.Dryad,
                Priority = 5,
                Condition = player => !StorySystem.Get(StoryFlags.DryadFromEye) && !StorySystem.Get(StoryFlags.DungeonFork),
                Exit = () => StorySystem.Get(StoryFlags.MechanicRescued) && (!DryadStory.RequireHouseForDryadExit || DryadStory.MechanicHoused())
            });

            // Dryad.PlanWithMechanic (dialogue 2, N10.1c: any Dryad, Eye route or not): from the moment dialogue 1 is done (it ends, or was skipped because the Mechanic was already rescued and housed) until the Mechanic handed out the scrolls (ScrollsGiven is set in N7.1;
            // until then test it with /fstory flag ScrollsGiven on).
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Dryad.PlanWithMechanic",
                NpcType = NPCID.Dryad,
                Priority = 5,
                Condition = player => (StorySystem.Get(StoryFlags.DoneKey("Dryad.RescueMechanic")) || StorySystem.Get(StoryFlags.DoneKey("Dryad.RescueMechanicOther"))) && !StorySystem.Get(StoryFlags.ScrollsGiven) && !StorySystem.Get(StoryFlags.DungeonFork),
                Exit = () => StorySystem.Get(StoryFlags.ScrollsGiven)
            });

            // ------------------------------------------------------------------
            // Mechanic.Bag (patch N5.1): the player carries the Mechanic's Bag and her shop is still locked. She asks "Have you found my bag?".
            // The vanilla first button ("Shop") is REPLACED by "Give the bag" while this entry is active; it sends the TurnInBag request and the
            // server decides (it consumes one Bag and sets MechanicShopUnlocked). Exit: the shop is unlocked (latches Done.Mechanic.Bag).
            // Priority 5: lower than Mechanic.SuperWeaponIntro (6), because the scrolls are the earlier step of the story. If a player carries the
            // Bag before the scrolls were handed out, the intro shows first, and the Bag entry takes over as soon as the scrolls are given.
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
            // Mechanic.RescuedEye (patch N10.3, polish note 4; reworked in N10.3b): the special line of a Mechanic rescued on the Eye route (EyeKillSeen on,
            // no DungeonFork). It REPLACES the vanilla rescue chat (the very first chat she ever has, ReplacesRescueChat, see ImportantDialogueGlobalNPC.PostAI)
            // and is said once: Exit is MechanicRescued, which is already true when she is freed, so Done.Mechanic.RescuedEye latches within
            // DialogueExitCheckTicks of the rescue and the entry never comes back. The Condition accepts vanilla's NPC.savedMech as well, because the world flag
            // MechanicRescued is only set by the server watcher a tick after the rescue (too late for the rescue chat itself). No "!" is involved: the
            // player opens this chat by freeing her. Priority 7 stays above Mechanic.SuperWeaponIntro (6). It hands out nothing: the scrolls are claimed in the
            // next conversation by Mechanic.SuperWeaponIntro (once she is housed and a Dryad was talked to). Text: Ismail's own wording in Localization.
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Mechanic.RescuedEye",
                NpcType = NPCID.Mechanic,
                Priority = 7,
                ReplacesRescueChat = true,
                RescueCondition = player => StorySystem.Get(StoryFlags.EyeKillSeen) && !StorySystem.Get(StoryFlags.DungeonFork),   // N10.8: the Eye route only; "Done" and "seen" do not stop the rescue line
                Condition = player => (StorySystem.Get(StoryFlags.MechanicRescued) || NPC.savedMech) && StorySystem.Get(StoryFlags.EyeKillSeen) && !StorySystem.Get(StoryFlags.DungeonFork)
                    && !StorySystem.Get(StoryFlags.ScrollsGiven) && !ImportantDialoguePlayer.HasSeen(player, "Mechanic.RescuedEye"),
                Exit = () => StorySystem.Get(StoryFlags.MechanicRescued)
            });

            // Mechanic.SuperWeaponIntro (patch N7.1): right after her rescue the Mechanic shows "!" and explains super weapon #1 (the missile: drill,
            // tank, thruster). The scroll hand-out is an OnTalkStart request (not a chat button): the Mechanic's two vanilla chat buttons (Shop and
            // Happiness) are both used, so a button would have to hide her Shop. The first conversation sends ClaimScrolls; the server hands out
            // three Sealed Scrolls ONCE per world, to the first player who asks (D9), and sets ScrollsGiven. If a request is refused (too far,
            // she is gone) nothing is lost: the entry stays active and the next conversation asks again.
            // Condition: she was rescued (also on the Skeletron-first route, D11), a player had a real conversation with a Dryad (D20, N8.2: the
            // world flag DryadTalked, set by the server), she has a house (N10.2b: she is not homeless) and the scrolls were not given. Exit: ScrollsGiven, which also
            // ends Dryad.PlanWithMechanic. Priority 6: above Mechanic.Bag (5).
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Mechanic.SuperWeaponIntro",
                NpcType = NPCID.Mechanic,
                Priority = 6,
                Condition = player => StorySystem.Get(StoryFlags.MechanicRescued) && !StorySystem.Get(StoryFlags.ScrollsGiven) && StorySystem.Get(StoryFlags.DryadTalked) && DryadStory.MechanicHoused(),
                Exit = () => StorySystem.Get(StoryFlags.ScrollsGiven),
                OnTalkStart = (player, npc) => StoryNet.RequestClaimScrolls(npc.whoAmI)
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