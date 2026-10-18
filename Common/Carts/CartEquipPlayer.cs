using Factorraria.Common.Net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Minecart items stack to 99, but the minecart equip slot (the 3rd misc slot, after pet and light pet) holds a single cart.
    /// Whatever a click or shift-click leaves above 1 in that slot goes back to the inventory (or drops if it is full).
    /// </summary>
    public class CartEquipPlayer : ModPlayer
    {
        int CartMiscSlot = 2; // miscEquips: pet, light pet, minecart, mount, grapple

        // Network slot number vanilla's SyncEquipment message uses for a miscEquips entry: inventory (0-57) plus the mouse item (58)
        // come first, then armor, then armor dyes, then miscEquips. Computed from the real array lengths instead of a hard-coded number.
        int MiscEquipNetSlot(int index) => 58 + Player.armor.Length + Player.dye.Length + index;

        public override void PostUpdate()
        {
            // PostUpdate runs for every player on every machine. Only the owner trims its own slot: on the server and on other clients this
            // edited a replica inventory and spawned the excess into the world as well, so it came out twice. The owner's result reaches
            // everyone else through the SyncEquipment message below.
            if (!FactorrariaNet.IsOwner(Player)) return;

            Item equipped = Player.miscEquips[CartMiscSlot];

            if (equipped == null || equipped.IsAir || equipped.stack <= 1)
            {
                return;
            }

            Item excess = equipped.Clone();
            excess.stack = equipped.stack - 1;
            equipped.stack = 1;

            Item leftover = Player.GetItem(Player.whoAmI, excess, GetItemSettings.InventoryEntityToPlayerInventorySettings);
            if (leftover != null && !leftover.IsAir)
            {
                Player.QuickSpawnItem(Player.GetSource_Misc("FactorrariaCartEquipExcess"), leftover, leftover.stack);
            }

            // Tell the server (which relays to the other clients) that the cart slot is now a single cart. Single-player has nobody to tell.
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, Player.whoAmI, MiscEquipNetSlot(CartMiscSlot), equipped.prefix);
            }
        }
    }
}