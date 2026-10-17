using Factorraria.Content.Items.Mounts;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // The mech suit's on/off flag, mirrored to everyone (patch 2.1). The owner's client is the only place the suit is driven; the server and the other
    // clients keep a copy of the flag only so MechSuitArmProjectile (which runs on every machine) does not kill the arms of a player who is in the suit.
    //  - owner turns the suit on or off -> MechSuitStateRequest to the server
    //  - server stores it on that player's MechSuitPlayer and broadcasts MechSuitState to everyone else
    //  - a client joining later is told by MechSuitPlayer.SyncPlayer
    // Single-player short-circuits: the request runs the handler directly and the broadcast does nothing.
    public class MechSuitNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.MechSuitStateRequest, PacketDirection.ToServer, MechSuitNet.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.MechSuitState, PacketDirection.ToClient, MechSuitNet.HandleState);
        }
    }

    public static class MechSuitNet
    {
        /// <summary>Owner side: tell the server the suit is now on or off.</summary>
        public static void ReportState(bool active)
        {
            FactorrariaNet.SendRequest(FactorrariaPacket.MechSuitStateRequest, writer => writer.Write(active));
        }

        /// <summary>Server side: tell one client (or everyone, toClient = -1) the current flag of one player.</summary>
        public static void SendState(int player, bool active, int toClient = -1, int ignoreClient = -1)
        {
            void Write(BinaryWriter writer)
            {
                writer.Write((byte)player);
                writer.Write(active);
            }

            if (toClient >= 0)
                FactorrariaNet.SendToClient(toClient, FactorrariaPacket.MechSuitState, Write);
            else
                FactorrariaNet.Broadcast(FactorrariaPacket.MechSuitState, Write, ignoreClient);
        }

        // Server (or single-player, where sender is the local player).
        public static void HandleRequest(BinaryReader reader, int sender)
        {
            bool active = reader.ReadBoolean();

            if (sender < 0 || sender >= Main.maxPlayers || !Main.player[sender].active)
                return;

            Main.player[sender].GetModPlayer<MechSuitPlayer>().SetReportedState(active);
            SendState(sender, active, -1, sender);
        }

        // Client.
        public static void HandleState(BinaryReader reader, int sender)
        {
            int player = reader.ReadByte();
            bool active = reader.ReadBoolean();

            if (player >= Main.maxPlayers)
                return;

            Main.player[player].GetModPlayer<MechSuitPlayer>().SetReportedState(active);
        }
    }
}