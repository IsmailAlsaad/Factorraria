using Factorraria.Common.Net;
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
    /// </summary>
    public class StoryCommand : ModCommand
    {
        public override string Command => "fstory";
        public override CommandType Type => CommandType.World;
        public override string Usage => "/fstory status | known | flag <name> [on|off] | dialogue";
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

        static void SetFlag(CommandCaller caller, string[] args)
        {
            if (args.Length < 2)
            {
                caller.Reply("Usage: /fstory flag <name> [on|off]", Color.Yellow);
                return;
            }

            string key = args[1];
            if (!StoryFlags.IsKnown(key))
            {
                caller.Reply("Unknown flag '" + key + "'. Try /fstory known.", Color.Yellow);
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