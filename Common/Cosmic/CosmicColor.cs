using Microsoft.Xna.Framework;
using Terraria.ID;

namespace Factorraria.Common.Cosmic
{
    // The eight particle colors, ordered from the highest frequency (white, treated as UV) to the lowest (grey, N2: below red, no gemspark).
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
        Red = 6,
        Grey = 7   // N2: the lowest frequency. No gemspark block absorbs it; the ruby gemspark (red) re-emits it.
    }

    // Fixed facts about the colors (which gemspark absorbs which color, what comes next in the cascade, how it is drawn).
    // Balance numbers (energy, multiplier, cluster size) are NOT here: they are tunables on CosmicRadiationSystem.
    public static class CosmicColors
    {
        public static int Count => 8;

        // The last color in the order has no next step (it was Red until N2 added Grey below it).
        public static CosmicColor Lowest => CosmicColor.Grey;

        public static bool HasNext(CosmicColor color) => color != Lowest;

        // The color a gemspark of this color re-emits. The lowest color has no next color (it returns itself; check HasNext first).
        public static CosmicColor Next(CosmicColor color) => color == Lowest ? Lowest : (CosmicColor)((byte)color + 1);

        // Does a gemspark block exist that absorbs this color? A color without one (Grey) is never absorbed by any gemspark. Everything
        // that asks "which gemspark absorbs this color" goes through GemsparkTile, so such a color needs no special case anywhere else.
        public static bool HasGemspark(CosmicColor color) => GemsparkTile(color) >= 0;

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
                case CosmicColor.Red: return TileID.RubyGemspark;
                default: return -1;   // no gemspark block absorbs this color (Grey, N2)
            }
        }

        public static bool TryFromGemspark(int tileType, out CosmicColor color)
        {
            for (int i = 0; i < Count; i++)
            {
                int tile = GemsparkTile((CosmicColor)i);
                if (tile >= 0 && tile == tileType)   // a color with no gemspark (Grey, -1) never matches
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
                case CosmicColor.Red: return new Color(255, 65, 55);
                default: return new Color(150, 150, 168);   // Grey (N2): a pale grey, it is drawn additively so a darker one would vanish
            }
        }

        // The color an ionized cloud TILE is multiplied by (C6b). A multiply by white changes nothing, so the white (UV) particle uses a steel
        // grey that the pulse swings to and from the normal cloud color.
        public static Color TileTint(CosmicColor color)
        {
            if (color == CosmicColor.Grey)
                return new Color(100, 100, 122);   // N2: a dark storm grey, a cloud hit by grey goes dark

            return color == CosmicColor.White ? new Color(120, 135, 170) : DrawColor(color);
        }
    }
}