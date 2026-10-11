using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Minecart items stack to 99, but the minecart equip slot (the 3rd misc slot, after pet and light pet) holds a single cart.
    /// Whatever a click or shift-click leaves above 1 in that slot goes back to the inventory (or drops if it is full).
    /// </summary>
    public class CartEquipPlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            Item equipped = Player.miscEquips[2];

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
        }
    }
}