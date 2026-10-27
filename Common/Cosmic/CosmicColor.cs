using Microsoft.Xna.Framework;
using Terraria.ID;

namespace Factorraria.Common.Cosmic
{
    // The seven particle colors, ordered from the highest frequency (white, treated as UV) to the lowest (red).
    // The byte value is the index into every per-color table (CosmicRadiationSystem.BaseEnergy, Multiplier, ClusterMin, ClusterMax)
    // and the id on the wire, so never reorder or insert: append only.
    public enum CosmicColor : byte
    {
        White = 0,
        Violet = 1,
        Blue = 2,
        Green = 3,
        Yellow = 4,
        Orange = 5,
        Red = 6
    }

    // Fixed facts about the colors (which gemspark absorbs which color, what comes next in the cascade, how it is drawn).
    // Balance numbers (energy, multiplier, cluster size) are NOT here: they are tunables on CosmicRadiationSystem.
    public static class CosmicColors
    {
        public static int Count => 7;

        public static bool HasNext(CosmicColor color) => color != CosmicColor.Red;

        // The color a gemspark of this color re-emits. Red has no next color (it returns Red; check HasNext first).
        public static CosmicColor Next(CosmicColor color) => color == CosmicColor.Red ? CosmicColor.Red : (CosmicColor)((byte)color + 1);

        // The lit vanilla gemspark block that absorbs this color. The offline (unpowered) gemspark variants are not receivers.
        public static int GemsparkTile(CosmicColor color)
        {
            switch (color)
            {
                case CosmicColor.White: return TileID.DiamondGemspark;
                case CosmicColor.Violet: return TileID.AmethystGemspark;
                case CosmicColor.Blue: return TileID.SapphireGemspark;
                case CosmicColor.Green: return TileID.EmeraldGemspark;
                case CosmicColor.Yellow: return TileID.TopazGemspark;
                case CosmicColor.Orange: return TileID.AmberGemspark;
                default: return TileID.RubyGemspark;
            }
        }

        public static bool TryFromGemspark(int tileType, out CosmicColor color)
        {
            for (int i = 0; i < Count; i++)
            {
                if (GemsparkTile((CosmicColor)i) == tileType)
                {
                    color = (CosmicColor)i;
                    return true;
                }
            }

            color = CosmicColor.White;
            return false;
        }

        // Cloud blocks that can be ionized: vanilla's own cloud set (Cloud, Rain Cloud, Snow Cloud).
        public static bool IsCloudTile(int tileType) => TileID.Sets.Clouds[tileType];

        public static Color DrawColor(CosmicColor color)
        {
            switch (color)
            {
                case CosmicColor.White: return new Color(255, 255, 255);
                case CosmicColor.Violet: return new Color(165, 90, 255);
                case CosmicColor.Blue: return new Color(70, 120, 255);
                case CosmicColor.Green: return new Color(70, 225, 100);
                case CosmicColor.Yellow: return new Color(255, 230, 70);
                case CosmicColor.Orange: return new Color(255, 150, 45);
                default: return new Color(255, 65, 55);
            }
        }
    }
}