using Factorraria.Common.Machines;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    // Vanilla toggles a monolith on right-click before any mod hook runs, and GlobalTile.RightClick cannot cancel that.
    // Vanilla still runs untouched (its own click gating, and our UI opening through TileLoader.RightClick); afterwards
    // the Materializer's frames are put back to what they were, so the monolith never flips.
    public class MaterializerRightClickGuard : ModSystem
    {
        public override void Load()
        {
            On_Player.TileInteractionsUse += KeepMonolithFrames;
        }

        public override void Unload()
        {
            On_Player.TileInteractionsUse -= KeepMonolithFrames;
        }

        static void KeepMonolithFrames(On_Player.orig_TileInteractionsUse orig, Player self, int myX, int myY)
        {
            TileObjectData data = TileObjectData.GetTileData(TileID.ShimmerMonolith, 0);
            Point16 corner = default;

            bool isMaterializer = data != null
                && WorldGen.InWorld(myX, myY, 2)
                && Main.tile[myX, myY].HasTile
                && Main.tile[myX, myY].TileType == TileID.ShimmerMonolith
                && MachineVisualRegistry.Definitions.ContainsKey(TileID.ShimmerMonolith)
                && TileEntityHelper.TryGetEntityFromTile(myX, myY, out _, out corner);

            if (!isMaterializer)
            {
                orig(self, myX, myY);
                return;
            }

            short[] frames = new short[data.Width * data.Height];
            for (int x = 0; x < data.Width; x++)
                for (int y = 0; y < data.Height; y++)
                    frames[x * data.Height + y] = Framing.GetTileSafely(corner.X + x, corner.Y + y).TileFrameX;

            orig(self, myX, myY);

            for (int x = 0; x < data.Width; x++)
            {
                for (int y = 0; y < data.Height; y++)
                {
                    Tile tile = Framing.GetTileSafely(corner.X + x, corner.Y + y);
                    if (tile.HasTile && tile.TileType == TileID.ShimmerMonolith)
                        tile.TileFrameX = frames[x * data.Height + y];
                }
            }
        }
    }
}