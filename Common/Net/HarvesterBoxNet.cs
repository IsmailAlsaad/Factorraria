using Factorraria.Common.Harvester;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Harvester Box staff (Phase 4). The server (or single player) owns the whole harvest cycle; the tile changes and the drops reach clients
    // through the vanilla and vItem sync. This packet is only the cosmetic start of the staff's strike:
    //   single player      -> nothing is sent, the visuals read the entity's own cycle state
    //   dedicated server   -> HarvesterBoxStrike to the players near the box (payload: tile x, y of the box)
    //   host that plays    -> the same packets go to the guests; the host's own visuals read the entity's cycle state, so nothing is played locally
    //   multiplayer client -> queues the packet and starts the strike on the row entity it already has (a missing replica just means no animation)
    // A client works out "ready" (the active gear) from its own tiles, and replays the rest of the strike with the same tick counts as the server,
    // so nothing else is synced: no join sync, no cancel packet.
    public class HarvesterBoxNetSystem : ModSystem
    {
        public float NearbyTiles = 100f;    // players further away than this do not get the packet
        public int MaxPerTick = 64;         // client: at most this many strikes are started in one tick

        ConcurrentQueue<Point16> incoming = new ConcurrentQueue<Point16>();

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.HarvesterBoxStrike, PacketDirection.ToClient, HandleStrike);
        }

        public override void OnWorldLoad() => Clear();

        public override void OnWorldUnload() => Clear();

        void Clear()
        {
            while (incoming.TryDequeue(out _)) { }
        }

        // Server only (a no-op everywhere else).
        public void AnnounceStrike(int tileX, int tileY)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            FactorrariaNet.SendToNearby(new Vector2(tileX * 16f + 8f, tileY * 16f + 8f), NearbyTiles, FactorrariaPacket.HarvesterBoxStrike, writer =>
            {
                writer.Write(tileX);
                writer.Write(tileY);
            });
        }

        // May run on the network thread: only validates and queues.
        void HandleStrike(System.IO.BinaryReader reader, int sender)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
                return;

            incoming.Enqueue(new Point16(x, y));
        }

        // Client, main thread.
        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            int budget = MaxPerTick;
            while (budget-- > 0 && incoming.TryDequeue(out Point16 box))
            {
                if (HarvesterBoxRows.TryFindRowEntity(box.X, box.Y, out TileEntity entity, out _) && entity is IHarvesterBoxRow row)
                    row.BeginClientStrike(box.X);
            }
        }
    }
}