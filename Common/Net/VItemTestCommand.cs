using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Temporary test command for patch 7.1 (remove in Phase 10). A Chat command runs on the client that typed it; type "fvitem" in the
    // dedicated server console to read the server's numbers.
    //   /fvitem   shows this side's vItem list size, id table size, next id and the conversion / pickup counters
    public class VItemTestCommand : ModCommand
    {
        public override string Command => "fvitem";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fvitem";
        public override string Description => "Multiplayer test (7.1): vItem counts and counters of this side.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            caller.Reply(VirtualItemSystem.Describe(), Color.LightGreen);
        }
    }
}