using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.WorldGeneration
{
    public class EvilOreRemover : ModSystem
    {
        public static bool Enabled = true;

        public override void PostWorldGen()
        {
            if (!Enabled)
                return;

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = 0; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile)
                        continue;

                    if (tile.TileType == TileID.Demonite || tile.TileType == TileID.Crimtane)
                    {
                        tile.TileType = TileID.Glass;
                        WorldGen.SquareTileFrame(x, y, true); // re-frame so merging with neighbours looks right
                    }
                }
            }
        }
    }
}