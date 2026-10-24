using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Hooks
{
    // Keeps the hook slot (miscEquips[4]) at exactly one Mechanical Arm. The item is stackable, but vanilla moves the whole stack into the
    // slot on quick-equip / swap; the surplus goes back to the inventory (dropped if full). Owner only: the slot is only read locally
    // (QuickGrapple), and the projectile itself is synced like any other, so no new packet is needed.
    public class MechanicalArmPlayer : ModPlayer
    {
        const int HookSlot = 4;

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            MechanicalArmItem.ResetToHookMode(Main.mouseItem);

            Item hook = Player.miscEquips[HookSlot];
            int type = ModContent.ItemType<MechanicalArmItem>();
            if (hook.type != type)
                return;

            MechanicalArmItem.ResetToHookMode(hook);

            if (hook.stack <= 1)
                return;

            Item surplus = new Item();
            surplus.SetDefaults(type);
            surplus.stack = hook.stack - 1;
            hook.stack = 1;

            Item leftover = Player.GetItem(Player.whoAmI, surplus, new GetItemSettings(NoText: true));
            if (!leftover.IsAir)
                Player.QuickSpawnItem(Player.GetSource_Misc("FactorrariaMechanicalArm"), leftover, leftover.stack);

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                int netSlot = Player.inventory.Length + Player.armor.Length + Player.dye.Length + HookSlot;
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, Player.whoAmI, netSlot, hook.prefix);
            }
        }
    }
}