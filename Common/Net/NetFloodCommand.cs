using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Multiplayer test command: /fflood [count] sends many pings in a single tick so the server's rate limiter has to drop some.
    // The pings use negative tokens, so the client ignores the replies. A Chat command runs on the client that typed it.
    public class NetFloodCommand : ModCommand
    {
        public override string Command => "fflood";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "Multiplayer test: floods the server with pings to trigger the rate limiter. Usage: /fflood [count]";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (FactorrariaNet.IsSinglePlayer)
            {
                caller.Reply("fflood: single-player, requests never go through the rate limiter", Color.LightGreen);
                return;
            }

            int count = 200;
            if (args.Length > 0 && int.TryParse(args[0], out int parsed))
                count = System.Math.Clamp(parsed, 1, 2000);

            for (int i = 0; i < count; i++)
            {
                int token = -(i + 1);
                FactorrariaNet.SendRequest(FactorrariaPacket.Ping, writer => writer.Write(token));
            }

            caller.Reply("fflood: sent " + count + " pings in one tick. The server log should show a rate-limit line; /fping should work again after about a second.", Color.LightGreen);
        }
    }
}