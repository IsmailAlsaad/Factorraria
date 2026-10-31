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
            // Dryad dialogues (patch N6.1). Text only (no chat buttons), so her vanilla buttons stay as they are.
            // Dryad.RescueMechanic (dialogue 1): starts once the Eye of Cthulhu was killed (our flag EyeKillSeen, so a Dryad who arrived the vanilla way
            // also says it) and never on the Skeletron-first route. Ends when the Mechanic is rescued AND housed (D8; DryadStory.RequireHouseForDryadExit).
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Dryad.RescueMechanic",
                NpcType = NPCID.Dryad,
                Priority = 5,
                Condition = player => StorySystem.Get(StoryFlags.EyeKillSeen) && !StorySystem.Get(StoryFlags.DungeonFork),
                Exit = () => StorySystem.Get(StoryFlags.MechanicRescued) && (!DryadStory.RequireHouseForDryadExit || DryadStory.MechanicHoused())
            });

            // Dryad.PlanWithMechanic (dialogue 2): from the moment dialogue 1 is done until the Mechanic handed out the scrolls (ScrollsGiven is set in N7.1;
            // until then test it with /fstory flag ScrollsGiven on).
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Dryad.PlanWithMechanic",
                NpcType = NPCID.Dryad,
                Priority = 5,
                Condition = player => StorySystem.Get(StoryFlags.DoneKey("Dryad.RescueMechanic")) && !StorySystem.Get(StoryFlags.ScrollsGiven) && !StorySystem.Get(StoryFlags.DungeonFork),
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
            // Mechanic.SuperWeaponIntro (patch N7.1): right after her rescue the Mechanic shows "!" and explains super weapon #1 (the missile: drill,
            // tank, thruster). The scroll hand-out is an OnTalkStart request (not a chat button): the Mechanic's two vanilla chat buttons (Shop and
            // Happiness) are both used, so a button would have to hide her Shop. The first conversation sends ClaimScrolls; the server hands out
            // three Sealed Scrolls ONCE per world, to the first player who asks (D9), and sets ScrollsGiven. If a request is refused (too far,
            // she is gone) nothing is lost: the entry stays active and the next conversation asks again.
            // Condition: she was rescued (also on the Skeletron-first route, D11), a player had a real conversation with a Dryad (D20, N8.2: the
            // world flag DryadTalked, set by the server) and the scrolls were not given. Exit: ScrollsGiven, which also
            // ends Dryad.PlanWithMechanic. Priority 6: above Mechanic.Bag (5).
            ImportantDialogueRegistry.Register(new ImportantDialogueEntry
            {
                Id = "Mechanic.SuperWeaponIntro",
                NpcType = NPCID.Mechanic,
                Priority = 6,
                Condition = player => StorySystem.Get(StoryFlags.MechanicRescued) && !StorySystem.Get(StoryFlags.ScrollsGiven) && StorySystem.Get(StoryFlags.DryadTalked),
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