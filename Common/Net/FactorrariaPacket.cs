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
        KnowledgeDelta = 10,      // server -> client: "recipe <catalog key> is now unlocked / crafted" (patch 3.2)
        SeenDelta = 11,           // server -> client: "these ingredient item types are now seen" (patch 3.2)
        SeenReport = 12,          // client -> server: "I have held these ingredient item types" (batched, patch 3.2)
        ReadParchmentRequest = 13, // client -> server: "I read a parchment of pool <name>" (patch 3.2)
        ReadParchmentResult = 14, // server -> client: answer to ReadParchmentRequest (patch 3.2)
        ChestConveyorChanged = 15, // server -> client: "the chest conveyor entry at (x, y) is now present / absent with mode <m>" (patch 3.4a)
        ChestConveyorModeRequest = 16, // client -> server: "cycle the conveyor mode of the chest at (x, y)" (patch 3.4a)
        AwakenedStatuesChanged = 17,   // server -> client: "these statue origins are no longer awakened, these now are" (patch 3.4b)
        MachineBiomeReport = 18,       // client -> server: "the machine I just placed at (x, y) is in biome <b>" (patch 4.3)
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