using Factorraria.Common.Net;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Story
{
    /// <summary>
    /// A player can only ever hold one Mechanic's Bag. If the inventory (or the cursor) ends up with more than one, the extra copies are
    /// deleted without a message or sound; the first one found in the inventory stays. Runs on the owning client only (single-player
    /// included) a few times a second, and syncs each emptied slot so the server's copy of the inventory agrees.
    /// </summary>
    public class MechanicsBagPlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            int checkEveryTicks = 10;
            if (!FactorrariaNet.IsOwner(Player) || Main.GameUpdateCount % checkEveryTicks != 0)
                return;

            int bag = ModContent.ItemType<MechanicsBagItem>();
            bool kept = false;

            for (int i = 0; i < Player.inventory.Length; i++)
            {
                Item item = Player.inventory[i];
                if (item == null || item.IsAir || item.type != bag)
                    continue;

                if (!kept)
                {
                    kept = true;
                    continue;
                }

                item.TurnToAir();
                FactorrariaNet.SyncInventoryItem(Player, item);
            }

            Item cursor = Main.mouseItem;
            if (kept && cursor != null && !cursor.IsAir && cursor.type == bag)
                cursor.TurnToAir();
        }
    }
}