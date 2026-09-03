using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Content.Items.Liquids.Motors;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Liquids
{
    // Every motor TIER (MK1, future MK2, etc) inherits this, parameterized by its
    // own MotorTileEntity subtype. Handles placement-hook wiring and telling the
    // liquid network a topology-relevant tile appeared/disappeared — mirrors
    // PipeTileBase's role, just without the auto-shaping (motors don't reframe
    // based on neighbors, they're a fixed sprite rotated via right-click instead).
    public abstract class MotorTileBase<TEntity> : ModTile where TEntity : MotorTileEntityBase, new()
    {
        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileLighted[Type] = true;
            Main.tileSolid[Type] = true;
            Main.tileNoAttach[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<TEntity>().Hook_AfterPlacement, -1, 0, true);
            TileObjectData.newTile.AnchorInvalidTiles = new int[] { };

            // Prevent DivideByZeroException when external mods query tile style
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.StyleWrapLimit = 1;

            // Clear default floor requirement
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;

            // MUST be called after setting AnchorBottom
            TileObjectData.addTile(Type);
        }

        public override bool CanPlace(int i, int j)
        {
            // 1. Valid if background wall exists
            if (Main.tile[i, j].WallType > WallID.None)
                return true;

            // 2. Valid if attached to ANY tile (solid blocks, pipes, or motors)
            if (Main.tile[i - 1, j].HasTile ||
                Main.tile[i + 1, j].HasTile ||
                Main.tile[i, j - 1].HasTile ||
                Main.tile[i, j + 1].HasTile)
            {
                return true;
            }

            return false; // Prevents floating placement in mid-air
        }

        // NEW: this is the piece that was missing. A motor is a bridge point in the
        // liquid network's topology (unlike other machines, which are dead ends), so
        // placing/breaking one needs to invalidate the network the same way a pipe does.
        public override void PlaceInWorld(int i, int j, Item item)
        {
            LiquidNetworkSystem.networkNeedsRebuilding = true;
        }

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            LiquidNetworkSystem.networkNeedsRebuilding = true;
        }
    }
}