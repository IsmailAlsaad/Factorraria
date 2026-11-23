using Factorraria.Common.Harvester;
using Microsoft.Xna.Framework;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Seeds on Blinkroot boxes (docs/planter_boxes/ROADMAP.md, Phase 5c). Left click with Grass, Jungle Grass, Corrupt, Crimson, Hallowed or Mushroom Grass Seeds
    // on a Blinkroot box (planter or Harvester) turns the box into the seed's box, like a seed on dirt.
    //   single player      -> SendRequest runs the handler right here
    //   multiplayer client -> PlanterBoxSeed (payload: tile x, y of the box) to the server
    //   server             -> validates (reach, the seed really is in the sender's held slot, the box is a Blinkroot box), converts the box, takes one seed and
    //                         syncs that slot; the tile reaches clients through the tile square ConvertBox sends.
    // The client never changes a tile or an item itself, so a refused request changes nothing. Vanilla's own placement of the seed simply fails on a box (it
    // needs dirt or mud under the cursor), so the two never both succeed.
    public class PlanterBoxSeedNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.PlanterBoxSeed, PacketDirection.ToServer, HandleSeed, 2);
        }

        // Called when the local player uses a seed (main thread): looks at the tile under the cursor.
        public static void TrySeedAtCursor(Player player, int itemType)
        {
            HarvesterBoxKind? seed = PlanterBoxConversion.SeedKind(itemType);
            if (!seed.HasValue)
                return;

            Point target = Main.MouseWorld.ToTileCoordinates();
            if (!WorldGen.InWorld(target.X, target.Y, 4) || !FactorrariaNet.InReach(player, target.X, target.Y))
                return;

            if (!PlanterBoxConversion.CanConvertBySeed(target.X, target.Y, seed.Value))
                return;

            // Vanilla's own placement fails on a box, so nothing played a sound: the sound vanilla plays when a seed goes into dirt. Only the player hears it.
            SoundEngine.PlaySound(SoundID.Dig, new Vector2(target.X * 16f + 8f, target.Y * 16f + 8f));

            FactorrariaNet.SendRequest(FactorrariaPacket.PlanterBoxSeed, writer =>
            {
                writer.Write(target.X);
                writer.Write(target.Y);
            });
        }

        static void HandleSeed(BinaryReader reader, int sender)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            if (!WorldGen.InWorld(x, y, 4) || sender < 0 || sender >= Main.maxPlayers)
                return;

            Player player = Main.player[sender];
            if (!FactorrariaNet.InReach(player, x, y))
                return;

            Item held = player.inventory[player.selectedItem];
            HarvesterBoxKind? seed = PlanterBoxConversion.SeedKind(held.type);
            if (!seed.HasValue || held.stack <= 0)
                return;

            if (!PlanterBoxConversion.CanConvertBySeed(x, y, seed.Value) || !PlanterBoxConversion.ConvertBox(x, y, seed.Value))
                return;

            held.stack--;
            if (held.stack <= 0)
                held.TurnToAir();

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, player.selectedItem, held.prefix);
        }
    }

    // Using one of the six seeds (left click) asks the server to turn the Blinkroot box under the cursor into the seed's box.
    public class PlanterBoxSeedItem : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => PlanterBoxConversion.SeedKind(entity.type).HasValue;

        public override bool? UseItem(Item item, Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                PlanterBoxSeedNetSystem.TrySeedAtCursor(player, item.type);

            return null;
        }
    }
}