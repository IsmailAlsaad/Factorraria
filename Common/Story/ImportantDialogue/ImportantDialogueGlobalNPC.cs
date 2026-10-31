using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
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