using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.ChestLoot
{
    public class ChestLootSystem : ModSystem
    {
        public override void PostSetupContent()
        {
            // Item/tile types are valid here, so definitions can use ModContent.ItemType<T>().
            ChestLootRegistry.Clear();
            ChestLootDefinitions.Register();
        }

        public override void PostWorldGen()
        {
            // genRand keeps results seed-deterministic.
            ChestLootRegistry.ApplyAll(WorldGen.genRand, msg => Mod.Logger.Info(msg));
        }

        public override void Unload()
        {
            ChestLootRegistry.Clear();
        }
    }
}