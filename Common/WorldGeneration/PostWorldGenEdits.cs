using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.WorldGeneration
{
    public class PostWorldGenEdits : ModSystem
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

                    if (tile.TileType == TileID.Demonite || tile.TileType == TileID.Crimtane || tile.TileType == TileID.Obsidian)
                    {
                        tile.TileType = TileID.Stone;
                        WorldGen.SquareTileFrame(x, y, true);
                    }
                }
            }
        }
    }
}