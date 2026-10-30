using Factorraria.Common.Story;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // NPC story flags (NPC roadmap patch N1.1). The server owns the flags (StorySystem.ServerSet). Two ways a client learns them:
    //  - join: provider "story-flags" sends the complete set, the client replaces its copy
    //  - change: StoryFlagChanged (server -> every client) carries one key and its new value
    // Single-player has no packets: ServerSet already changed the only copy there is.
    public class StoryNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.StoryFlagChanged, PacketDirection.ToClient, StoryNet.HandleFlagChanged);
            JoinSync.Register("story-flags", StoryNet.WriteAll, StoryNet.ReadAll);
        }
    }

    public static class StoryNet
    {
        // Tunable (not const, so hot reload can change it).
        public static int MaxJoinFlags = 4096;

        /// <summary>Server only (no-op elsewhere): tells every client that one flag changed.</summary>
        public static void BroadcastFlag(string key, bool value)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            FactorrariaNet.Broadcast(FactorrariaPacket.StoryFlagChanged, writer =>
            {
                writer.Write(key);
                writer.Write(value);
            });
        }

        // Client.
        public static void HandleFlagChanged(BinaryReader reader, int sender)
        {
            string key = reader.ReadString();
            bool value = reader.ReadBoolean();
            StorySystem.ApplyFromServer(key, value);
        }

        // Join provider, server side: the complete set.
        public static void WriteAll(BinaryWriter writer)
        {
            writer.Write(StorySystem.Flags.Count);
            foreach (string key in StorySystem.Flags)
                writer.Write(key);
        }

        // Join provider, client side: replaces the local set.
        public static void ReadAll(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxJoinFlags)
                count = 0;

            List<string> keys = new List<string>(count);
            for (int i = 0; i < count; i++)
                keys.Add(reader.ReadString());

            StorySystem.ReplaceAll(keys);
        }
    }
}