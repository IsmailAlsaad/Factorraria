using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Client side of the important dialogue (NPC roadmap patch N2.1): the "!" mark, the chat text and the button clicks.
    /// All three only run for the local player and only read the synced story flags.
    /// </summary>
    public class ImportantDialogueGlobalNPC : GlobalNPC
    {
        // GetChat runs on the local client only (tModLoader docs). It must stay free of side effects: the talk-start work lives in ImportantDialoguePlayer.
        public override void GetChat(NPC npc, ref string chat)
        {
            if (Main.dedServ || !ImportantDialogueRegistry.HasNpc(npc.type))
                return;

            ImportantDialogueEntry entry = ImportantDialogueSystem.FindActive(Main.LocalPlayer, npc.type, true);
            if (entry != null)
                chat = entry.GetText(Main.LocalPlayer);
        }

        // N10.3b: the first chat a freed Mechanic ever has is vanilla's own rescue line. It is replaced (same tick) by the active entry that has
        // ReplacesRescueChat. We cannot rely on GetChat for this, because vanilla may set the rescue line directly (and the story flag MechanicRescued
        // is only set by the server watcher a tick later), so we watch the NPC itself: PostAI remembers every tick a BoundMechanic ran its AI; the
        // first tick the NPC at that index is a Mechanic and the local player is talking to her, the chat text is overwritten. Local client only,
        // no packet; nothing is saved (a bound NPC that is not rescued leaves only a stale tick that expires after RescueWindowTicks).
        public static uint RescueWindowTicks = 300;
        static readonly Dictionary<int, uint> boundMechanicTick = new Dictionary<int, uint>();

        public override void Unload() => boundMechanicTick.Clear();

        public override void PostAI(NPC npc)
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if (npc.type == NPCID.BoundMechanic)
            {
                boundMechanicTick[npc.whoAmI] = Main.GameUpdateCount;
                return;
            }

            if (npc.type != NPCID.Mechanic || boundMechanicTick.Count == 0 || !boundMechanicTick.TryGetValue(npc.whoAmI, out uint lastBound))
                return;

            if (Main.GameUpdateCount - lastBound > RescueWindowTicks)
            {
                boundMechanicTick.Remove(npc.whoAmI);
                return;
            }

            Player player = Main.LocalPlayer;
            if (player.talkNPC != npc.whoAmI)
                return;

            boundMechanicTick.Remove(npc.whoAmI);

            ImportantDialogueEntry entry = ImportantDialogueSystem.FindActive(player, npc.type, true);
            if (entry == null || !entry.ReplacesRescueChat)
                return;

            Main.npcChatText = entry.GetText(player);
            FactorrariaNet.Log("[Story] rescue chat replaced by " + entry.Id);
        }

        public override bool PreChatButtonClicked(NPC npc, bool firstButton)
        {
            // False = one of our buttons was clicked and handled, so the vanilla code for that button must not run.
            return !ImportantDialogueChatButtons.TryHandleClick(npc, firstButton);
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (Main.dedServ || npc.IsABestiaryIconDummy || !ImportantDialogueRegistry.HasNpc(npc.type))
                return;

            Player player = Main.LocalPlayer;

            // Hidden while the player chats with this NPC. (PostDraw only runs for NPCs that are drawn, so an off-screen NPC never shows it.)
            if (player.talkNPC == npc.whoAmI)
                return;

            // N10.2 (polish note 3): the "!" only marks a dialogue this player has not seen yet.
            ImportantDialogueEntry active = ImportantDialogueSystem.FindActive(player, npc.type, false);
            if (active != null && !ImportantDialoguePlayer.HasSeen(player, active.Id))
                ImportantDialogueSystem.DrawMark(spriteBatch, npc, screenPos);
        }
    }
}