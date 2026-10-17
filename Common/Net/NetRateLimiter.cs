using System.Diagnostics;
using Factorraria.Content.Configs;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Per-player token bucket applied by FactorrariaNet.Handle to every packet a client sends to the server (roadmap patch 1.4).
    // A request costs 1 token (a packet type can declare a higher cost when it is registered). When a player's bucket is empty the
    // packet is dropped before its handler runs and a summary line is logged at most once a second. Server code that calls
    // SendRequest itself, and single-player, never go through the limiter. Limits come from NetworkConfig.
    public static class NetRateLimiter
    {
        static readonly double[] tokens = new double[Main.maxPlayers];
        static readonly long[] lastRefill = new long[Main.maxPlayers];
        static readonly long[] lastLog = new long[Main.maxPlayers];
        static readonly int[] dropped = new int[Main.maxPlayers];
        static readonly bool[] started = new bool[Main.maxPlayers];

        /// <summary>Returns false when the player is over the limit (the packet must be dropped).</summary>
        public static bool TryConsume(int player, int cost = 1)
        {
            if (player < 0 || player >= Main.maxPlayers)
                return true;

            NetworkConfig config = NetworkConfig.Current;
            double rate = config.RequestsPerSecond;
            if (rate <= 0)
                return true;

            double capacity = System.Math.Max(1, config.RequestBurst);
            long now = Stopwatch.GetTimestamp();

            if (!started[player])
            {
                started[player] = true;
                tokens[player] = capacity;
                lastRefill[player] = now;
            }

            double elapsed = (now - lastRefill[player]) / (double)Stopwatch.Frequency;
            lastRefill[player] = now;
            tokens[player] = System.Math.Min(capacity, tokens[player] + elapsed * rate);

            if (tokens[player] >= cost)
            {
                tokens[player] -= cost;
                return true;
            }

            dropped[player]++;
            if (now - lastLog[player] >= Stopwatch.Frequency)
            {
                FactorrariaNet.Log("rate limit: dropped " + dropped[player] + " request(s) from " + Main.player[player].name + " (" + player
                    + ") in the last second (limit " + rate + "/s, burst " + capacity + ")");
                dropped[player] = 0;
                lastLog[player] = now;
            }
            return false;
        }

        public static void Reset(int player)
        {
            if (player < 0 || player >= Main.maxPlayers)
                return;

            started[player] = false;
            dropped[player] = 0;
            lastLog[player] = 0;
        }

        public static void ResetAll()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
                Reset(i);
        }
    }

    // Clears a player's bucket when they disconnect, so the next player in the slot starts with a full one.
    public class NetRateLimiterSystem : ModSystem
    {
        public override void Load()
        {
            JoinSync.RegisterDisconnectHook(NetRateLimiter.Reset);
        }

        public override void Unload()
        {
            NetRateLimiter.ResetAll();
        }
    }
}