using Factorraria.Common.Net;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.BeeWheel
{
    // Loads the wheel and bee textures and registers the "a tile was placed" consumer (TilePlaced.Clear runs in TilePlacedSystem.Unload).
    public class BeeWheelSystem : ModSystem
    {
        public override void Load()
        {
            TilePlaced.Register(new BeeWheelPlacedConsumer());
        }

        public override void PostSetupContent()
        {
            if (Main.dedServ)
                return;

            BeeWheelVisual.LoadAssets();
        }

        public override void Unload()
        {
            BeeWheelVisual.UnloadAssets();
        }
    }
}