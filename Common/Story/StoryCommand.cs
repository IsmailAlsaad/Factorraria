using Factorraria.Common.Net;
using Factorraria.Content.Items.Story;
using Factorraria.Content.NPCs.HerBackpack;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Test tool for the NPC story flags. TEMPORARY: removed in patch N9 of docs/npcs/ROADMAP.md.
    /// A World command: in multiplayer it runs on the server, so it writes the real flags and every client gets the change.
    ///   /fstory status                  every set flag, the flag version, and which side this process is
    ///   /fstory known                   the fixed flag keys (and the "Done.&lt;id&gt;" form)
    ///   /fstory flag &lt;name&gt; [on|off]    set (default) or clear a flag
    ///   /fstory dialogue                every important dialogue: NPC, priority, condition, exit, done (patch N2.1)
    ///   /fstory spawn mechanic|backpack spawn the tied Mechanic or Her Backpack next to you (patch N4.1)
    ///   /fstory dump                    rare NPC state: flags, the spawn rule verdicts for you, every Mechanic / Backpack NPC (patch N4.1)
    ///   /fstory weight <name> <number>  change a rare spawn tunable while playing: mechanic, dungeon, backpack, rarity (patch N4.1)
    ///   /fstory bag                     put one Mechanic's Bag into your inventory, for testing the bag-loss watcher and the turn-in (patch N4.2)
    ///   /fstory eye                     run the first-Eye-of-Cthulhu handler at your position without fighting it: sets EyeKillSeen and spawns the Dryad if she never arrived (patch N6.1). To repeat: /fstory flag EyeKillSeen off and /fstory flag DryadSeen off
    /// </summary>
    public class StoryCommand : ModCommand
    {
        public override string Command => "fstory";
        public override CommandType Type => CommandType.World;
        public override string Usage => "/fstory status | known | flag <name> [on|off] | dialogue | spawn <mechanic|backpack> | dump | bag | eye | weight <name> <number>";
        public override string Description => "NPC story debug tools";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

            switch (sub)
            {
                case "status":
                    Status(caller);
                    break;

                case "known":
                    caller.Reply("Fixed keys: " + string.Join(", ", StoryFlags.All()) + ". Dialogue completion keys look like \"" + StoryFlags.DoneKey("<id>") + "\".", Color.LightGreen);
                    break;

                case "flag":
                    SetFlag(caller, args);
                    break;

                case "dialogue":
                    Dialogue(caller);
                    break;

                case "spawn":
                    Spawn(caller, args);
                    break;

                case "dump":
                    Dump(caller);
                    break;

                case "bag":
                    GiveBag(caller);
                    break;

                case "eye":
                    Eye(caller);
                    break;

                case "weight":
                    Weight(caller, args);
                    break;

                default:
                    caller.Reply("Usage: " + Usage, Color.Yellow);
                    break;
            }
        }

        static void Status(CommandCaller caller)
        {
            List<string> sorted = new List<string>(StorySystem.Flags);
            sorted.Sort(StringComparer.Ordinal);

            string side = Main.netMode == NetmodeID.Server ? "dedicated server" : Main.netMode == NetmodeID.MultiplayerClient ? "multiplayer client" : "single-player";
            caller.Reply("[Story] " + side + ", version " + StorySystem.Version + ", " + sorted.Count + " flag(s) set" + (sorted.Count > 0 ? ": " + string.Join(", ", sorted) : "."), Color.LightGreen);
        }

        // Lists every important dialogue. In multiplayer this runs on the server, so Condition is evaluated for the calling player's server-side copy.
        static void Dialogue(CommandCaller caller)
        {
            if (ImportantDialogueRegistry.All.Count == 0)
            {
                caller.Reply("[Story] no important dialogues are registered.", Color.LightGreen);
                return;
            }

            foreach (ImportantDialogueEntry entry in ImportantDialogueRegistry.All)
            {
                string condition = "?";
                if (caller.Player != null)
                {
                    try
                    {
                        condition = entry.Condition(caller.Player) ? "true" : "false";
                    }
                    catch (Exception)
                    {
                        condition = "error";
                    }
                }

                string exit = "none";
                if (entry.Exit != null)
                {
                    try
                    {
                        exit = entry.Exit() ? "true" : "false";
                    }
                    catch (Exception)
                    {
                        exit = "error";
                    }
                }

                caller.Reply(entry.Id + " | " + Lang.GetNPCNameValue(entry.NpcType) + " | priority " + entry.Priority + " | condition " + condition + " | exit " + exit + " | done " + (entry.IsDone ? "true" : "false"), Color.LightGreen);
            }
        }

        // Debug spawn (patch N4.1). A World command, so in multiplayer it runs on the server and the vanilla NPC sync shows the NPC to everyone.
        static void Spawn(CommandCaller caller, string[] args)
        {
            string what = args.Length > 1 ? args[1].ToLowerInvariant() : "";
            int type;
            if (what == "mechanic")
                type = NPCID.BoundMechanic;
            else if (what == "backpack")
                type = ModContent.NPCType<HerBackpackNPC>();
            else
            {
                caller.Reply("Usage: /fstory spawn mechanic|backpack", Color.Yellow);
                return;
            }

            Player player = caller.Player;
            if (player == null)
            {
                caller.Reply("There is no player to spawn next to.", Color.Yellow);
                return;
            }

            int x = (int)player.Center.X + player.direction * 96;
            int y = (int)player.Bottom.Y;
            int index = NPC.NewNPC(new Terraria.DataStructures.EntitySource_Misc("fstory spawn"), x, y, type);
            if (index < 0 || index >= Main.maxNPCs)
            {
                caller.Reply("No free NPC slot.", Color.Yellow);
                return;
            }

            caller.Reply("Spawned " + Main.npc[index].GivenOrTypeName + " as NPC #" + index + " at tile " + (int)(Main.npc[index].Center.X / 16f) + ", " + (int)(Main.npc[index].Center.Y / 16f) + ".", Color.LightGreen);
        }

        // Everything needed to judge the rare spawns in one place (patch N4.1). The verdicts use the calling player's zones as the server sees them.
        static void Dump(CommandCaller caller)
        {
            caller.Reply("[Story] NPC.savedMech " + NPC.savedMech + ", Eye defeated " + NPC.downedBoss1 + ", Skeletron defeated " + NPC.downedBoss3 + ", MechanicRescued " + OnOff(StoryFlags.MechanicRescued) + ", MechanicShopUnlocked " + OnOff(StoryFlags.MechanicShopUnlocked)
                + ", BackpackTaken " + OnOff(StoryFlags.BackpackTaken) + ", DungeonFork " + OnOff(StoryFlags.DungeonFork), Color.LightGreen);

            string reason;
            float weight = RareNpcRules.MechanicWeight(caller.Player, out reason);
            caller.Reply("Tied Mechanic spawn weight for you now: " + weight + (weight <= 0f ? " (" + reason + ")" : ""), Color.LightGreen);

            weight = RareNpcRules.BackpackWeight(caller.Player, out reason);
            caller.Reply("Her Backpack spawn weight for you now: " + weight + (weight <= 0f ? " (" + reason + ")" : ""), Color.LightGreen);

            caller.Reply("Tunables: mechanic " + RareNpcRules.MechanicSpawnWeight + ", dungeon " + RareNpcRules.DungeonMechanicSpawnWeight + ", backpack " + RareNpcRules.BackpackSpawnWeight + ", rarity " + RareNpcRules.RareNpcRarity, Color.LightGreen);

            int backpackType = ModContent.NPCType<HerBackpackNPC>();
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || (npc.type != NPCID.BoundMechanic && npc.type != NPCID.Mechanic && npc.type != backpackType))
                    continue;

                count++;
                caller.Reply("NPC #" + i + " " + npc.GivenOrTypeName + " (type " + npc.type + ") at tile " + (int)(npc.Center.X / 16f) + ", " + (int)(npc.Center.Y / 16f)
                    + " | rarity " + npc.rarity + " | friendly " + npc.friendly + " | townNPC " + npc.townNPC + " | homeless " + npc.homeless + " | aiStyle " + npc.aiStyle, Color.LightGreen);
            }

            if (count == 0)
                caller.Reply("No Mechanic or Her Backpack NPC exists right now.", Color.LightGreen);
        }

        // Test helper (patch N6.1): the first Eye of Cthulhu kill without the fight. A World command, so in multiplayer it runs on the server.
        static void Eye(CommandCaller caller)
        {
            Vector2 where = caller.Player != null ? caller.Player.Center : new Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f);
            caller.Reply("[Story] " + DryadStory.OnEyeKilled(where) + ".", Color.LightGreen);
        }

        // Test helper (patch N4.2): hands the caller one Bag without touching any flag.
        static void GiveBag(CommandCaller caller)
        {
            Player player = caller.Player;
            if (player == null)
            {
                caller.Reply("There is no player to give the Bag to.", Color.Yellow);
                return;
            }

            player.QuickSpawnItem(player.GetSource_Misc("fstory bag"), ModContent.ItemType<MechanicsBagItem>(), 1);
            caller.Reply("Gave you one Mechanic's Bag.", Color.LightGreen);
        }

        static string OnOff(string flag) => StorySystem.Get(flag) ? "on" : "off";

        // Changes a rare spawn tunable while playing (patch N4.1). Runs on the server in multiplayer, which is where the rules are used.
        static void Weight(CommandCaller caller, string[] args)
        {
            if (args.Length < 3 || !float.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
            {
                caller.Reply("Usage: /fstory weight <mechanic|dungeon|backpack|rarity> <number>", Color.Yellow);
                return;
            }

            switch (args[1].ToLowerInvariant())
            {
                case "mechanic":
                    RareNpcRules.MechanicSpawnWeight = value;
                    break;

                case "dungeon":
                    RareNpcRules.DungeonMechanicSpawnWeight = value;
                    break;

                case "backpack":
                    RareNpcRules.BackpackSpawnWeight = value;
                    break;

                case "rarity":
                    RareNpcRules.RareNpcRarity = (int)value;
                    break;

                default:
                    caller.Reply("Usage: /fstory weight <mechanic|dungeon|backpack|rarity> <number>", Color.Yellow);
                    return;
            }

            caller.Reply("Set " + args[1].ToLowerInvariant() + " to " + value + ". (Rarity only applies to NPCs spawned afterwards.)", Color.LightGreen);
        }

        static void SetFlag(CommandCaller caller, string[] args)
        {
            if (args.Length < 2)
            {
                caller.Reply("Usage: /fstory flag <name> [on|off]", Color.Yellow);
                return;
            }

            string typed = args[1];
            string key = StoryFlags.Resolve(typed);
            if (key == null)
            {
                // Show the character codes so an invisible character in the typed name is visible.
                List<string> codes = new List<string>();
                foreach (char c in typed)
                    codes.Add(((int)c).ToString());
                caller.Reply("Unknown flag '" + typed + "' (" + typed.Length + " chars: " + string.Join(" ", codes) + "). Try /fstory known.", Color.Yellow);
                return;
            }

            bool value = true;
            if (args.Length > 2)
            {
                string word = args[2].ToLowerInvariant();
                if (word == "off" || word == "0" || word == "false")
                    value = false;
                else if (word != "on" && word != "1" && word != "true")
                {
                    caller.Reply("The last word must be on or off.", Color.Yellow);
                    return;
                }
            }

            bool changed = StorySystem.ServerSet(key, value);
            caller.Reply(changed ? "Flag " + key + " is now " + (value ? "on" : "off") + "." : "Flag " + key + " was already " + (value ? "on" : "off") + ".", Color.LightGreen);
        }
    }
}