using Factorraria.Content.Projectiles.Weapons.IceBoomerang;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Weapons.IceBoomerang
{
    /// <summary>
    /// Overhauls the vanilla Ice Boomerang: it throws IceBoomerangProjectile at the cursor, only one can be in the air, and it
    /// turns into an ice crystal instead of returning. Recipe, drops and stats stay vanilla.
    /// </summary>
    public class IceBoomerangGlobalItem : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.IceBoomerang;
        }

        public override void SetDefaults(Item item)
        {
            item.shoot = ModContent.ProjectileType<IceBoomerangProjectile>();
            item.shootSpeed = IceBoomerangTuning.ThrowSpeed;
            item.autoReuse = true;
        }

        // Only one boomerang in flight: it counts as in flight until it has become a crystal.
        public override bool CanUseItem(Item item, Player player)
        {
            return player.ownedProjectileCounts[ModContent.ProjectileType<IceBoomerangProjectile>()] <= 0;
        }

        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Shoot only runs on the throwing player's own client. The throw range (ai[1]) is the distance to the cursor.
            float range = MathHelper.Clamp(Vector2.Distance(position, Main.MouseWorld), IceBoomerangTuning.MinThrowRange, IceBoomerangTuning.MaxThrowRange);
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 0f, range);
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            //tooltips.Add(new TooltipLine(Mod, "FactorrariaIceBoomerang1", "Thrown at your cursor, it does not come back"));
            //tooltips.Add(new TooltipLine(Mod, "FactorrariaIceBoomerang2", "It stops at the cursor, on a wall or on the first enemy it hits, and becomes an ice crystal"));
            //tooltips.Add(new TooltipLine(Mod, "FactorrariaIceBoomerang3", "Up to 5 crystals last 30 seconds; the oldest breaks first"));
            //tooltips.Add(new TooltipLine(Mod, "FactorrariaIceBoomerang4", "Enemies touching a crystal are chilled"));
        }
    }
}