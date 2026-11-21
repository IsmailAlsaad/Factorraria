using Factorraria.Common.Harvester;
using Microsoft.Xna.Framework;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Harvester Box acorns (Phase 5). Left click with an acorn on the tile above a tree box plants a sapling, like an acorn on a grass block.
    //   single player      -> SendRequest runs the handler right here
    //   multiplayer client -> HarvesterBoxPlantAcorn (payload: tile x, y of the box) to the server
    //   server             -> validates (reach, the acorn really is in the sender's held slot, tree box, free tiles above), writes the sapling,
    //                         takes one acorn and syncs that slot; the tiles reach clients through the vanilla tile square.
    // The client never changes a tile or an item itself, so a refused request changes nothing. Vanilla's own placement of the acorn simply fails on a box
    // (its soil list does not know the box), so the two never both succeed.
    public class HarvesterBoxTreeNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.HarvesterBoxPlantAcorn, PacketDirection.ToServer, HandlePlant, 2);
        }

        // Called when the local player uses an acorn (main thread): looks at the tile under the cursor.
        public static void TryPlantAcornAtCursor(Player player)
        {
            Point target = Main.MouseWorld.ToTileCoordinates();
            int boxX = target.X;
            int boxY = target.Y + 1;   // the sapling stands on the tile above the box

            if (!HarvesterBoxTrees.CanPlantSapling(boxX, boxY - 1) || !FactorrariaNet.InReach(player, boxX, boxY))
                return;

            // Vanilla's own placement fails on a box, so nothing played a sound: same sound vanilla plays when an acorn goes into grass.
            // Only the planting player hears it (the sapling itself reaches everyone through the tile square).
            SoundEngine.PlaySound(SoundID.Dig, new Vector2(boxX * 16f + 8f, (boxY - 1) * 16f + 8f));

            FactorrariaNet.SendRequest(FactorrariaPacket.HarvesterBoxPlantAcorn, writer =>
            {
                writer.Write(boxX);
                writer.Write(boxY);
            });
        }

        static void HandlePlant(BinaryReader reader, int sender)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            if (!WorldGen.InWorld(x, y, 4) || sender < 0 || sender >= Main.maxPlayers)
                return;

            Player player = Main.player[sender];
            if (!FactorrariaNet.InReach(player, x, y))
                return;

            Item held = player.inventory[player.selectedItem];
            if (held.type != ItemID.Acorn || held.stack <= 0)
                return;

            if (!HarvesterBoxTrees.CanPlantSapling(x, y - 1))
                return;

            int style = HarvesterBoxKinds.SaplingStyle(HarvesterBoxRegistry.BoxTreeSoil[Main.tile[x, y].TileType]);
            HarvesterBoxTrees.PlaceSapling(x, y - 1, style);

            held.stack--;
            if (held.stack <= 0)
                held.TurnToAir();

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, player.selectedItem, held.prefix);
        }
    }

    // Using an acorn (left click) asks the server to plant it when the cursor is on the tile above a tree box. Vanilla's own placement fails there, so nothing doubles.
    public class HarvesterBoxAcornItem : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.Acorn;

        public override bool? UseItem(Item item, Player player)
        {
            if (player.whoAmI == Main.myPlayer && HarvesterBoxRegistry.BoxTreeSoil.Count > 0)
                HarvesterBoxTreeNetSystem.TryPlantAcornAtCursor(player);

            return null;
        }
    }
}