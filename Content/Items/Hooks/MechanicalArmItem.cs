using Factorraria.Content.Projectiles.Hooks;
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
            Item.maxStack = 1;
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