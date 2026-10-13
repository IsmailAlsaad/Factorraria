using Factorraria.Common.Machines;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    // Vanilla toggles a monolith on right-click before any mod hook runs, and GlobalTile.RightClick cannot cancel that.
    // For the Materializer's tile we skip the vanilla handler and run only the mod right-click hooks (which open the UI).
    public class MaterializerRightClickGuard : ModSystem
    {
        public override void Load()
        {
            On_Player.TileInteractionsUse += SkipVanillaMonolithToggle;
        }

        public override void Unload()
        {
            On_Player.TileInteractionsUse -= SkipVanillaMonolithToggle;
        }

        static void SkipVanillaMonolithToggle(On_Player.orig_TileInteractionsUse orig, Player self, int myX, int myY)
        {
            if (WorldGen.InWorld(myX, myY, 2))
            {
                Tile tile = Main.tile[myX, myY];
                if (tile.HasTile && tile.TileType == TileID.ShimmerMonolith && MachineVisualRegistry.Definitions.ContainsKey(tile.TileType))
                {
                    TileLoader.RightClick(myX, myY);
                    self.tileInteractionHappened = true;   // like vanilla: the click was consumed by the tile
                    return;
                }
            }

            orig(self, myX, myY);
        }
    }
}