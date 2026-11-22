using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Every planter box breaks with the dust of its own soil (Daybloom: grass, Waterleaf: sand, Blinkroot: dirt, ...). The dust is vanilla's own: for one call
    // the tile at x, y looks like the reference tile, and vanilla's tile dust code (WorldGen.KillTile_MakeTileDust, already used by the Mech Suit) does the rest,
    // so the colour, amount and spread match breaking that block. The real type is always put back. Dust is visual only: it runs on clients, needs no packet.
    public static class PlanterBoxDust
    {
        public static void Spawn(int x, int y, int referenceTile)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            Tile tile = Main.tile[x, y];
            ushort real = tile.TileType;
            tile.TileType = (ushort)referenceTile;
            try
            {
                WorldGen.KillTile_MakeTileDust(x, y, tile);
            }
            finally
            {
                tile.TileType = real;
            }
        }
    }

    // The vanilla planter box (tile 380) is one tile with a style in frameY / 18, so its dust is chosen here by style. The styles come from the planter box
    // items (HarvesterBoxRegistry.ResolvePlanterStyles), no style number is hard-coded. Styles we know nothing about keep vanilla's dust.
    public class PlanterBoxGlobalTile : GlobalTile
    {
        public override bool CreateDust(int i, int j, int type, ref int dustType)
        {
            if (type != TileID.PlanterBox || !WorldGen.InWorld(i, j, 1))
                return true;

            if (!HarvesterBoxRegistry.PlanterDustTiles.TryGetValue(Main.tile[i, j].TileFrameY / 18, out int dustTile))
                return true;

            PlanterBoxDust.Spawn(i, j, dustTile);   // the reference tile is never 380, so this cannot loop back here
            return false;
        }
    }
}