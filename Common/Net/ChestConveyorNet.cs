using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Chest conveyor replica (roadmap patch 3.4a, finding U5, first half of 3.4). The server owns ChestConveyorSystem's registry
    // (it probes the chests and runs them). A client only needs to know WHICH chests are registered and their mode, because the
    // mode button under the chest grid (ChestConveyorModeUI) shows only for registered chests.
    //  - join: provider "chest-conveyors" writes every entry (x, y, mode); the client replaces its replica
    //  - the server broadcasts ChestConveyorChanged(x, y, present, mode) whenever an entry is added, removed or its mode changes
    //  - the button sends ChestConveyorModeRequest(x, y); the server checks the player, the reach and the entry, cycles the mode and
    //    broadcasts to EVERYONE including the sender (no prediction: the button changes as soon as the answer arrives)
    // Single-player: SendRequest runs the handler directly and there is nothing to broadcast.
    public class ChestConveyorNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.ChestConveyorChanged, PacketDirection.ToClient, ChestConveyorNet.HandleChanged);
            FactorrariaNet.Register(FactorrariaPacket.ChestConveyorModeRequest, PacketDirection.ToServer, ChestConveyorNet.HandleCycleRequest);
            JoinSync.Register("chest-conveyors", ChestConveyorNet.WriteAll, ChestConveyorNet.ReadAll);
        }
    }

    public static class ChestConveyorNet
    {
        public static int MaxJoinEntries = 100000;

        /// <summary>Server only (no-op elsewhere): tells every client that an entry now exists with this mode, or is gone.</summary>
        public static void BroadcastEntry(Point chest, bool present, ChestConveyorMode mode)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            FactorrariaNet.Log("[ChestConveyor] entry " + (present ? "added or changed (" + mode + ")" : "removed") + " at (" + chest.X + ", " + chest.Y + ")");

            FactorrariaNet.Broadcast(FactorrariaPacket.ChestConveyorChanged, writer =>
            {
                writer.Write((short)chest.X);
                writer.Write((short)chest.Y);
                writer.Write(present);
                writer.Write((byte)mode);
            });
        }

        /// <summary>Owner side: the mode button was clicked on the chest whose top-left tile is this position.</summary>
        public static void RequestCycle(Point chest)
        {
            FactorrariaNet.SendRequest(FactorrariaPacket.ChestConveyorModeRequest, writer =>
            {
                writer.Write((short)chest.X);
                writer.Write((short)chest.Y);
            });
        }

        // Server (or single-player, where sender is the local player).
        public static void HandleCycleRequest(BinaryReader reader, int sender)
        {
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();

            if (sender < 0 || sender >= Main.maxPlayers)
                return;

            Player player = Main.player[sender];
            if (!player.active || player.dead || !WorldGen.InWorld(x, y, 1))
                return;

            // In single-player the chest is open on screen, so the reach check adds nothing.
            if (Main.netMode != NetmodeID.SinglePlayer && !FactorrariaNet.InReach(player, x, y))
            {
                FactorrariaNet.Log("[ChestConveyor] mode request from " + player.name + " rejected: (" + x + ", " + y + ") is out of reach");
                return;
            }

            var chest = new Point(x, y);
            if (!ChestConveyorSystem.TryCycleModeAt(chest, out ChestConveyorMode mode))
                return;

            BroadcastEntry(chest, true, mode);
        }

        // Client.
        public static void HandleChanged(BinaryReader reader, int sender)
        {
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();
            bool present = reader.ReadBoolean();
            int mode = reader.ReadByte();

            if (!WorldGen.InWorld(x, y, 1) || mode > 2)
                return;

            ChestConveyorSystem.SetReplicaEntry(new Point(x, y), present, (ChestConveyorMode)mode);
        }

        // ---------------------------------------------------------------- join provider ("chest-conveyors")

        public static void WriteAll(BinaryWriter writer)
        {
            var entries = new List<ChestConveyorEntry>(ChestConveyorSystem.Entries);
            writer.Write(entries.Count);

            foreach (ChestConveyorEntry entry in entries)
            {
                writer.Write((short)entry.TopLeft.X);
                writer.Write((short)entry.TopLeft.Y);
                writer.Write((byte)entry.Mode);
            }

            FactorrariaNet.Log("join provider chest-conveyors: sent " + entries.Count + " entries");
        }

        public static void ReadAll(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxJoinEntries)
                throw new InvalidDataException("chest conveyor count " + count);

            var all = new List<KeyValuePair<Point, ChestConveyorMode>>(count);
            for (int i = 0; i < count; i++)
            {
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                int mode = reader.ReadByte();
                all.Add(new KeyValuePair<Point, ChestConveyorMode>(new Point(x, y), mode > 2 ? ChestConveyorMode.InputOutput : (ChestConveyorMode)mode));
            }

            ChestConveyorSystem.ReplaceReplica(all);
            FactorrariaNet.Log("join provider chest-conveyors: " + count + " entries");
        }
    }
}