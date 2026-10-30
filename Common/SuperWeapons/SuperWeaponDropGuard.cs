using Terraria.ModLoader;

namespace Factorraria.Common.SuperWeapons
{
    // Super weapons patch S4.2: while a launch removes the weapon's tiles (SuperWeaponSystem.SuppressPartDrops) the three part tiles drop
    // nothing, so a launched weapon does not leave its items on the ground. Any other tile is untouched.
    public class SuperWeaponDropGuard : GlobalTile
    {
        public override bool CanDrop(int i, int j, int type)
        {
            if (!SuperWeaponSystem.IsPartTile(type))
                return true;

            SuperWeaponSystem system = ModContent.GetInstance<SuperWeaponSystem>();
            return system == null || !system.SuppressPartDrops;
        }
    }
}