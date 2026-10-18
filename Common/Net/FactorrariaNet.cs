using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Net
{
    // The one place packets are routed, built and sent (roadmap rules 1-3 and 9). Systems register a handler per packet type from
    // ModSystem.Load() on every side; Factorraria.HandlePacket forwards into Handle().
    //
    // Requests, not edits: a client calls SendRequest, the server handler validates and applies, then broadcasts with SendToClient /
    // Broadcast / SendToNearby. In single-player (and when server code makes the request itself) SendRequest runs the server handler
    // directly, so single-player never depends on the network path, and the send helpers do nothing there because the server's state
    // already is the player's state.
    public static class FactorrariaNet
    {
        sealed class Route
        {
            public PacketDirection Direction;
            public PacketHandler Handler;
            public int Cost = 1; // rate-limiter tokens one packet of this type costs
        }

        static readonly Dictionary<FactorrariaPacket, Route> routes = new();
        static Mod modInstance;

        // Extra tiles of slack on top of the vanilla tile range when the server checks a request, so latency and a moving player
        // are not rejected. Comes from the server config (NetworkConfig, patch 1.4).
        public static float ReachSlackTiles => global::Factorraria.Content.Configs.NetworkConfig.Current.ReachSlackTiles;

        static Mod ModInstance => modInstance ??= ModLoader.GetMod("Factorraria");

        // ---------------------------------------------------------------- which side am I

        /// <summary>True where the simulation runs: single-player, Host &amp; Play's server process and a dedicated server.</summary>
        public static bool IsServerSide => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>True when this process has a player and a screen (everything except a dedicated server).</summary>
        public static bool HasLocalClient => !Main.dedServ;

        public static bool IsSinglePlayer => Main.netMode == NetmodeID.SinglePlayer;

        /// <summary>True only for the player this process controls. Gate input, Main.MouseWorld and one-time spawns with it (rule 4).</summary>
        public static bool IsOwner(Player player) => player != null && HasLocalClient && player.whoAmI == Main.myPlayer;

        /// <summary>
        /// Owner only, multiplayer client only: send this player's position, velocity and controls to everyone right now (vanilla message 13).
        /// Player has no netUpdate flag, and vanilla sends this message only when something it watches changes (a key, a jump), so anything
        /// that moves the owner without a key press (the Mech Suit, the Mechanical Arm's push) has to ask for it. Call it after the movement
        /// of the frame (PostUpdate, or a projectile's AI, which runs after the players), so the packet carries this frame's position.
        /// </summary>
        public static void SyncPlayerNow(Player player)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || !IsOwner(player) || !player.active)
                return;

            NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, player.whoAmI);
        }

        /// <summary>
        /// Owner only, multiplayer client only: tell the server (which relays to the others) what an inventory slot now holds, after code
        /// changed that item in place (SetDefaults, stack, prefix). Finds the slot by reference in the player's inventory (0-58), so it
        /// does nothing for an item that is not there (a chest or bank item, a copy). Sends vanilla's SyncEquipment message. Single-player
        /// and the server do nothing.
        /// </summary>
        public static void SyncInventoryItem(Player player, Item item)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || !IsOwner(player) || item == null)
                return;

            for (int i = 0; i < player.inventory.Length; i++)
            {
                if (ReferenceEquals(player.inventory[i], item))
                {
                    NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, i, item.prefix);
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- reach

        /// <summary>
        /// Server-side check that a player is close enough to a tile to have done something to it: vanilla tile range plus block range,
        /// the held item's tile boost and ReachSlackTiles. Dead or inactive players never pass.
        /// </summary>
        public static bool InReach(Player player, int tileX, int tileY)
        {
            if (player == null || !player.active || player.dead)
                return false;

            float extra = ReachSlackTiles + player.blockRange + Math.Max(0, player.HeldItem?.tileBoost ?? 0);
            float dx = Math.Abs(player.Center.X / 16f - (tileX + 0.5f));
            float dy = Math.Abs(player.Center.Y / 16f - (tileY + 0.5f));
            return dx <= Player.tileRangeX + extra && dy <= Player.tileRangeY + extra;
        }

        // ---------------------------------------------------------------- items on the wire

        /// <summary>Writes an item with its stack, prefix and mod data (the same serializer vanilla uses), so nothing is lost in transit.</summary>
        public static void WriteItem(BinaryWriter writer, Item item)
        {
            ItemIO.Send(item ?? new Item(), writer, writeStack: true, writeFavorite: false);
        }

        public static Item ReadItem(BinaryReader reader)
        {
            return ItemIO.Receive(reader, readStack: true, readFavorite: false);
        }

        // ---------------------------------------------------------------- routing

        /// <summary>Registers the handler for a packet type. Call from ModSystem.Load() on every side. A type can only be registered once.</summary>
        public static void Register(FactorrariaPacket type, PacketDirection direction, PacketHandler handler, int cost = 1)
        {
            if (routes.ContainsKey(type))
                throw new InvalidOperationException("Factorraria packet " + type + " is already registered");

            routes[type] = new Route { Direction = direction, Handler = handler, Cost = cost };
        }

        public static void Clear()
        {
            routes.Clear();
            modInstance = null;
        }

        /// <summary>Called from Factorraria.HandlePacket. A bad packet from a client must never crash the server, so everything is caught.</summary>
        public static void Handle(BinaryReader reader, int whoAmI)
        {
            byte raw = 0;
            try
            {
                raw = reader.ReadByte();
                var type = (FactorrariaPacket)raw;

                if (!routes.TryGetValue(type, out Route route))
                {
                    Warn("unknown packet id " + raw + " from " + whoAmI);
                    return;
                }

                bool toServer = route.Direction == PacketDirection.ToServer;
                if (toServer != (Main.netMode == NetmodeID.Server))
                {
                    Warn(type + " arrived on the wrong side (netMode " + Main.netMode + "), dropped");
                    return;
                }

                // Every client-to-server packet goes through the per-player rate limiter (patch 1.4) before its handler runs.
                if (toServer && !NetRateLimiter.TryConsume(whoAmI, route.Cost))
                    return;

                route.Handler(reader, toServer ? whoAmI : -1);
            }
            catch (Exception e)
            {
                ModInstance?.Logger.Error("[MP-Net] handler for packet id " + raw + " failed (sender " + whoAmI + "): " + e);
            }
        }

        // ---------------------------------------------------------------- sending

        /// <summary>
        /// Asks the server to do something. On a multiplayer client this sends the packet. In single-player, or when server code calls it,
        /// the server handler runs right here (through the same write/read path), with sender = the local player, or -1 for server code.
        /// </summary>
        public static void SendRequest(FactorrariaPacket type, Action<BinaryWriter> write = null)
        {
            if (!routes.TryGetValue(type, out Route route) || route.Direction != PacketDirection.ToServer)
            {
                Warn("SendRequest: " + type + " is not registered as a ToServer packet");
                return;
            }

            byte[] body = BuildBody(type, write);

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                SendBody(-1, -1, body);
                return;
            }

            int sender = Main.dedServ ? -1 : Main.myPlayer;
            try
            {
                using var reader = new BinaryReader(new MemoryStream(body));
                reader.ReadByte(); // packet id
                route.Handler(reader, sender);
            }
            catch (Exception e)
            {
                ModInstance?.Logger.Error("[MP-Net] local handler for " + type + " failed: " + e);
            }
        }

        /// <summary>Server only (no-op elsewhere): sends one packet to one client.</summary>
        public static void SendToClient(int toClient, FactorrariaPacket type, Action<BinaryWriter> write = null)
        {
            if (Main.netMode != NetmodeID.Server || !IsConnectedClient(toClient) || !TryBuildClientBody(type, write, out byte[] body))
                return;

            SendBody(toClient, -1, body);
        }

        /// <summary>Server only (no-op elsewhere): sends one packet to every connected client except ignoreClient.</summary>
        public static void Broadcast(FactorrariaPacket type, Action<BinaryWriter> write = null, int ignoreClient = -1)
        {
            if (Main.netMode != NetmodeID.Server || !TryBuildClientBody(type, write, out byte[] body))
                return;

            SendBody(-1, ignoreClient, body);
        }

        /// <summary>Server only (no-op elsewhere): sends one packet to every connected client within radiusTiles of a world position.</summary>
        public static void SendToNearby(Vector2 worldCenter, float radiusTiles, FactorrariaPacket type, Action<BinaryWriter> write = null, int ignoreClient = -1)
        {
            if (Main.netMode != NetmodeID.Server || !TryBuildClientBody(type, write, out byte[] body))
                return;

            float radius = radiusTiles * 16f;
            float radiusSquared = radius * radius;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (i == ignoreClient || !IsConnectedClient(i))
                    continue;

                if (Vector2.DistanceSquared(Main.player[i].Center, worldCenter) <= radiusSquared)
                    SendBody(i, -1, body);
            }
        }

        // ---------------------------------------------------------------- internals

        static bool IsConnectedClient(int index)
        {
            return index >= 0 && index < Main.maxPlayers && Main.player[index].active;
        }

        static bool TryBuildClientBody(FactorrariaPacket type, Action<BinaryWriter> write, out byte[] body)
        {
            body = null;
            if (!routes.TryGetValue(type, out Route route) || route.Direction != PacketDirection.ToClient)
            {
                Warn("send: " + type + " is not registered as a ToClient packet");
                return false;
            }

            body = BuildBody(type, write);
            return true;
        }

        // The packet is built once into a byte array (id byte first) so one body can go to many clients; a ModPacket is single-use.
        static byte[] BuildBody(FactorrariaPacket type, Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)type);
                write?.Invoke(writer);
            }
            return stream.ToArray();
        }

        static void SendBody(int toClient, int ignoreClient, byte[] body)
        {
            ModPacket packet = ModInstance.GetPacket(body.Length + 8);
            packet.Write(body);
            packet.Send(toClient, ignoreClient);
        }

        public static void Log(string message) => ModInstance?.Logger.Info("[MP-Net] " + message);

        static void Warn(string message) => ModInstance?.Logger.Warn("[MP-Net] " + message);
    }
}