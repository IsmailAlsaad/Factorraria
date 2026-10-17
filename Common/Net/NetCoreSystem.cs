using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Registers the built-in packets (the test ping) and clears the router on unload. Other systems register their own packets from
    // their own Load(); registration order does not matter.
    public class NetCoreSystem : ModSystem
    {
        static readonly Dictionary<int, long> pendingPings = new();
        static int nextPingToken;

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.Ping, PacketDirection.ToServer, HandlePing);
            FactorrariaNet.Register(FactorrariaPacket.PingReply, PacketDirection.ToClient, HandlePingReply);
        }

        public override void Unload()
        {
            FactorrariaNet.Clear();
            pendingPings.Clear();
        }

        /// <summary>Starts a ping (used by /fping). reply receives a line to show to the player.</summary>
        public static void SendPing(Action<string> reply)
        {
            int token = ++nextPingToken;
            pendingPings[token] = Stopwatch.GetTimestamp();
            FactorrariaNet.SendRequest(FactorrariaPacket.Ping, writer => writer.Write(token));

            if (FactorrariaNet.IsSinglePlayer)
            {
                // The server handler already ran right here and there is no network, so no reply packet will come.
                pendingPings.Remove(token);
                reply("fping #" + token + ": single-player, the request ran directly (no packet sent)");
            }
        }

        // Server side (or direct call in single-player).
        static void HandlePing(System.IO.BinaryReader reader, int sender)
        {
            int token = reader.ReadInt32();
            string who = sender >= 0 && sender < Main.maxPlayers ? Main.player[sender].name + " (" + sender + ")" : "server";
            FactorrariaNet.Log("ping #" + token + " from " + who);

            if (sender >= 0)
                FactorrariaNet.SendToClient(sender, FactorrariaPacket.PingReply, writer =>
                {
                    writer.Write(token);
                    writer.Write((uint)Main.GameUpdateCount);
                });
        }

        // Client side.
        static void HandlePingReply(System.IO.BinaryReader reader, int sender)
        {
            int token = reader.ReadInt32();
            uint serverTick = reader.ReadUInt32();

            if (!pendingPings.Remove(token, out long started))
                return;

            double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            string text = "fping #" + token + ": round trip " + milliseconds.ToString("0.0") + " ms (server tick " + serverTick + ")";
            FactorrariaNet.Log(text);
            Main.NewText(text, Microsoft.Xna.Framework.Color.LightGreen);
        }
    }
}