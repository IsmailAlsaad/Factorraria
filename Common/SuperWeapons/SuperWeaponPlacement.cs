using Factorraria.Common.Net;
using Factorraria.Content.Tiles.SuperWeapons.MissileDrill;
using Factorraria.Content.Tiles.SuperWeapons.MissileTank;
using Factorraria.Content.Tiles.SuperWeapons.MissileThruster;
using Terraria;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.SuperWeapons
{
    /// <summary>
    /// The "no placing in mid-air" rule for the super weapon parts (drill, tank, thruster). A part may be placed when at least one tile
    /// along the outside of its footprint (the four sides, corners not counted) is solid, a platform, or another super weapon part, so a
    /// whole weapon can still hang off one anchored part.
    /// AUTHORITY / SYNC: none. Called from the parts' CanUseItem on the placing player's own client (other copies of the player are
    /// skipped, the same way GoldWireItem does), so no state and no packet; placing itself is still vanilla's tile sync.
    /// </summary>
    public static class SuperWeaponPlacement
    {
        public static bool CanPlacePart(Player player, Item item)
        {
            if (!FactorrariaNet.IsOwner(player))
                return true;

            TileObjectData data = TileObjectData.GetTileData(item.createTile, item.placeStyle);
            if (data == null)
                return true;

            // The footprint's top-left corner: the cursor tile minus the style's origin (the same offset vanilla uses to place it).
            int left = Player.tileTargetX - data.Origin.X;
            int top = Player.tileTargetY - data.Origin.Y;
            int right = left + data.Width - 1;
            int bottom = top + data.Height - 1;

            for (int x = left; x <= right; x++)
            {
                if (IsSupport(x, top - 1) || IsSupport(x, bottom + 1))
                    return true;
            }

            for (int y = top; y <= bottom; y++)
            {
                if (IsSupport(left - 1, y) || IsSupport(right + 1, y))
                    return true;
            }

            return false;
        }

        static bool IsSupport(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return false;

            Tile tile = Main.tile[x, y];
            if (!tile.HasUnactuatedTile)
                return false;

            ushort type = tile.TileType;
            return Main.tileSolid[type] || Main.tileSolidTop[type] || IsPart(type);
        }

        static bool IsPart(int type)
        {
            return type == ModContent.TileType<MissileDrillTile>()
                || type == ModContent.TileType<MissileTankTile>()
                || type == ModContent.TileType<MissileThrusterTile>();
        }
    }
}