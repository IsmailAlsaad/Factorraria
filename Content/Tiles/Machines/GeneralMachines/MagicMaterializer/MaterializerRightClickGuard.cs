using Factorraria.Common.Machines;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    // Vanilla toggles a monolith on right-click inside Player.TileInteractionsUse (Wiring.HitSwitch plus NetMessage 59 to the server), and
    // GlobalTile.RightClick cannot cancel that. Since patch 6.4 (finding U9) the vanilla interaction is not undone any more, it never runs:
    // for the Materializer's tile this hook skips orig. Nothing is flipped, so nothing can reach the server or other clients before a
    // restore (verification item 8). The tile's own right-click (the machine's OnRightClick, which sends the toggle request in multiplayer)
    // is still called here, the same TileLoader.RightClick call vanilla makes at the end of TileInteractionsUse.
    // The tile is recognised by type alone, like MaterializerWireGuard, so a client that has no replica yet cannot flip a monolith either.
    // The server's MagicMaterializerTileEntity.Update (SyncMonolithFrames) still forces the frames every tick, which also covers a modified
    // client that sends message 59 itself.
    public class MaterializerRightClickGuard : ModSystem
    {
        public override void Load()
        {
            On_Player.TileInteractionsUse += BlockVanillaMonolith;
        }

        public override void Unload()
        {
            On_Player.TileInteractionsUse -= BlockVanillaMonolith;
        }

        static void BlockVanillaMonolith(On_Player.orig_TileInteractionsUse orig, Player self, int myX, int myY)
        {
            bool isMaterializer = WorldGen.InWorld(myX, myY, 2)
                && Main.tile[myX, myY].HasTile
                && Main.tile[myX, myY].TileType == TileID.ShimmerMonolith
                && MachineVisualRegistry.Definitions.ContainsKey(TileID.ShimmerMonolith);

            if (!isMaterializer)
            {
                orig(self, myX, myY);
                return;
            }

            TileLoader.RightClick(myX, myY);        // MachineVisualOverride.RightClick: the Materializer's own handling
            self.tileInteractionHappened = true;    // as vanilla's monolith branch did, so the click counts as used
        }
    }
}