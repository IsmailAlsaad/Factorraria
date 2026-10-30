using Factorraria.Common.Net;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// The world's NPC story state: a set of named flags (see StoryFlags). NPC roadmap patch N1.1.
    ///  - AUTHORITY: the server (or single-player) writes flags through ServerSet. A multiplayer client only reads them.
    ///  - SYNC: the join provider "story-flags" sends every flag to a joining client, StoryFlagChanged broadcasts each later change
    ///    (Common/Net/StoryNet.cs). Single-player applies the change directly, there is no packet.
    ///  - SAVE: world data key "Flags" (a list of the flags that are set).
    /// Version goes up on every change, so a cached condition can tell it is stale.
    /// </summary>
    public class StorySystem : ModSystem
    {
        // Tunable (not const, so hot reload can change it).
        public static int MaxKeyLength = 64;

        static readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Goes up on every change of the flag set (server and client).</summary>
        public static int Version { get; private set; }

        public static IReadOnlyCollection<string> Flags => flags;

        public static bool Get(string key) => key != null && flags.Contains(key);

        /// <summary>
        /// Server or single-player: sets or clears a flag and tells every client. Returns true if the flag changed.
        /// A multiplayer client does nothing (it learns the change from the server).
        /// </summary>
        public static bool ServerSet(string key, bool value)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return false;

            if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
                return false;

            bool changed = value ? flags.Add(key) : flags.Remove(key);
            if (!changed)
                return false;

            Version++;
            FactorrariaNet.Log("[Story] flag " + key + " = " + (value ? "on" : "off"));
            StoryNet.BroadcastFlag(key, value);
            return true;
        }

        /// <summary>Client: applies one change the server sent.</summary>
        public static void ApplyFromServer(string key, bool value)
        {
            if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
                return;

            bool changed = value ? flags.Add(key) : flags.Remove(key);
            if (changed)
                Version++;
        }

        /// <summary>Client: replaces the whole set (the join sync).</summary>
        public static void ReplaceAll(IEnumerable<string> newFlags)
        {
            flags.Clear();
            foreach (string key in newFlags)
                if (!string.IsNullOrEmpty(key) && key.Length <= MaxKeyLength)
                    flags.Add(key);

            Version++;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            List<string> list = new List<string>(flags);
            list.Sort(StringComparer.Ordinal);
            tag["Flags"] = list;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            flags.Clear();
            Version++;

            if (!tag.ContainsKey("Flags"))
                return;

            foreach (string key in tag.Get<List<string>>("Flags"))
                if (!string.IsNullOrEmpty(key) && key.Length <= MaxKeyLength)
                    flags.Add(key);
        }

        public override void OnWorldUnload()
        {
            flags.Clear();
            Version++;
        }

        public override void Unload()
        {
            flags.Clear();
        }
    }
}