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

            Item.shoot = ModContent.ProjectileType<MechanicalArmProjectile>();
            Item.shootSpeed = 11f;
        }

        // One item, two uses: a normal click fires the hook, Shift + left click places the Robotic Arm machine.
        // Vanilla decides between shooting and placing from Item.shoot / Item.createTile, so those are switched here.
        // The mode is re-read whenever a use starts (CanUseItem) and while held (HoldItem, for the placement preview);
        // anywhere else in the inventory the item is always plain hook mode.
        // The inventory must be closed: Shift is also the quick-move key there, and placing mode swaps Item.shoot off,
        // which would make the item stop counting as a hook (hook slot / right-click equip check Main.projHook[Item.shoot]).
        bool PlacingMode(Player player) => player.whoAmI == Main.myPlayer && !Main.playerInventory && Main.keyState.PressingShift();

        // Arms that are not in hand (hook slot, cursor) never get UpdateInventory, so MechanicalArmPlayer resets them through this.
        public static void ResetToHookMode(Item item)
        {
            if (item != null && item.ModItem is MechanicalArmItem arm)
                arm.ApplyMode(false);
        }

        void ApplyMode(bool placing)
        {
            if (placing)
            {
                Item.createTile = ModContent.TileType<RoboticArmTile>();
                Item.consumable = true;
                Item.shoot = ProjectileID.None;
            }
            else
            {
                Item.createTile = -1;
                Item.consumable = false;
                Item.shoot = ModContent.ProjectileType<MechanicalArmProjectile>();
            }
        }

        public override bool CanUseItem(Player player)
        {
            ApplyMode(PlacingMode(player));
            return true;
        }

        public override void HoldItem(Player player)
        {
            ApplyMode(PlacingMode(player));
        }

        public override void UpdateInventory(Player player)
        {
            if (player.HeldItem != Item)
                ApplyMode(false);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Only one arm out at a time — pressing the button again while one's already
            // out retracts the old one instead of stacking a second on top of it.
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile existing = Main.projectile[i];
                if (existing.active && existing.owner == player.whoAmI && existing.type == type)
                {
                    existing.Kill();
                }
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}