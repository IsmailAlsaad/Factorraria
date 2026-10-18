using Factorraria.Common.Statues;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Awakened statue replica (roadmap patch 3.4b, finding U5, second half of 3.4). The server owns AwakenedStatueSystem's set
    // (the awakening machines run there and RescanFor builds it). A client holds a replica of origin + statue entry per awakened
    // statue, which is all AwakenedStatueDrawSystem, the wire guard and IsAwakenedTile read.
    //  - join: provider "awakened-statues" writes every awakened statue (x, y, tile type, style); the client replaces its replica
    //  - the server broadcasts AwakenedStatuesChanged at the end of every RescanFor: the origins that are no longer awakened and
    //    the statues that are newly awakened (or are a different statue now), in one packet (chunked if there are very many)
    // Statues are identified by tile type + style, which AwakenedStatueRegistry resolves identically on both sides (both are
    // built from the same mod code, see the Phase 0 probe: 44 entries on every process).
    // Single-player: nothing is sent, the one process owns the set.
    public class AwakenedStatueNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.AwakenedStatuesChanged, PacketDirection.ToClient, AwakenedStatueNet.HandleChanged);
            JoinSync.Register("awakened-statues", AwakenedStatueNet.WriteAll, AwakenedStatueNet.ReadAll);
        }
    }

    public static class AwakenedStatueNet
    {
        public static int MaxJoinEntries = 100000;
        public static int MaxChangesPerPacket = 1000;   // 4 bytes per removal, 8 per addition: well under the packet limit

        /// <summary>Server only (no-op elsewhere): sends what one rescan changed. Chunked so a huge claim cannot overflow a packet.</summary>
        public static void BroadcastDelta(List<Point16> removed, List<AwakenedStatue> added)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            if (removed.Count == 0 && added.Count == 0)
                return;

            FactorrariaNet.Log("[AwakenedStatue] delta: removed " + removed.Count + ", added " + added.Count);

            int r = 0;
            int a = 0;
            while (r < removed.Count || a < added.Count)
            {
                int removedStart = r;
                int removedCount = Math.Min(MaxChangesPerPacket, removed.Count - r);
                int addedStart = a;
                int addedCount = Math.Min(MaxChangesPerPacket - removedCount, added.Count - a);

                FactorrariaNet.Broadcast(FactorrariaPacket.AwakenedStatuesChanged, writer =>
                {
                    writer.Write((ushort)removedCount);
                    for (int i = 0; i < removedCount; i++)
                    {
                        writer.Write((short)removed[removedStart + i].X);
                        writer.Write((short)removed[removedStart + i].Y);
                    }

                    writer.Write((ushort)addedCount);
                    for (int i = 0; i < addedCount; i++)
                        WriteStatue(writer, added[addedStart + i]);
                });

                r += removedCount;
                a += addedCount;
            }
        }

        static void WriteStatue(BinaryWriter writer, AwakenedStatue statue)
        {
            writer.Write((short)statue.Origin.X);
            writer.Write((short)statue.Origin.Y);
            writer.Write((ushort)statue.Entry.TileType);
            writer.Write((short)statue.Entry.Style);
        }

        // Reads one statue and returns false if it is unusable. The bytes are always consumed, so a bad entry cannot desync the stream.
        static bool ReadStatue(BinaryReader reader, out Point16 origin, out AwakenedStatueEntry entry)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            int tileType = reader.ReadUInt16();
            int style = reader.ReadInt16();

            origin = new Point16(x, y);
            entry = null;

            if (!WorldGen.InWorld(x, y, 1))
                return false;

            return ModContent.GetInstance<AwakenedStatueRegistry>().TryGet(tileType, style, out entry);
        }

        // Client.
        public static void HandleChanged(BinaryReader reader, int sender)
        {
            var system = ModContent.GetInstance<AwakenedStatueSystem>();

            int removedCount = reader.ReadUInt16();
            for (int i = 0; i < removedCount; i++)
            {
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                if (WorldGen.InWorld(x, y, 1))
                    system.SetReplicaStatue(new Point16(x, y), false, null);
            }

            int addedCount = reader.ReadUInt16();
            for (int i = 0; i < addedCount; i++)
            {
                if (ReadStatue(reader, out Point16 origin, out AwakenedStatueEntry entry))
                    system.SetReplicaStatue(origin, true, entry);
            }
        }

        // ---------------------------------------------------------------- join provider ("awakened-statues")

        public static void WriteAll(BinaryWriter writer)
        {
            var all = new List<AwakenedStatue>(ModContent.GetInstance<AwakenedStatueSystem>().Awakened.Values);
            writer.Write(all.Count);

            foreach (AwakenedStatue statue in all)
                WriteStatue(writer, statue);

            FactorrariaNet.Log("join provider awakened-statues: sent " + all.Count + " statues");
        }

        public static void ReadAll(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxJoinEntries)
                throw new InvalidDataException("awakened statue count " + count);

            var all = new List<KeyValuePair<Point16, AwakenedStatueEntry>>(count);
            for (int i = 0; i < count; i++)
            {
                if (ReadStatue(reader, out Point16 origin, out AwakenedStatueEntry entry))
                    all.Add(new KeyValuePair<Point16, AwakenedStatueEntry>(origin, entry));
            }

            ModContent.GetInstance<AwakenedStatueSystem>().ReplaceReplica(all);
            FactorrariaNet.Log("join provider awakened-statues: " + all.Count + " of " + count + " statues");
        }
    }
}