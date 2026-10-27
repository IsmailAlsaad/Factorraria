using Factorraria.Common.Net;
using Factorraria.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.BeeWheel
{
    // Tells every Bee Wheel in range that the world changed, so it rescans soon.
    //  - Placement on the server / single-player: BeeWheelPlacedConsumer (the shared TilePlaced route).
    //  - Placement on the placing client and every break (server, single-player, clients that run KillTile): this GlobalTile.
    //  - Changes that skip both (other players' placements as seen by a client, explosions, tile sections): the wheel's own
    //    periodic safety rescan (BeeWheelTileEntity.SafetyRescanTicks) catches them.
    public class BeeWheelTileHooks : GlobalTile
    {
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!fail && !effectOnly)
                BeeWheelTileEntity.NotifyTileChanged(i, j);
        }

        public override void PlaceInWorld(int i, int j, int type, Item item)
        {
            BeeWheelTileEntity.NotifyTileChanged(i, j);
        }
    }

    public sealed class BeeWheelPlacedConsumer : ITilePlacedConsumer
    {
        public bool AppliesTo(int tileType) => true;

        // Idempotent: it only pulls a wheel's next scan time forward.
        public void OnTilePlaced(int x, int y, int tileType, int placer)
        {
            BeeWheelTileEntity.NotifyTileChanged(x, y);
        }
    }

    // Royal honey drop from queen bee larva
    public class LarvaDropGlobalTile : GlobalTile
    {
        public override void Drop(int i, int j, int type)
        {
            if (type != TileID.Larva) return;

            Item.NewItem(WorldGen.GetItemSource_FromTileBreak(i, j), i * 16, j * 16, 16, 16,
                ModContent.ItemType<RoyalHoneyItem>());
        }
    }
}