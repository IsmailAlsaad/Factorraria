using Factorraria.Content.Items.Mounts;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Content.Buffs
{
    // The standard mount buff: vanilla removes it on dismount, and it keeps the mount on while it is there.
    // Art: make Content/Items/Mounts/MechSuitBuff.png (32x32). Until it exists the item's own art is used as the icon.
    public class MechSuitBuff : ModBuff
    {
        public override string Texture => "Factorraria/Content/Buffs/MechSuitBuff";

        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.mount.SetMount(ModContent.MountType<MechSuitMount>(), player);
            player.buffTime[buffIndex] = 10;
        }
    }
}