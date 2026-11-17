using Factorraria.Common.Systems;
using System.Collections.Concurrent;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Side feature: the sound and lava burst when HellstoneGrowthSystem turns a stone block into hellstone. The block itself is replicated
    // with a vanilla tile square; this packet is only the cosmetic part. Announce is called by the server (or single player) where the
    // block converts:
    //   single player      -> plays it right here
    //   dedicated server   -> sends HellstoneGrow to the players near the block (capped per tick, so a burst is not a flood)
    //   host that plays    -> the same packets go to the guests, and the host plays it locally too
    //   multiplayer client -> never calls it (the packet below plays it)
    public class HellstoneGrowNetSystem : ModSystem
    {
        public float NearbyTiles = 80f;     // players further away than this do not get the packet
        public int MaxPerTick = 6;          // server: at most this many effects are sent in one tick

        ConcurrentQueue<GrowBlock> incoming = new ConcurrentQueue<GrowBlock>();
        uint sentTick;
        int sentThisTick;

        struct GrowBlock
        {
            public int X;
            public int Y;
        }

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.HellstoneGrow, PacketDirection.ToClient, HandleGrow);
        }

        public override void OnWorldLoad() => Clear();

        public override void OnWorldUnload() => Clear();

        void Clear()
        {
            while (incoming.TryDequeue(out _)) { }
            sentTick = 0;
            sentThisTick = 0;
        }

        // Server and single player only.
        public void Announce(int x, int y)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Main.netMode == NetmodeID.Server)
            {
                uint now = Main.GameUpdateCount;
                if (sentTick != now)
                {
                    sentTick = now;
                    sentThisTick = 0;
                }
                if (sentThisTick >= MaxPerTick)
                    return;
                sentThisTick++;

                FactorrariaNet.SendToNearby(new Microsoft.Xna.Framework.Vector2(x * 16f + 8f, y * 16f + 8f), NearbyTiles, FactorrariaPacket.HellstoneGrow, writer =>
                {
                    writer.Write(x);
                    writer.Write(y);
                });

                // A host that also plays is not a connected client of itself, so it plays the effect directly.
                if (!Main.dedServ)
                    ModContent.GetInstance<HellstoneGrowthSystem>().PlayEffect(x, y);
                return;
            }

            ModContent.GetInstance<HellstoneGrowthSystem>().PlayEffect(x, y);
        }

        void HandleGrow(System.IO.BinaryReader reader, int sender)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
                return;

            incoming.Enqueue(new GrowBlock { X = x, Y = y });
        }

        // Client, main thread.
        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            int budget = 8; // never more than a handful of effects in one tick
            while (budget-- > 0 && incoming.TryDequeue(out GrowBlock block))
                ModContent.GetInstance<HellstoneGrowthSystem>().PlayEffect(block.X, block.Y);
        }
    }
}