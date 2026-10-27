using Factorraria.Common.Net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // Which tile types are cosmic receivers: a lookup by tile type, built from the registries (never from tile id numbers).
    //   0 to 6 = the lit gemspark block of that CosmicColor, 7 = a cloud block (CosmicReceiverIndex.CloudCode), 255 = not a receiver.
    public static class CosmicTileCodes
    {
        public const byte None = 255;

        static byte[] codes;

        public static byte CodeOf(int tileType)
        {
            byte[] map = codes;
            if (map == null || map.Length != TileLoader.TileCount)
                map = Build();

            return tileType >= 0 && tileType < map.Length ? map[tileType] : None;
        }

        static byte[] Build()
        {
            byte[] map = new byte[TileLoader.TileCount];
            for (int t = 0; t < map.Length; t++)
                map[t] = CosmicColors.IsCloudTile(t) ? CosmicReceiverIndex.CloudCode : None;

            for (int c = 0; c < CosmicColors.Count; c++)
            {
                int tile = CosmicColors.GemsparkTile((CosmicColor)c);
                if (tile >= 0 && tile < map.Length)
                    map[tile] = (byte)c;
            }

            codes = map;
            return map;
        }
    }

    // What a ray sees in the real world. Server and single-player only (rays are never traced on a client).
    public sealed class CosmicTileProbe : ITileProbe
    {
        public CosmicTileKind Probe(int x, int y, out CosmicColor gemsparkColor)
        {
            gemsparkColor = CosmicColor.White;

            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
                return CosmicTileKind.Solid;

            Tile tile = Main.tile[x, y];
            if (!tile.HasUnactuatedTile)
                return CosmicTileKind.Empty;

            int type = tile.TileType;
            byte code = CosmicTileCodes.CodeOf(type);

            if (code < CosmicColors.Count)
            {
                gemsparkColor = (CosmicColor)code;
                return CosmicTileKind.Gemspark;
            }

            if (code == CosmicReceiverIndex.CloudCode)
                return CosmicTileKind.Cloud;

            // Platforms are solid AND solid-top; they let rays through (same test as Common/Carts/CartCargo.cs).
            return Main.tileSolid[type] && !Main.tileSolidTop[type] ? CosmicTileKind.Solid : CosmicTileKind.Empty;
        }
    }

    // A receiver was mined or destroyed: drop it from the index. Runs on the server and in single-player (a multiplayer client
    // never owns the simulation). KillTile also fires for a hit that does not break the tile (fail) and for dust-only calls
    // (effectOnly); those change nothing.
    public class CosmicReceiverGlobalTile : GlobalTile
    {
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (fail || effectOnly || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (CosmicTileCodes.CodeOf(type) != CosmicTileCodes.None)
                CosmicRadiationSystem.Instance.Sim.OnReceiverRemoved(i, j);
        }
    }

    // A gemspark or cloud block was placed (TilePlaced runs this where the simulation runs, once the server has verified the
    // placing client's request). Idempotent: adding a tile that is already indexed changes nothing.
    sealed class CosmicReceiverPlacedConsumer : ITilePlacedConsumer
    {
        public bool AppliesTo(int tileType) => CosmicTileCodes.CodeOf(tileType) != CosmicTileCodes.None;

        public void OnTilePlaced(int x, int y, int tileType, int placer)
        {
            CosmicRadiationSystem.Instance.Sim.OnReceiverPlaced(x, y, CosmicTileCodes.CodeOf(tileType));
        }
    }

    public class CosmicReceiverSystem : ModSystem
    {
        public override void Load()
        {
            TilePlaced.Register(new CosmicReceiverPlacedConsumer());
        }
    }
}
