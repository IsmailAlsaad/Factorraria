using System;
using System.IO;

namespace Factorraria.Common.Cosmic
{
    // Per-world color heatmap (pure code, no Terraria types). The world is cut into 7 equal sections along X; each section is the
    // hot spot of one color. Only the shuffled order is stored (Order[section] = color index); every weight is computed from it.
    // A color's weight at tile X is a bump over its own section that bleeds into the neighbors, on top of a constant floor:
    //   weight = floor + (1 - floor) * exp(-(x - sectionCenter)^2 / (2 * sigma^2)),  sigma = sigmaFraction * sectionWidth.
    // The weight is relative (1 at the center of the hot spot); pick colors by the weights at that column, so every color keeps a
    // small chance everywhere.
    public class CosmicHeatmap
    {
        public int FormatVersion => 1;

        // Width in tiles of one lookup column. The table is rebuilt whenever the world width or the shape parameters change.
        public int TableStep = 16;

        public bool Ready { get; private set; }

        // Order[section] = color index. Section 0 is the leftmost.
        public byte[] Order = new byte[7];

        byte[] sectionOfColor = new byte[7];

        float[] table;
        int tableWorldTiles;
        float tableFloor;
        float tableSigma;

        public void Clear()
        {
            Ready = false;
            table = null;
        }

        // Random shuffle of the 7 colors across the 7 sections.
        public void Generate(Random rng)
        {
            byte[] order = new byte[7];
            for (int i = 0; i < 7; i++)
                order[i] = (byte)i;

            for (int i = 6; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            SetOrder(order);
        }

        // Accepts the order only if it is a permutation of 0..6 (a corrupt save or packet is refused and the old state is kept).
        public bool SetOrder(byte[] order)
        {
            if (order == null || order.Length != 7)
                return false;

            bool[] seen = new bool[7];
            for (int i = 0; i < 7; i++)
            {
                if (order[i] > 6 || seen[order[i]])
                    return false;
                seen[order[i]] = true;
            }

            for (int i = 0; i < 7; i++)
            {
                Order[i] = order[i];
                sectionOfColor[order[i]] = (byte)i;
            }

            Ready = true;
            table = null;
            return true;
        }

        public float SectionWidth(int worldTilesX) => worldTilesX / 7f;

        public int SectionAt(int tileX, int worldTilesX)
        {
            int section = (int)(tileX / SectionWidth(worldTilesX));
            return Math.Clamp(section, 0, 6);
        }

        public CosmicColor HotColorOfSection(int section) => (CosmicColor)Order[Math.Clamp(section, 0, 6)];

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
            table = new float[columns * 7];

            for (int c = 0; c < columns; c++)
            {
                int x = c * step + step / 2;
                for (int k = 0; k < 7; k++)
                    table[c * 7 + k] = WeightExact((CosmicColor)k, x, worldTilesX, floor, sigmaFraction);
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
            int column = Math.Clamp(tileX / step, 0, table.Length / 7 - 1);
            return table[column * 7 + (int)color];
        }

        // Picks a color for a particle spawned at this column, weighted by the heatmap. Uniform while not Ready.
        public CosmicColor Pick(int tileX, int worldTilesX, float floor, float sigmaFraction, Random rng)
        {
            if (!Ready)
                return (CosmicColor)rng.Next(7);

            EnsureTable(worldTilesX, floor, sigmaFraction);
            int step = Math.Max(1, TableStep);
            int column = Math.Clamp(tileX / step, 0, table.Length / 7 - 1);

            float total = 0f;
            for (int k = 0; k < 7; k++)
                total += table[column * 7 + k];

            float roll = (float)rng.NextDouble() * total;
            for (int k = 0; k < 7; k++)
            {
                roll -= table[column * 7 + k];
                if (roll <= 0f)
                    return (CosmicColor)k;
            }

            return CosmicColor.Red;
        }

        // Wire and save format: version byte, count byte (0 = not ready, otherwise 7), then the 7 order bytes.
        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)FormatVersion);
            writer.Write((byte)(Ready ? 7 : 0));

            if (Ready)
            {
                for (int i = 0; i < 7; i++)
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

            if (count != 7)
                return false;

            byte[] order = new byte[7];
            for (int i = 0; i < 7; i++)
                order[i] = reader.ReadByte();

            return SetOrder(order);
        }
    }
}