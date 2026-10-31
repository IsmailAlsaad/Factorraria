using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Test tool for the per-player "seen" state of the important dialogues (patch N10.2). TEMPORARY: removed in patch N9 together with /fstory.
    /// A Chat command: it runs on the client that types it, because "seen" lives in that character (the /fstory commands run on the server).
    ///   /fseen                   list the entries this character has seen in this world
    ///   /fseen clear [id]        forget one entry, or all of them in this world (the "!" comes back)
    ///   /fseen add &lt;id&gt;        mark an entry as seen
    /// </summary>
    public class DialogueSeenCommand : ModCommand
    {
        public override string Command => "fseen";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/fseen | clear [id] | add <id>";
        public override string Description => "Important dialogue seen-state of this character (debug)";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            Player player = caller.Player;
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "list";

            if (sub == "list")
            {
                List<string> ids = ImportantDialoguePlayer.SeenIds(player);
                caller.Reply("[Story] seen in this world: " + (ids.Count == 0 ? "nothing" : string.Join(", ", ids)), Color.LightGreen);
                List<string> all = new List<string>();
                foreach (ImportantDialogueEntry entry in ImportantDialogueRegistry.All)
                    all.Add(entry.Id);
                caller.Reply("All entries: " + string.Join(", ", all), Color.LightGreen);
                return;
            }

            if (sub == "clear")
            {
                string id = args.Length > 1 ? Resolve(args[1]) : null;
                if (args.Length > 1 && id == null)
                {
                    caller.Reply("Unknown entry id: " + args[1], Color.Yellow);
                    return;
                }

                int count = ImportantDialoguePlayer.ForgetSeen(player, id);
                caller.Reply("[Story] forgot " + count + " seen entr" + (count == 1 ? "y" : "ies") + ".", Color.LightGreen);
                return;
            }

            if (sub == "add" && args.Length > 1)
            {
                string id = Resolve(args[1]);
                if (id == null)
                {
                    caller.Reply("Unknown entry id: " + args[1], Color.Yellow);
                    return;
                }

                ImportantDialoguePlayer.MarkSeen(player, id);
                caller.Reply("[Story] marked " + id + " as seen.", Color.LightGreen);
                return;
            }

            caller.Reply("Usage: " + Usage, Color.Yellow);
        }

        static string Resolve(string typed)
        {
            foreach (ImportantDialogueEntry entry in ImportantDialogueRegistry.All)
                if (string.Equals(entry.Id, typed, StringComparison.OrdinalIgnoreCase))
                    return entry.Id;

            return null;
        }
    }
}