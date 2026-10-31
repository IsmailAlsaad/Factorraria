using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Story hooks on NPC death (NPC roadmap patch N6.1). Only the Eye of Cthulhu matters here (rule S4).
    ///  - AUTHORITY: GlobalNPC.OnKill runs on the server / single-player only (a multiplayer client returns at once). All state goes through StorySystem.ServerSet.
    /// </summary>
    public class StoryEventsGlobalNPC : GlobalNPC
    {
        public override void OnKill(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (npc.type == NPCID.EyeofCthulhu)
                DryadStory.OnEyeKilled(npc.Center);
        }
    }

    /// <summary>
    /// The Dryad's arrival (NPC roadmap rules S3 and S4, decisions D1 to D3).
    ///  - First Eye of Cthulhu kill (our own saved flag EyeKillSeen, not NPC.downedBoss1): if no Dryad exists and none ever did (DryadSeen),
    ///    a Dryad is spawned as a homeless town NPC exactly at the Eye's centre and the vanilla arrival line is announced.
    ///  - SERVER / single-player only. Clients see the Dryad through the normal NPC sync and the flags through the story flag sync. No packet.
    /// </summary>
    public static class DryadStory
    {
        // Tunables (not const, so hot reload can change them).
        public static bool RequireHouseForDryadExit = true;   // D8: dialogue 1 ends only when the rescued Mechanic also has a house; false = the rescue alone ends it
        public static bool SpawnOnFork = true;                // N8.1: true = with DungeonFork on (Skeletron-first route) the first Eye of Cthulhu kill still spawns the Dryad, because the Mechanic's scrolls need a Dryad (her dialogues stay off); false = no scripted Dryad on this route
        public static int DryadWidth = 18;
        public static int DryadHeight = 40;

        /// <summary>
        /// Server / single-player. Returns a one-line description of what happened (the /fstory eye command prints it).
        /// Only the FIRST kill does anything: later kills, and worlds where a Dryad already arrived, never spawn her again (D2).
        /// </summary>
        public static string OnEyeKilled(Vector2 center)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return "a multiplayer client does not run this";

            if (StorySystem.Get(StoryFlags.EyeKillSeen))
                return "the Eye of Cthulhu was already killed once (EyeKillSeen is on): nothing to do";

            StorySystem.ServerSet(StoryFlags.EyeKillSeen, true);

            // N8.1: on the Skeletron-first route (D11) the Dryad dialogues are off, but the Dryad herself still arrives (SpawnOnFork, default true),
            // because the Mechanic only hands out the scrolls while a Dryad exists (rule D20). SpawnOnFork = false stops the scripted arrival there.
            if (StorySystem.Get(StoryFlags.DungeonFork) && !SpawnOnFork)
            {
                FactorrariaNet.Log("[Story] Eye of Cthulhu first kill with DungeonFork on and SpawnOnFork false: no Dryad is spawned");
                return "EyeKillSeen set; DungeonFork is on and DryadStory.SpawnOnFork is false, so no Dryad is spawned";
            }

            if (StorySystem.Get(StoryFlags.DryadSeen) || NPC.AnyNPCs(NPCID.Dryad))
            {
                StorySystem.ServerSet(StoryFlags.DryadSeen, true);
                FactorrariaNet.Log("[Story] Eye of Cthulhu first kill: a Dryad exists or arrived before, none is spawned");
                return "EyeKillSeen set; a Dryad already exists or arrived before, so none is spawned";
            }

            if (!SpawnDryad(center))
                return "EyeKillSeen set, but the Dryad could not be spawned (no free NPC slot)";

            StorySystem.ServerSet(StoryFlags.DryadSeen, true);
            return "EyeKillSeen set and the Dryad was spawned";
        }

        static bool SpawnDryad(Vector2 center)
        {
            // The Dryad's centre is placed exactly on the Eye's centre (NewNPC takes the bottom centre of the NPC, so the Y is moved down by half her height).
            // The Eye flies through blocks, so if it died inside the ground she starts there and the vanilla NPC physics decide what happens next.
            int index = NPC.NewNPC(new EntitySource_Misc("FactorrariaStoryDryad"), (int)center.X, (int)(center.Y + DryadHeight / 2f), NPCID.Dryad);
            if (index < 0 || index >= Main.maxNPCs)
                return false;

            NPC dryad = Main.npc[index];

            // The same fields vanilla sets for an arriving town NPC: homeless, heading for the world spawn until the player gives her a house.
            dryad.homeless = true;
            dryad.homeTileX = Main.spawnTileX;
            dryad.homeTileY = Main.spawnTileY;
            dryad.direction = center.X < Main.spawnTileX * 16f ? 1 : -1;
            dryad.netUpdate = true;

            if (Main.netMode == NetmodeID.Server)
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasArrived", dryad.GetFullNetName()), new Color(50, 125, 255));
            else
                Main.NewText(Language.GetTextValue("Announcement.HasArrived", dryad.FullName), 50, 125, 255);

            FactorrariaNet.Log("[Story] the Dryad arrived at the Eye of Cthulhu's death position (tile " + (int)(center.X / 16f) + ", " + (int)(center.Y / 16f) + ")");
            return true;
        }

        /// <summary>
        /// True while any Dryad NPC exists. Since N8.2 it is no longer the gate for the scrolls (the flag DryadTalked is); kept as a helper. Works on every side: the server
        /// and single-player read their own NPC list, a client reads the vanilla-synced one.
        /// </summary>
        public static bool DryadExists() => NPC.AnyNPCs(NPCID.Dryad);

        /// <summary>True when a town Mechanic exists and has a house (used by the exit of Dryad dialogue 1, D8).</summary>
        public static bool MechanicHoused()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == NPCID.Mechanic && !npc.homeless)
                    return true;
            }

            return false;
        }
    }

    /// <summary>Rule S3: the first tick any Dryad exists, DryadSeen is latched, so a Dryad who dies later is never dropped again. Server / single-player only.</summary>
    public class StoryEventsSystem : ModSystem
    {
        public static int WatchEveryTicks = 20;

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Main.GameUpdateCount % (uint)System.Math.Max(1, WatchEveryTicks) != 0)
                return;

            if (!StorySystem.Get(StoryFlags.DryadSeen) && NPC.AnyNPCs(NPCID.Dryad))
                StorySystem.ServerSet(StoryFlags.DryadSeen, true);
        }
    }
}