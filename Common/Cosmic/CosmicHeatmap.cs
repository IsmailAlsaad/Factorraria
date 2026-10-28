using System;
using System.IO;

namespace Factorraria.Common.Cosmic
{
    // Per-world color heatmap (pure code, no Terraria types). The world is cut into 8 equal sections (one per color) along X; each section is the
    // hot spot of one color. Only the shuffled order is stored (Order[section] = color index); every weight is computed from it.
    // A color's weight at tile X is a bump over its own section that bleeds into the neighbors, on top of a constant floor:
    //   weight = floor + (1 - floor) * exp(-(x - sectionCenter)^2 / (2 * sigma^2)),  sigma = sigmaFraction * sectionWidth.
    // The weight is relative (1 at the center of the hot spot); pick colors by the weights at that column, so every color keeps a
    // small chance everywhere.
    public class CosmicHeatmap
    {
        // One section per color (equals CosmicColors.Count). N2 made it 8; the saved order and the wire format changed with it.
        public const int Sections = 8;

        // 1 = seven sections (C1 to C6f). A saved heatmap of another version is refused and a new one is generated.
        public int FormatVersion => 2;

        // Width in tiles of one lookup column. The table is rebuilt whenever the world width or the shape parameters change.
        public int TableStep = 16;

        public bool Ready { get; private set; }

        // Order[section] = color index. Section 0 is the leftmost.
        public byte[] Order = new byte[Sections];

        byte[] sectionOfColor = new byte[Sections];

        float[] table;
        int tableWorldTiles;
        float tableFloor;
        float tableSigma;

        public void Clear()
        {
            Ready = false;
            table = null;
        }

        // Random shuffle of the 8 colors across the 8 sections.
        public void Generate(Random rng)
        {
            byte[] order = new byte[Sections];
            for (int i = 0; i < Sections; i++)
                order[i] = (byte)i;

            for (int i = Sections - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            SetOrder(order);
        }

        // Accepts the order only if it is a permutation of 0..7 (a corrupt save or packet is refused and the old state is kept).
        public bool SetOrder(byte[] order)
        {
            if (order == null || order.Length != Sections)
                return false;

            bool[] seen = new bool[Sections];
            for (int i = 0; i < Sections; i++)
            {
                if (order[i] >= Sections || seen[order[i]])
                    return false;
                seen[order[i]] = true;
            }

            for (int i = 0; i < Sections; i++)
            {
                Order[i] = order[i];
                sectionOfColor[order[i]] = (byte)i;
            }

            Ready = true;
            table = null;
            return true;
        }

        public float SectionWidth(int worldTilesX) => worldTilesX / (float)Sections;

        public int SectionAt(int tileX, int worldTilesX)
        {
            int section = (int)(tileX / SectionWidth(worldTilesX));
            return Math.Clamp(section, 0, Sections - 1);
        }

        public CosmicColor HotColorOfSection(int section) => (CosmicColor)Order[Math.Clamp(section, 0, Sections - 1)];

        public int SectionOfColor(CosmicColor color) => sectionOfColor[(int)color];

        // The exact formula (used to build the lookup table and by the debug command).
        public float WeightExact(CosmicColor color, int tileX, int worldTilesX, float floor, float sigmaFraction)
        {
            float width = SectionWidth(worldTilesX);
            float center = (sectionOfColor[(int)color] + 0.5f) * width;
            float sigma = Math.Max(sigmaFraction * width, 1f);
            float d = tileX - center;
            return floor + (1f - floor) * MathF.Exp(-(d * d) / (2f * sigma * sigma));
        }

        void EnsureTable(int worldTilesX, float floor, float sigmaFraction)
        {
            if (table != null && tableWorldTiles == worldTilesX && tableFloor == floor && tableSigma == sigmaFraction)
                return;

            int step = Math.Max(1, TableStep);
            int columns = worldTilesX / step + 2;
            table = new float[columns * Sections];

            for (int c = 0; c < columns; c++)
            {
                int x = c * step + step / 2;
                for (int k = 0; k < Sections; k++)
                    table[c * Sections + k] = WeightExact((CosmicColor)k, x, worldTilesX, floor, sigmaFraction);
            }

            tableWorldTiles = worldTilesX;
            tableFloor = floor;
            tableSigma = sigmaFraction;
        }

        // Lookup-table weight (cheap; what the spawner uses). 0 while not Ready.
        public float Weight(CosmicColor color, int tileX, int worldTilesX, float floor, float sigmaFraction)
        {
            if (!Ready)
                return 0f;

            EnsureTable(worldTilesX, floor, sigmaFraction);
            int step = Math.Max(1, TableStep);
            int column = Math.Clamp(tileX / step, 0, table.Length / Sections - 1);
            return table[column * Sections + (int)color];
        }

        // Picks a color for a particle spawned at this column, weighted by the heatmap. Uniform while not Ready.
        public CosmicColor Pick(int tileX, int worldTilesX, float floor, float sigmaFraction, Random rng)
        {
            if (!Ready)
                return (CosmicColor)rng.Next(Sections);

            EnsureTable(worldTilesX, floor, sigmaFraction);
            int step = Math.Max(1, TableStep);
            int column = Math.Clamp(tileX / step, 0, table.Length / Sections - 1);

            float total = 0f;
            for (int k = 0; k < Sections; k++)
                total += table[column * Sections + k];

            float roll = (float)rng.NextDouble() * total;
            for (int k = 0; k < Sections; k++)
            {
                roll -= table[column * Sections + k];
                if (roll <= 0f)
                    return (CosmicColor)k;
            }

            return (CosmicColor)(Sections - 1);
        }

        // Wire and save format: version byte, count byte (0 = not ready, otherwise 8), then the 8 order bytes.
        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)FormatVersion);
            writer.Write((byte)(Ready ? Sections : 0));

            if (Ready)
            {
                for (int i = 0; i < Sections; i++)
                    writer.Write(Order[i]);
            }
        }

        // Returns false (and changes nothing) when the data is from another version or is not a valid permutation.
        public bool Read(BinaryReader reader)
        {
            byte version = reader.ReadByte();
            byte count = reader.ReadByte();

            if (version != FormatVersion)
                return false;

            if (count == 0)
            {
                Clear();
                return true;
            }

            if (count != Sections)
                return false;

            byte[] order = new byte[Sections];
            for (int i = 0; i < Sections; i++)
                order[i] = reader.ReadByte();

            return SetOrder(order);
        }
    }
}