using Factorraria.Common.Net;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Notices the moment the tied Mechanic is freed and acts on it (NPC roadmap patch N10.8, which reworks N10.3b and the rescue watcher of rule S2).
    /// Vanilla's rescue (AI_000_TransformBoundNPC) does exactly: Transform(124) (the same NPC slot, the index is kept), RegisterChatStartWith, Player.SetTalkNPC(whoAmI), and on a
    /// server SendData(40) to that player. It never writes npcChatText itself, and it runs inside the NPC's AI, so Player.talkNPC already points at her when PostAI runs that tick. NPC.savedMech is NOT
    /// set there: the town NPC AI sets it on every tick the NPC runs as a Mechanic (NPC.cs, the switch on type, case 124), so it turns on one AI tick AFTER the transform. Two
    /// independent signals are used, so nothing hangs on a single one:
    ///  - the SWITCH: ImportantDialogueGlobalNPC.PostAI calls OnAi for every Bound Mechanic and Mechanic AI tick. A BoundMechanic stamps its slot every tick; the
    ///    first AI tick the same slot runs as a Mechanic within RescueWindowTicks of that stamp is the rescue (Freed).
    ///  - NPC.savedMech turning on while the local player talks to a Mechanic (Tick), used only when the switch was not seen.
    /// What each side does with it:
    ///  - SERVER / SINGLE-PLAYER: the switch sets a one-shot signal that RareNpcSystem.WatchRescue reads (TakeRescueSignal) next to NPC.savedMech.
    ///  - LOCAL CLIENT (single-player included): the rescue chat is replaced. For RescueWindowTicks the tracker waits for Player.talkNPC to point at her, then
    ///    evaluates the entry flagged ReplacesRescueChat with its RescueCondition (world flags only: "Done" and "seen" do not apply to a rescue), overwrites
    ///    Main.npcChatText, marks the entry seen and keeps the text for RescueGraceTicks in case vanilla writes its own line again. Every outcome is logged:
    ///    "[Story] rescue chat replaced by &lt;id&gt;" or "[Story] rescue chat not replaced: &lt;reason&gt;".
    ///  - AUTHORITY / SYNC: no packet. The chat overwrite is local and cosmetic; the world flags are still written only by the server.
    /// It does not depend on the order in which vanilla sets the chat text, Player.talkNPC and NPC.savedMech inside its rescue code.
    /// </summary>
    public static class MechanicRescueTracker
    {
        // Tunables (not const, so hot reload can change them).
        public static uint RescueWindowTicks = 300;   // how long after the switch the local player may still open the chat for the replacement
        public static uint RescueGraceTicks = 8;      // after the replacement, the text is put back this long if vanilla writes its own line again

        sealed class Rescue
        {
            public uint Armed;
            public bool Decided;
            public string Text;
            public uint ReplacedTick;
        }

        static readonly Dictionary<int, uint> boundTick = new Dictionary<int, uint>();
        static readonly Dictionary<int, Rescue> chat = new Dictionary<int, Rescue>();
        static bool serverSignal;
        static bool savedInit;
        static bool lastSaved;
        static int lastTalk = -1;
        static uint talkStartTick;

        /// <summary>Server / single-player: true once after a tied Mechanic turned into the town Mechanic on this side. Read by RareNpcSystem.WatchRescue.</summary>
        public static bool TakeRescueSignal()
        {
            bool signal = serverSignal;
            serverSignal = false;
            return signal;
        }

        public static void Reset()
        {
            boundTick.Clear();
            chat.Clear();
            serverSignal = false;
            savedInit = false;
            lastSaved = false;
            lastTalk = -1;
            talkStartTick = 0;
        }

        /// <summary>Called from GlobalNPC.PostAI for NPCID.BoundMechanic and NPCID.Mechanic, on every side that runs the NPC's AI.</summary>
        public static void OnAi(NPC npc)
        {
            if (Main.gameMenu)
                return;

            if (npc.type == NPCID.BoundMechanic)
            {
                boundTick[npc.whoAmI] = Main.GameUpdateCount;
                return;
            }

            if (npc.type != NPCID.Mechanic)
                return;

            if (boundTick.TryGetValue(npc.whoAmI, out uint last))
            {
                boundTick.Remove(npc.whoAmI);
                if (Main.GameUpdateCount - last <= RescueWindowTicks)
                    Freed(npc);
            }

            if (chat.Count > 0)
                Evaluate(npc.whoAmI);
        }

        static void Freed(NPC npc)
        {
            FactorrariaNet.Log("[Story] the tied Mechanic was freed (NPC #" + npc.whoAmI + " runs as the town Mechanic now; NPC.savedMech " + NPC.savedMech + ")");

            if (Main.netMode != NetmodeID.MultiplayerClient)
                serverSignal = true;

            if (!Main.dedServ && !chat.ContainsKey(npc.whoAmI))
                chat[npc.whoAmI] = new Rescue { Armed = Main.GameUpdateCount };
        }

        /// <summary>
        /// Local client: while a rescue chat is pending (armed, until its grace ticks end) the GetChat hook asks this first. Returns the rescue entry's text, or null.
        /// Vanilla's SetTalkNPC does not write the chat text itself, so the first line is produced later through GetChat, when savedMech and MechanicRescued are still
        /// false and the entry's normal Condition says no; this makes that path say the rescue line too.
        /// </summary>
        public static string PendingRescueText(NPC npc)
        {
            if (Main.dedServ || npc.type != NPCID.Mechanic || !chat.ContainsKey(npc.whoAmI))
                return null;

            ImportantDialogueEntry entry = FindRescueEntry();
            if (entry == null)
                return null;

            Player player = Main.LocalPlayer;
            bool ok;
            try
            {
                ok = entry.RescueCondition != null ? entry.RescueCondition(player) : entry.Condition(player);
            }
            catch (Exception e)
            {
                ImportantDialogueSystem.ReportOnce(entry.Id + ".RescueCondition", e);
                ok = false;
            }

            return ok ? entry.GetText(player) : null;
        }

        /// <summary>Local client, once per tick (PostUpdateEverything): the savedMech fallback and the evaluation of every pending rescue chat.</summary>
        public static void Tick()
        {
            if (Main.gameMenu || Main.dedServ)
                return;

            uint now = Main.GameUpdateCount;
            int talk = Main.LocalPlayer.talkNPC;
            if (talk != lastTalk)
            {
                lastTalk = talk;
                talkStartTick = now;
            }

            bool saved = NPC.savedMech;
            if (!savedInit)
            {
                savedInit = true;
                lastSaved = saved;
            }
            else if (saved && !lastSaved)
            {
                lastSaved = true;
                ArmFromSavedMech(talk, now);
            }
            else
            {
                lastSaved = saved;
            }

            if (chat.Count == 0)
                return;

            List<int> indexes = new List<int>(chat.Keys);
            foreach (int index in indexes)
                Evaluate(index);
        }

        // Fallback: NPC.savedMech turned on while the local player was talking to a Mechanic that started the conversation recently, and the switch was not seen.
        static void ArmFromSavedMech(int talk, uint now)
        {
            if (talk < 0 || talk >= Main.maxNPCs)
                return;

            NPC npc = Main.npc[talk];
            if (!npc.active || npc.type != NPCID.Mechanic || chat.ContainsKey(talk))
                return;

            if (now - talkStartTick > RescueWindowTicks)
                return;

            boundTick.Remove(talk);
            chat[talk] = new Rescue { Armed = now };
            FactorrariaNet.Log("[Story] NPC.savedMech turned on while the local player was talking to the Mechanic (NPC #" + talk + ") and the BoundMechanic switch was not seen: the rescue chat is armed from this signal");
        }

        static ImportantDialogueEntry FindRescueEntry()
        {
            foreach (ImportantDialogueEntry entry in ImportantDialogueRegistry.ForNpc(NPCID.Mechanic))
            {
                if (entry.ReplacesRescueChat)
                    return entry;
            }

            return null;
        }

        static string FlagSummary()
        {
            return "EyeKillSeen " + (StorySystem.Get(StoryFlags.EyeKillSeen) ? "on" : "off") + ", DungeonFork " + (StorySystem.Get(StoryFlags.DungeonFork) ? "on" : "off");
        }

        static void Evaluate(int index)
        {
            if (!chat.TryGetValue(index, out Rescue rescue))
                return;

            uint now = Main.GameUpdateCount;
            NPC npc = Main.npc[index];
            bool gone = !npc.active || npc.type != NPCID.Mechanic;
            Player player = Main.LocalPlayer;

            if (!rescue.Decided)
            {
                if (gone || now - rescue.Armed > RescueWindowTicks)
                {
                    chat.Remove(index);
                    FactorrariaNet.Log("[Story] rescue chat not replaced: " + (gone
                        ? "NPC #" + index + " is no longer the Mechanic"
                        : "the local player was not talking to her within " + RescueWindowTicks + " ticks (talkNPC " + player.talkNPC + ", expected " + index + ")"));
                    return;
                }

                if (player.talkNPC != index)
                    return;

                rescue.Decided = true;

                ImportantDialogueEntry entry = FindRescueEntry();
                if (entry == null)
                {
                    chat.Remove(index);
                    FactorrariaNet.Log("[Story] rescue chat not replaced: no important dialogue entry has ReplacesRescueChat");
                    return;
                }

                bool ok;
                try
                {
                    ok = entry.RescueCondition != null ? entry.RescueCondition(player) : entry.Condition(player);
                }
                catch (Exception e)
                {
                    ImportantDialogueSystem.ReportOnce(entry.Id + ".RescueCondition", e);
                    ok = false;
                }

                if (!ok)
                {
                    chat.Remove(index);
                    FactorrariaNet.Log("[Story] rescue chat not replaced: the rescue condition of " + entry.Id + " is false (" + FlagSummary() + ")");
                    return;
                }

                rescue.Text = entry.GetText(player);
                rescue.ReplacedTick = now;
                Main.npcChatText = rescue.Text;
                ImportantDialoguePlayer.MarkSeen(player, entry.Id);
                FactorrariaNet.Log("[Story] rescue chat replaced by " + entry.Id + " (" + FlagSummary() + ")");
                return;
            }

            // Replaced: for a few ticks, put the text back if vanilla wrote its own line again, then let go.
            if (gone || player.talkNPC != index || now - rescue.ReplacedTick > RescueGraceTicks)
            {
                chat.Remove(index);
                return;
            }

            if (Main.npcChatText != rescue.Text)
                Main.npcChatText = rescue.Text;
        }
    }

    /// <summary>Hooks the static MechanicRescueTracker into the world lifetime and the per-tick update.</summary>
    public class MechanicRescueSystem : ModSystem
    {
        public override void OnWorldLoad() => MechanicRescueTracker.Reset();

        public override void OnWorldUnload() => MechanicRescueTracker.Reset();

        public override void Unload() => MechanicRescueTracker.Reset();

        public override void PostUpdateEverything() => MechanicRescueTracker.Tick();
    }
}