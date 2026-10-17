using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Multiplayer test command: /fjoin asks the server for the full state again and prints the summary when it has arrived.
    // A Chat command runs on the client that typed it.
    public class NetJoinCommand : ModCommand
    {
        public override string Command => "fjoin";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "Multiplayer test: asks the server for the full synced state again.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (FactorrariaNet.IsSinglePlayer)
            {
                caller.Reply("fjoin: single-player, the state is already local (nothing to sync)", Color.LightGreen);
                return;
            }

            JoinSync.AnnounceInChat = true;
            JoinSync.RequestFullState();
            caller.Reply("fjoin: requested the full state from the server...", Color.LightGreen);
        }
    }
}