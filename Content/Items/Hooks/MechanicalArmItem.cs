using Factorraria.Content.Projectiles.Hooks;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Hooks
{
    public class MechanicalArmItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 99;
            Item.value = Item.buyPrice(gold: 2);
            Item.rare = ItemRarityID.LightRed;

            Item.noUseGraphic = true; // hides the item sprite during use — this removes the "swing" look, not useStyle
            Item.noMelee = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = false;

            Item.createTile = ModContent.TileType<RoboticArmTile>();
            Item.consumable = true;

            // Hook data: only used when the item sits in the hook slot (QuickGrapple reads shoot / shootSpeed from it).
            Item.shoot = ModContent.ProjectileType<MechanicalArmProjectile>();
            Item.shootSpeed = 11f;
        }

        // Left click only places the Robotic Arm machine. The grappling hook use comes from the hook slot (Grapple keybind), see MechanicalArmPlayer.
        // Main.projHook[Item.shoot] is what the hook slot checks, so Item.shoot stays the arm projectile whenever the item is idle.
        // It is switched off only while a click-use is running (belt and braces so vanilla never treats a placement as a shot),
        // and restored as soon as the use animation is over or the item is no longer in hand.
        static int HookProjectile => ModContent.ProjectileType<MechanicalArmProjectile>();

        public static void ResetToHookMode(Item item)
        {
            if (item != null && item.ModItem is MechanicalArmItem)
                item.shoot = HookProjectile;
        }

        public override bool CanShoot(Player player) => false;

        public override bool CanUseItem(Player player)
        {
            Item.shoot = ProjectileID.None;
            return true;
        }

        public override void HoldItem(Player player)
        {
            if (player.itemAnimation <= 0)
                Item.shoot = HookProjectile;
        }

        public override void UpdateInventory(Player player)
        {
            if (player.HeldItem != Item || player.itemAnimation <= 0)
                Item.shoot = HookProjectile;
        }
    }
}