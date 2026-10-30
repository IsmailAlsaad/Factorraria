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
    ///    a Dryad is spawned as a homeless town NPC at the Eye's death position and the vanilla arrival line is announced.
    ///  - SERVER / single-player only. Clients see the Dryad through the normal NPC sync and the flags through the story flag sync. No packet.
    /// </summary>
    public static class DryadStory
    {
        // Tunables (not const, so hot reload can change them).
        public static bool RequireHouseForDryadExit = true;   // D8: dialogue 1 ends only when the rescued Mechanic also has a house; false = the rescue alone ends it
        public static int SpawnSearchRadiusTiles = 25;        // how far from the Eye's death spot (in tiles) a free standing place is looked for
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
            int radius = System.Math.Max(1, SpawnSearchRadiusTiles);
            int x, y;

            // The Eye flies through blocks, so its death spot can be inside the ground: use the nearest free standing place instead, then the world spawn.
            if (!FindStandingSpot(center, radius, out x, out y)
                && !FindStandingSpot(new Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f), radius, out x, out y))
            {
                x = Main.spawnTileX;
                y = Main.spawnTileY;
            }

            // NewNPC puts the bottom centre of the NPC at (X, Y): the floor line of the foot tile.
            int index = NPC.NewNPC(new EntitySource_Misc("FactorrariaStoryDryad"), x * 16 + 8, (y + 1) * 16, NPCID.Dryad);
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

            FactorrariaNet.Log("[Story] the Dryad arrived at tile (" + x + ", " + y + ") after the first Eye of Cthulhu kill");
            return true;
        }

        // The free standing place closest to a world position: solid ground below, room for the Dryad's box, no liquid.
        static bool FindStandingSpot(Vector2 near, int radius, out int footX, out int footY)
        {
            footX = 0;
            footY = 0;

            int cx = (int)(near.X / 16f);
            int cy = (int)(near.Y / 16f);
            float best = float.MaxValue;
            bool found = false;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    float distance = dx * dx + dy * dy;
                    if (distance >= best)
                        continue;

                    int x = cx + dx;
                    int y = cy + dy;
                    if (!WorldGen.InWorld(x, y, 10))
                        continue;

                    if (!WorldGen.SolidTile(x, y + 1) || Main.tile[x, y].LiquidAmount > 0)
                        continue;

                    Vector2 position = new Vector2(x * 16 + 8 - DryadWidth / 2f, (y + 1) * 16 - DryadHeight);
                    if (Collision.SolidCollision(position, DryadWidth, DryadHeight))
                        continue;

                    best = distance;
                    footX = x;
                    footY = y;
                    found = true;
                }
            }

            return found;
        }

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