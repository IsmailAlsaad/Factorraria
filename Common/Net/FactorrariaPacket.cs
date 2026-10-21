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
        MachineLightUpdate = 19,       // server -> client: "these machines near you changed: flags, progress, crafts, use, recipe, fuel, tank levels" (patch 5.2)
        MachineSubscribeRequest = 20,  // client -> server: "I opened / closed the UI of the machine at (x, y)" (patch 5.3)
        MachineDetail = 21,            // server -> client: "here is the full, exact state of the machine at (x, y) you have open" (patch 5.3)
        MachineDetailClosed = 22,      // server -> client: "your subscription to the machine at (x, y) ended (killed, out of reach, dead, full, rejected): close its UI" (patch 5.3)
        MachineSlotEditRequest = 23,   // client -> server: "slot <kind> <index> of the machine at (x, y) held <old item>, make it <new item>" (patch 6.2)
        MachineSlotEditResult = 24,    // server -> client: "edit <seq> was accepted / rejected; on a reject the slot really holds <item>" (patch 6.2)
        MachineSelectRecipeRequest = 25, // client -> server: "on the machine at (x, y) I picked the recipe group with this catalog key (empty = auto)" (patch 6.3)
        MachineSelectRecipeResult = 26,  // server -> client: "pick <seq> was accepted / rejected; the machine's real pick is this key" (patch 6.3)
        MaterializerToggleRequest = 27,  // client -> server: "switch the starry background of the Materializer at (x, y) to on / off" (patch 6.4)
        MaterializerBackgroundChanged = 28, // server -> client(s): "the Materializer at (x, y) now has its background on / off (asked by player <n>)" (patch 6.4)
        VItemSubscribeRequest = 29,   // client -> server: "my join sync is done, send me the vItems near me from now on" (patch 7.2)
        VItemDelta = 30,              // server -> client: "these vItems appeared / changed / disappeared near you" (patch 7.2)
        ConveyorFilterRequest = 31,   // client -> server: "set / clear the filter of the priority conveyor network at (x, y) to item type <t>" (patch 7.3)
        PriorityFiltersChanged = 32,  // server -> client: "these are now all the filtered priority conveyor networks (filter + tiles)" (patch 7.3)
        GhostCheckRequest = 33,       // client -> server: "these world items and vItems are what I show near me, compare them with yours" (patch 7b.2, TEMPORARY test)
        ArmState = 34,                // server -> nearby clients: "the Robotic Arm at (x, y) now aims at tile (tx, ty) / has no target, starts a strike of <n> ticks, holds item type <t>" (patch 7b.4)
        ArmEffect = 35,               // server -> nearby clients: "play the block-placing sound at tile (x, y)" (patch 7b.4b; break and hit effects use vanilla message 17)
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