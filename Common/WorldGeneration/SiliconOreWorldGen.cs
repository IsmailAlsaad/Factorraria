using System.Collections.Generic;
using Factorraria.Content.Tiles.Ores;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace Factorraria.Common.WorldGeneration
{
    // Generates Silicon Ore veins in the same way vanilla generates copper/iron/silver/gold:
    // random TileRunner blobs, count scaled by world size, inside a depth band.
    public class SiliconOreWorldGen : ModSystem
    {
        public static double DensityPerTile = 3.0E-05;   // veins per world tile. Small world is about 150 veins. Silver is about 2-3x this.
        public static int StrengthMin = 4;               // vein blob size (TileRunner strength)
        public static int StrengthMax = 8;
        public static int StepsMin = 3;                  // vein length
        public static int StepsMax = 9;
        public static int BottomMargin = 200;            // tiles kept clear above the world bottom (underworld)

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int shiniesIndex = tasks.FindIndex(t => t.Name.Equals("Shinies"));
            if (shiniesIndex == -1)
                return;

            tasks.Insert(shiniesIndex + 1, new PassLegacy("Factorraria Silicon Ore", GenerateSiliconOre));
        }

        private void GenerateSiliconOre(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Burying silicon ore";

            int type = ModContent.TileType<SiliconOreTile>();
            int veins = (int)(Main.maxTilesX * Main.maxTilesY * DensityPerTile);

            // Rock layer down to just above the underworld = the same underground/cavern band silver and tungsten use.
            int minY = (int)Main.rockLayer;
            int maxY = Main.maxTilesY - BottomMargin;

            for (int k = 0; k < veins; k++)
            {
                progress.Set(k / (float)veins);

                int x = WorldGen.genRand.Next(0, Main.maxTilesX);
                int y = WorldGen.genRand.Next(minY, maxY);

                WorldGen.TileRunner(x, y,
                    WorldGen.genRand.Next(StrengthMin, StrengthMax + 1),
                    WorldGen.genRand.Next(StepsMin, StepsMax + 1),
                    type);
            }
        }
    }
}