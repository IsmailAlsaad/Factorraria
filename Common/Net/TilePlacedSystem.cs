using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Registers the tile-placed packet, owns the registry's lifetime and carries the TEMPORARY test consumer that logs every placement
    // the simulation hears about. Real consumers arrive with patch 3.3; remove the test consumer in Phase 10.
    public class TilePlacedSystem : ModSystem
    {
        public static bool LogPlacements = true;

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.TilePlaced, PacketDirection.ToServer, TilePlaced.HandleRequest);
            TilePlaced.Register(new PlacementLogConsumer());
        }

        public override void Unload()
        {
            TilePlaced.Clear();
        }

        sealed class PlacementLogConsumer : ITilePlacedConsumer
        {
            public bool AppliesTo(int tileType) => LogPlacements;

            public void OnTilePlaced(int x, int y, int tileType, int placer)
            {
                string name = TileLoader.GetTile(tileType)?.Name ?? ("vanilla " + tileType);
                string who = placer >= 0 && placer < Main.maxPlayers ? Main.player[placer].name + " (" + placer + ")" : "server or single-player";
                FactorrariaNet.Log("tile placed: " + name + " (type " + tileType + ") at (" + x + ", " + y + ") by " + who);
            }
        }
    }
}