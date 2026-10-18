using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // A system that needs to know "a tile was placed here" implements this and registers it with TilePlaced.Register() from
    // ModSystem.Load() (on every side). OnTilePlaced runs where the simulation runs (single-player, Host & Play's server, a dedicated
    // server), never on a multiplayer client. It must be IDEMPOTENT: the same placement can reach it twice (from the placing
    // player's request and from server code such as the Robotic Arm), and a second call must change nothing.
    public interface ITilePlacedConsumer
    {
        /// <summary>Cheap filter so the registry only calls consumers that care about this tile type.</summary>
        bool AppliesTo(int tileType);

        /// <summary>placer is the player index, or -1 when the placement came from server code or single-player.</summary>
        void OnTilePlaced(int x, int y, int tileType, int placer);
    }

    // Roadmap patch 1.3 (finding G0). Tile placement hooks (ModTile/GlobalTile.PlaceInWorld) only run on the process that ran the
    // placement, so on a multiplayer client the server never hears about it. One shared hook (TilePlacedGlobalTile) calls NotifyPlaced:
    //  - on a multiplayer client it sends one small request (x, y, tile type);
    //  - everywhere the simulation runs (single-player, server code like the Robotic Arm) it runs the consumers directly.
    // The server validates a client's request (tile at that position matches, sender in reach) before it runs the consumers.
    public static class TilePlaced
    {
        static readonly List<ITilePlacedConsumer> consumers = new();

        public static void Register(ITilePlacedConsumer consumer)
        {
            if (consumer != null && !consumers.Contains(consumer))
                consumers.Add(consumer);
        }

        public static void Clear()
        {
            consumers.Clear();
        }

        /// <summary>Called by the shared GlobalTile hook on whichever process ran the placement.</summary>
        public static void NotifyPlaced(int x, int y, int tileType)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                FactorrariaNet.SendRequest(FactorrariaPacket.TilePlaced, writer =>
                {
                    writer.Write((short)x);
                    writer.Write((short)y);
                    writer.Write((ushort)tileType);
                });
                // Patch 4.3: vanilla-tile machines also report the placer's biome (ModTile machines do it from Hook_AfterPlacement).
                MachinePlacementInfo.SendBiomeForVanillaTile(x, y, tileType);
                return;
            }

            RunConsumers(x, y, tileType, -1);
        }

        /// <summary>
        /// Runs the consumers for a placement. Callable directly from server code (for example the Robotic Arm, which places tiles on
        /// the server). Does nothing on a multiplayer client and when the tile at that position is not the stated type.
        /// </summary>
        public static void RunConsumers(int x, int y, int tileType, int placer = -1)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (!TileMatches(x, y, tileType))
                return;

            foreach (ITilePlacedConsumer consumer in consumers)
            {
                try
                {
                    if (consumer.AppliesTo(tileType))
                        consumer.OnTilePlaced(x, y, tileType, placer);
                }
                catch (Exception e)
                {
                    FactorrariaNet.Log("tile-placed consumer " + consumer.GetType().Name + " failed at (" + x + ", " + y + "): " + e);
                }
            }
        }

        // Server side (the packet from a client), or the direct call when server code used SendRequest (sender -1).
        public static void HandleRequest(BinaryReader reader, int sender)
        {
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();
            int tileType = reader.ReadUInt16();

            if (sender < 0)
            {
                RunConsumers(x, y, tileType, -1);
                return;
            }

            if (sender >= Main.maxPlayers)
                return;

            string who = Main.player[sender].name + " (" + sender + ")";

            if (!WorldGen.InWorld(x, y, 1) || tileType >= TileLoader.TileCount)
            {
                FactorrariaNet.Log("tile-placed request from " + who + " rejected: bad position or type (" + x + ", " + y + ", " + tileType + ")");
                return;
            }

            if (!TileMatches(x, y, tileType))
            {
                FactorrariaNet.Log("tile-placed request from " + who + " rejected: no tile of type " + tileType + " at (" + x + ", " + y + ")");
                return;
            }

            if (!FactorrariaNet.InReach(Main.player[sender], x, y))
            {
                FactorrariaNet.Log("tile-placed request from " + who + " rejected: (" + x + ", " + y + ") is out of reach");
                return;
            }

            RunConsumers(x, y, tileType, sender);
        }

        static bool TileMatches(int x, int y, int tileType)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == tileType;
        }
    }
}