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

        // Seamless launch: while the launch removes the parts there is no break dust and no break sound (the launch effect has its own).
        // SuperWeaponSystem.IsLaunchBreak says when; mining a part by hand still gives the normal dust and sound.
        public override bool CreateDust(int i, int j, int type, ref int dustType)
        {
            return !(SuperWeaponSystem.IsPartTile(type) && SuperWeaponSystem.IsLaunchBreak(i, j));
        }

        public override void NumDust(int i, int j, int type, bool fail, ref int num)
        {
            if (SuperWeaponSystem.IsPartTile(type) && SuperWeaponSystem.IsLaunchBreak(i, j))
                num = 0;
        }

        public override bool KillSound(int i, int j, int type, bool fail)
        {
            return !(SuperWeaponSystem.IsPartTile(type) && SuperWeaponSystem.IsLaunchBreak(i, j));
        }
    }
}