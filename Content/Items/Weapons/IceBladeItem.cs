using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Weapons
{
    public class IceBladeItem : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.IceBlade;
        }
        public override void SetDefaults(Item entity)
        {
            base.SetDefaults(entity);

            entity.useTurn = true;
            entity.useStyle = ItemUseStyleID.Shoot;
            entity.noMelee = true;
            entity.noUseGraphic = true;
            entity.shoot = ModContent.ProjectileType<Projectiles.Weapons.IceBladeSwingProjectile>();
        }

        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

            return false;
        }
    }
}
