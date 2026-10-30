using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// The important dialogue framework (NPC roadmap patch N2.1).
    ///  - AUTHORITY: the server (or single-player) checks every entry's Exit each DialogueExitCheckTicks and latches "Done.&lt;Id&gt;" through StorySystem.ServerSet.
    ///  - CLIENT ONLY (local): Condition, the "!" mark, the chat text, the chat buttons and OnTalkStart. They only read the synced flags.
    ///  - No packets of its own: the Done flags travel with the story flag sync of patch N1.1.
    /// </summary>
    public class ImportantDialogueSystem : ModSystem
    {
        // Tunables (not const, so hot reload can change them).
        public static int DialogueExitCheckTicks = 30;
        public static int ConditionCacheTicks = 10;
        public static float ExclamationBobPixels = 2f;
        public static float ExclamationOffsetY = 10f;
        public static string ExclamationTexturePath = "Factorraria/Common/UI/CustomUIElements/Exclamation";


        struct CachedActive
        {
            public uint Tick;
            public int Version;
            public ImportantDialogueEntry Entry;
        }

        static readonly Dictionary<int, CachedActive> cache = new Dictionary<int, CachedActive>();
        static readonly HashSet<string> reportedErrors = new HashSet<string>();
        static Asset<Texture2D> markTexture;
        static bool markTextureTried;

        public override void Load()
        {
            ImportantDialogueRegistry.Clear();
            ImportantDialogueDefinitions.Register();

            if (!Main.dedServ)
                ImportantDialogueChatButtons.Install();
        }

        public override void Unload()
        {
            ImportantDialogueRegistry.Clear();
            cache.Clear();
            reportedErrors.Clear();
            markTexture = null;
            markTextureTried = false;
        }

        public override void OnWorldLoad() => cache.Clear();

        public override void OnWorldUnload() => cache.Clear();

        public override void PostUpdateEverything()
        {
            if (Main.gameMenu || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Main.GameUpdateCount % (uint)Math.Max(1, DialogueExitCheckTicks) != 0)
                return;

            CheckExits();
        }

        // Server / single-player: retires every entry whose Exit is true.
        static void CheckExits()
        {
            foreach (ImportantDialogueEntry entry in ImportantDialogueRegistry.All)
            {
                if (entry.Exit == null)
                    continue;

                string doneKey = StoryFlags.DoneKey(entry.Id);
                if (StorySystem.Get(doneKey))
                    continue;

                bool exit;
                try
                {
                    exit = entry.Exit();
                }
                catch (Exception e)
                {
                    ReportOnce(entry.Id + ".Exit", e);
                    continue;
                }

                if (exit)
                    StorySystem.ServerSet(doneKey, true);
            }
        }

        /// <summary>
        /// The active entry of an NPC type for this player: the highest Priority entry whose Condition is true and that is not done. Null = none.
        /// fresh = false may return a result up to ConditionCacheTicks old (fine for drawing); fresh = true always evaluates (use it when talking).
        /// </summary>
        public static ImportantDialogueEntry FindActive(Player player, int npcType, bool fresh)
        {
            IReadOnlyList<ImportantDialogueEntry> entries = ImportantDialogueRegistry.ForNpc(npcType);
            if (entries.Count == 0 || player == null)
                return null;

            if (!fresh && cache.TryGetValue(npcType, out CachedActive cached)
                && cached.Version == StorySystem.Version
                && Main.GameUpdateCount - cached.Tick < (uint)Math.Max(0, ConditionCacheTicks))
                return cached.Entry;

            ImportantDialogueEntry found = null;
            foreach (ImportantDialogueEntry entry in entries)
            {
                if (entry.IsDone)
                    continue;

                bool ok;
                try
                {
                    ok = entry.Condition(player);
                }
                catch (Exception e)
                {
                    ReportOnce(entry.Id + ".Condition", e);
                    ok = false;
                }

                if (ok)
                {
                    found = entry;
                    break;
                }
            }

            cache[npcType] = new CachedActive { Tick = Main.GameUpdateCount, Version = StorySystem.Version, Entry = found };
            return found;
        }

        /// <summary>Local client: the player just started talking to this NPC. Runs OnTalkStart of the active entry, once.</summary>
        public static void RunTalkStart(Player player, NPC npc)
        {
            ImportantDialogueEntry entry = FindActive(player, npc.type, true);
            if (entry?.OnTalkStart == null)
                return;

            try
            {
                entry.OnTalkStart(player, npc);
            }
            catch (Exception e)
            {
                ReportOnce(entry.Id + ".OnTalkStart", e);
            }
        }

        /// <summary>Draws the "!" above the NPC's head. Uses Exclamation.png when it exists, otherwise a yellow bordered "!".</summary>
        public static void DrawMark(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos)
        {
            if (!markTextureTried)
            {
                markTextureTried = true;
                markTexture = null;
                string used = null;
                if (ModContent.RequestIfExists<Texture2D>(ExclamationTexturePath, out markTexture))
                    used = ExclamationTexturePath;
            }

            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * ExclamationBobPixels;
            Vector2 pos = new Vector2(npc.Center.X - screenPos.X, npc.position.Y + npc.gfxOffY - screenPos.Y - ExclamationOffsetY + bob);

            if (markTexture != null)
            {
                Texture2D texture = markTexture.Value;
                spriteBatch.Draw(texture, pos, null, Color.White, 0f, new Vector2(texture.Width / 2f, texture.Height), 1f, SpriteEffects.None, 0f);
                return;
            }

            Utils.DrawBorderStringBig(spriteBatch, "!", pos, Color.Yellow, 0.7f, 0.5f, 1f);
        }

        /// <summary>A broken Condition / Exit / callback in the definitions file must not crash the game or flood the log: report each place once.</summary>
        public static void ReportOnce(string where, Exception e)
        {
            if (reportedErrors.Add(where))
                FactorrariaNet.Log("[Story] important dialogue " + where + " threw: " + e);
        }
    }
}