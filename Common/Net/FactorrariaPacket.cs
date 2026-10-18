using System.IO;

namespace Factorraria.Common.Net
{
    // Every Factorraria packet type. Values are explicit and part of the wire format: append new ids at the end, never renumber
    // or reuse one (client and server must run the same mod version, but old ids in logs and notes should keep their meaning).
    public enum FactorrariaPacket : byte
    {
        Ping = 0,        // client -> server: round trip test (patch 1.1)
        PingReply = 1,   // server -> client: answer to Ping
        JoinRequest = 2, // client -> server: "send me the full state" (patch 1.2)
        JoinChunk = 3,   // server -> client: one chunk of one join provider's data (patch 1.2)
        JoinDone = 4,    // server -> client: every provider has been sent (patch 1.2)
        TilePlaced = 5,  // client -> server: "I placed tile <type> at (x, y)" (patch 1.3)
        MechSuitStateRequest = 6, // client -> server: "my mech suit is now on / off" (patch 2.1)
        MechSuitState = 7,        // server -> client: "player <index>'s mech suit is on / off" (patch 2.1)
        WireEditRequest = 8,      // client -> server: "add / remove wire <type> at (x, y)" (patch 3.1)
        WireChanged = 9,          // server -> client: "the wire flags at (x, y) are now <flags>" (patch 3.1)
    }

    // Which side a packet type is meant to be handled on. The router drops a packet that arrives on the wrong side.
    public enum PacketDirection : byte
    {
        ToServer,
        ToClient
    }

    // sender: for ToServer packets the player index of the sender, or -1 when server code called the request itself
    // (for example the Robotic Arm worker). For ToClient packets it is always -1.
    public delegate void PacketHandler(BinaryReader reader, int sender);
}