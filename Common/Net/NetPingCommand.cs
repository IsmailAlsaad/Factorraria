using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Multiplayer test command: /fping sends a request to the server and prints the round trip time. A Chat command runs on the
    // client that typed it, which is what the test needs.
    public class NetPingCommand : ModCommand
    {
        public override string Command => "fping";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "Multiplayer test: pings the server and shows the round trip time.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            NetCoreSystem.SendPing(message => caller.Reply(message, Color.LightGreen));
        }
    }
}