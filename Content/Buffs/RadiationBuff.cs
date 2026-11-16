using Factorraria.Common.Cosmic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Buffs
{
    // Cosmic radiation debuff. It only marks "you are irradiated" and carries the time left; the radiation LEVEL, the life loss and the
    // Geiger clicks live in CosmicRadiationPlayer (local to the owning client). Each cosmic particle that gets close to the player adds
    // seconds to this buff equal to its multiplier. When the buff runs out the level goes back to 0.
    // Art: make Content/Buffs/RadiationBuff.png (32x32). Until it exists the vanilla Poisoned icon is used.
    public class RadiationBuff : ModBuff
    {
        public override string Texture => ModContent.HasAsset("Factorraria/Content/Buffs/RadiationBuff")
            ? "Factorraria/Content/Buffs/RadiationBuff"
            : "Terraria/Images/Buff_" + BuffID.Poisoned;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        // The tooltip shows the local player's current level (the level is never synced, so this is only right for yourself).
        public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
        {
            if (Main.LocalPlayer != null)
                tip += "\nRadiation level: " + Main.LocalPlayer.GetModPlayer<CosmicRadiationPlayer>().Level.ToString("0.0");
        }
    }
}