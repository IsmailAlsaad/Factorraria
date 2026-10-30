using Factorraria.Common.Net;
using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Puts the active important dialogue's buttons into the NPC chat window (NPC roadmap patch N2.1). Client only.
    /// tModLoader has SetChatButtons only on ModNPC, not on GlobalNPC, so this detours NPCLoader.SetChatButtons (public static,
    /// called every frame the chat window is open, after the vanilla labels are set) and adds our labels to the first FREE slot.
    /// A vanilla button (Shop, Help, ...) therefore stays first; vanilla Close is always separate. Clicks come back through
    /// GlobalNPC.PreChatButtonClicked (ImportantDialogueGlobalNPC), which asks TryHandleClick whether the slot was ours.
    /// tModLoader removes the detour when the mod unloads.
    /// </summary>
    public static class ImportantDialogueChatButtons
    {
        public delegate void OrigSetChatButtons(ref string button, ref string button2);

        public delegate void HookSetChatButtons(OrigSetChatButtons orig, ref string button, ref string button2);

        // Which of the entry's buttons (0 = Button1, 1 = Button2) sits in chat slot 0 (first button) and slot 1 (second button); -1 = a vanilla button or empty.
        static readonly int[] slotButton = { -1, -1 };
        static ImportantDialogueEntry slotEntry;
        static int slotNpc = -1;
        static readonly System.Collections.Generic.HashSet<string> reportedFull = new System.Collections.Generic.HashSet<string>();

        public static void Install()
        {
            MethodInfo method = typeof(NPCLoader).GetMethod(nameof(NPCLoader.SetChatButtons), BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                FactorrariaNet.Log("[Story] NPCLoader.SetChatButtons not found: important dialogue chat buttons are disabled");
                return;
            }

            MonoModHooks.Add(method, (HookSetChatButtons)SetChatButtonsDetour);
        }

        static void SetChatButtonsDetour(OrigSetChatButtons orig, ref string button, ref string button2)
        {
            orig(ref button, ref button2);

            slotButton[0] = -1;
            slotButton[1] = -1;
            slotEntry = null;
            slotNpc = -1;

            try
            {
                Player player = Main.LocalPlayer;
                NPC npc = player.TalkNPC;
                if (npc == null)
                    return;

                ImportantDialogueEntry entry = ImportantDialogueSystem.FindActive(player, npc.type, false);
                if (entry == null)
                    return;

                slotEntry = entry;
                slotNpc = npc.whoAmI;
                Assign(entry, 0, ref button, ref button2);
                Assign(entry, 1, ref button, ref button2);
            }
            catch (Exception e)
            {
                ImportantDialogueSystem.ReportOnce("ChatButtons", e);
            }
        }

        static void Assign(ImportantDialogueEntry entry, int index, ref string button, ref string button2)
        {
            ImportantDialogueButton def = index == 0 ? entry.Button1 : entry.Button2;
            if (def == null)
                return;

            string label = entry.GetButtonLabel(index);
            if (string.IsNullOrEmpty(button))
            {
                button = label;
                slotButton[0] = index;
            }
            else if (string.IsNullOrEmpty(button2))
            {
                button2 = label;
                slotButton[1] = index;
            }
            // Both slots are taken by vanilla buttons: this button is not shown. Say so once, with the vanilla labels.
            else if (reportedFull.Add(entry.Id + "." + index))
                FactorrariaNet.Log("[Story] button " + (index + 1) + " of " + entry.Id + " is NOT shown: both chat button slots are used by vanilla (\"" + button + "\" and \"" + button2 + "\")");
        }

        /// <summary>Called from PreChatButtonClicked. True = the clicked slot was one of ours and its callback ran (vanilla must not run).</summary>
        public static bool TryHandleClick(NPC npc, bool firstButton)
        {
            if (Main.dedServ || slotEntry == null || npc.whoAmI != slotNpc)
                return false;

            int index = slotButton[firstButton ? 0 : 1];
            if (index < 0)
                return false;

            ImportantDialogueButton def = index == 0 ? slotEntry.Button1 : slotEntry.Button2;
            if (def?.OnClick == null)
                return true;

            try
            {
                def.OnClick(Main.LocalPlayer, npc);
            }
            catch (Exception e)
            {
                ImportantDialogueSystem.ReportOnce(slotEntry.Id + ".Button" + (index + 1), e);
            }

            return true;
        }
    }
}