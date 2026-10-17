using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Net
{
    // Join sync (roadmap patch 1.2, findings X3 and "missing player-level plumbing"). Clients start with empty registries because
    // world data loads on the server only, so every system that UI or draw code reads on a client registers a provider here:
    // the server writes the system's full state, the client reads it and replaces its local copy.
    //
    // Flow: client RequestFullState -> JoinRequest(token) -> the server queues a job for that player and, once per tick, serializes
    // one provider at a time and sends it in chunks (one chunk per tick, so a big state never floods the connection) -> JoinChunk
    // -> JoinDone. Providers are identified on the wire by their string key, never by list position (roadmap rule 5).
    public static class JoinSync
    {
        sealed class Provider
        {
            public string Key;
            public Action<BinaryWriter> Write;
            public Action<BinaryReader> Read;
        }

        // Server: one per player that asked for the full state.
        sealed class Job
        {
            public int Token;
            public int NextProvider;
            public byte[] Data;
            public int Offset;
            public int TotalBytes;
        }

        // Client: the chunks of one provider arriving.
        sealed class Incoming
        {
            public byte[] Data;
            public int Filled;
        }

        // Tunables. Plain static fields on purpose (hot reload); patch 1.4 moves them into the server config.
        public static int ChunkSize = 6000;
        public static int MaxProviderBytes = 8000000;
        public static uint RequestCooldownTicks = 120;

        static readonly List<Provider> providers = new();
        static readonly List<Action<int>> disconnectHooks = new();

        // Server state.
        static readonly Dictionary<int, Job> jobs = new();
        static readonly Dictionary<int, uint> lastRequestTick = new();

        // Client state.
        static int currentToken;
        static Stopwatch clock;
        static readonly Dictionary<string, Incoming> incoming = new();
        static readonly HashSet<string> received = new();

        /// <summary>Client: true once the last full-state request finished (always true in single-player, where the state is already local).</summary>
        public static bool Completed;

        /// <summary>Client: set by /fjoin so the finished sync prints its summary in chat.</summary>
        public static bool AnnounceInChat;

        /// <summary>Client: raised after every provider of a full-state request has been read (UI that depends on synced state can rebuild).</summary>
        public static event Action JoinCompleted;

        // ---------------------------------------------------------------- registration

        /// <summary>
        /// Registers a join provider. Call from ModSystem.Load() on every side. key is a short stable string (it is the id on the wire).
        /// serverWrite must write the system's complete state; clientRead must read exactly what was written and replace local state.
        /// </summary>
        public static void Register(string key, Action<BinaryWriter> serverWrite, Action<BinaryReader> clientRead)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("A join provider needs a key");

            foreach (Provider existing in providers)
                if (existing.Key == key)
                    throw new InvalidOperationException("Join provider '" + key + "' is already registered");

            providers.Add(new Provider { Key = key, Write = serverWrite, Read = clientRead });
        }

        /// <summary>
        /// Registers a cleanup that runs when a player disconnects (server and remote clients; see ModPlayer.PlayerDisconnect).
        /// The hook receives the player index. Call from ModSystem.Load().
        /// </summary>
        public static void RegisterDisconnectHook(Action<int> hook)
        {
            if (hook != null)
                disconnectHooks.Add(hook);
        }

        public static void Clear()
        {
            providers.Clear();
            disconnectHooks.Clear();
            jobs.Clear();
            lastRequestTick.Clear();
            incoming.Clear();
            received.Clear();
            clock = null;
            currentToken = 0;
            Completed = false;
            AnnounceInChat = false;
            JoinCompleted = null;
        }

        // ---------------------------------------------------------------- client: ask for the state

        /// <summary>Client: asks the server for the full state. Called from OnEnterWorld and by /fjoin. In single-player nothing is sent over a network.</summary>
        public static void RequestFullState()
        {
            int token = ++currentToken;
            Completed = false;
            incoming.Clear();
            received.Clear();
            clock = Stopwatch.StartNew();

            FactorrariaNet.SendRequest(FactorrariaPacket.JoinRequest, writer => writer.Write(token));

            if (FactorrariaNet.IsSinglePlayer)
                Completed = true; // the server's state already is the player's state
        }

        // ---------------------------------------------------------------- server: request handler and pump

        // Server side (or the direct call in single-player).
        public static void HandleRequest(BinaryReader reader, int sender)
        {
            int token = reader.ReadInt32();

            if (Main.netMode != NetmodeID.Server)
            {
                FactorrariaNet.Log("join request #" + token + ": single-player, the state is already local, nothing to send");
                return;
            }

            if (sender < 0 || sender >= Main.maxPlayers || !Main.player[sender].active)
            {
                FactorrariaNet.Log("join request #" + token + " dropped: sender " + sender + " is not an active player");
                return;
            }

            if (jobs.ContainsKey(sender))
            {
                FactorrariaNet.Log("join request #" + token + " dropped: " + Main.player[sender].name + " (" + sender + ") is still being sent a state");
                return;
            }

            uint now = Main.GameUpdateCount;
            if (lastRequestTick.TryGetValue(sender, out uint last) && now - last < RequestCooldownTicks)
            {
                FactorrariaNet.Log("join request #" + token + " dropped: " + Main.player[sender].name + " (" + sender + ") asked again too soon");
                return;
            }

            lastRequestTick[sender] = now;
            jobs[sender] = new Job { Token = token };
            FactorrariaNet.Log("join request #" + token + " from " + Main.player[sender].name + " (" + sender + "): sending " + providers.Count + " providers");
        }

        /// <summary>Server: called every tick. Each queued player gets at most one chunk per tick.</summary>
        public static void ServerTick()
        {
            if (Main.netMode != NetmodeID.Server || jobs.Count == 0)
                return;

            foreach (int who in new List<int>(jobs.Keys))
            {
                Job job = jobs[who];
                if (who < 0 || who >= Main.maxPlayers || !Main.player[who].active)
                {
                    jobs.Remove(who);
                    continue;
                }

                try
                {
                    StepJob(who, job);
                }
                catch (Exception e)
                {
                    FactorrariaNet.Log("join sync for player " + who + " failed and was cancelled: " + e);
                    jobs.Remove(who);
                }
            }
        }

        static void StepJob(int who, Job job)
        {
            if (job.NextProvider >= providers.Count)
            {
                int count = providers.Count;
                int bytes = job.TotalBytes;
                int token = job.Token;
                FactorrariaNet.SendToClient(who, FactorrariaPacket.JoinDone, writer =>
                {
                    writer.Write(token);
                    writer.Write(count);
                    writer.Write(bytes);
                });
                FactorrariaNet.Log("join sync #" + token + " finished for player " + who + ": " + count + " providers, " + bytes + " bytes");
                jobs.Remove(who);
                return;
            }

            Provider provider = providers[job.NextProvider];

            if (job.Data == null)
            {
                job.Data = Serialize(provider);
                job.Offset = 0;
                if (job.Data == null)
                {
                    // The provider threw: skip it (already logged). The client reports it as missing in the summary.
                    job.NextProvider++;
                    return;
                }
            }

            byte[] data = job.Data;
            int offset = job.Offset;
            int length = Math.Max(0, Math.Min(ChunkSize, data.Length - offset));
            int tokenCopy = job.Token;
            string key = provider.Key;

            FactorrariaNet.SendToClient(who, FactorrariaPacket.JoinChunk, writer =>
            {
                writer.Write(tokenCopy);
                writer.Write(key);
                writer.Write(data.Length);
                writer.Write(length);
                writer.Write(data, offset, length);
            });

            job.Offset += length;
            job.TotalBytes += length;
            if (job.Offset >= data.Length)
            {
                job.Data = null;
                job.NextProvider++;
            }
        }

        static byte[] Serialize(Provider provider)
        {
            try
            {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream))
                    provider.Write(writer);

                byte[] data = stream.ToArray();
                if (data.Length > MaxProviderBytes)
                {
                    FactorrariaNet.Log("join provider '" + provider.Key + "' wrote " + data.Length + " bytes (limit " + MaxProviderBytes + "), skipped");
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                FactorrariaNet.Log("join provider '" + provider.Key + "' failed to write, skipped: " + e);
                return null;
            }
        }

        // ---------------------------------------------------------------- client: receive

        public static void HandleChunk(BinaryReader reader, int sender)
        {
            int token = reader.ReadInt32();
            string key = reader.ReadString();
            int total = reader.ReadInt32();
            int length = reader.ReadInt32();

            if (token != currentToken)
                return; // answer to an older request

            if (total < 0 || total > MaxProviderBytes || length < 0 || length > total)
            {
                FactorrariaNet.Log("join chunk for '" + key + "' dropped: bad sizes (total " + total + ", chunk " + length + ")");
                return;
            }

            Provider provider = providers.Find(p => p.Key == key);
            if (provider == null)
            {
                FactorrariaNet.Log("join chunk for unknown provider '" + key + "' dropped");
                return;
            }

            if (!incoming.TryGetValue(key, out Incoming buffer))
                incoming[key] = buffer = new Incoming { Data = new byte[total] };

            if (buffer.Data.Length != total || buffer.Filled + length > total)
            {
                FactorrariaNet.Log("join chunk for '" + key + "' dropped: it does not fit the buffer");
                incoming.Remove(key);
                return;
            }

            byte[] bytes = reader.ReadBytes(length);
            Buffer.BlockCopy(bytes, 0, buffer.Data, buffer.Filled, bytes.Length);
            buffer.Filled += bytes.Length;

            if (buffer.Filled < total)
                return;

            incoming.Remove(key);
            try
            {
                using var stream = new MemoryStream(buffer.Data);
                using var providerReader = new BinaryReader(stream);
                provider.Read(providerReader);
                if (stream.Position != stream.Length)
                    FactorrariaNet.Log("join provider '" + key + "' read " + stream.Position + " of " + stream.Length + " bytes (reader and writer disagree)");
                received.Add(key);
            }
            catch (Exception e)
            {
                FactorrariaNet.Log("join provider '" + key + "' failed to read: " + e);
            }
        }

        public static void HandleDone(BinaryReader reader, int sender)
        {
            int token = reader.ReadInt32();
            int count = reader.ReadInt32();
            int bytes = reader.ReadInt32();

            if (token != currentToken)
                return;

            var missing = new List<string>();
            foreach (Provider provider in providers)
                if (!received.Contains(provider.Key))
                    missing.Add(provider.Key);

            double milliseconds = clock != null ? clock.Elapsed.TotalMilliseconds : 0;
            string text = "join sync #" + token + ": " + received.Count + "/" + providers.Count + " providers read (server sent " + count + "), "
                + bytes + " bytes, " + milliseconds.ToString("0") + " ms";
            if (missing.Count > 0)
                text += ", MISSING: " + string.Join(", ", missing);

            FactorrariaNet.Log(text);
            incoming.Clear();
            Completed = true;

            if (AnnounceInChat)
            {
                AnnounceInChat = false;
                Main.NewText("fjoin: " + text, missing.Count == 0 ? Color.LightGreen : Color.OrangeRed);
            }

            try
            {
                JoinCompleted?.Invoke();
            }
            catch (Exception e)
            {
                FactorrariaNet.Log("JoinCompleted handler failed: " + e);
            }
        }

        // ---------------------------------------------------------------- disconnect

        /// <summary>Called from FactorrariaPlayerSync.PlayerDisconnect on the server and on remote clients.</summary>
        public static void OnPlayerDisconnect(int playerIndex)
        {
            jobs.Remove(playerIndex);
            lastRequestTick.Remove(playerIndex);

            foreach (Action<int> hook in disconnectHooks)
            {
                try
                {
                    hook(playerIndex);
                }
                catch (Exception e)
                {
                    FactorrariaNet.Log("disconnect hook failed for player " + playerIndex + ": " + e);
                }
            }
        }
    }
}