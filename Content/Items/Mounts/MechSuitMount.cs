using Factorraria.Common.Net;
using Factorraria.Content.Buffs;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Mounts
{
    // The Mech Suit as a vanilla mount, so the item fits the mount slot and the Mount key works. The movement itself is still MechSuitPlayer:
    // with no arm latched the player moves like normal (the numbers below copy the plain player), with an arm latched the suit takes over.
    // Art is optional: MechSuitMount_Back.png / _Front.png (any size) would be drawn by vanilla if they exist; today the suit draws nothing extra.
    public class MechSuitMount : ModMount
    {
        public override void SetStaticDefaults()
        {
            MountData.buff = ModContent.BuffType<MechSuitBuff>();
            MountData.spawnDust = 0;

            // The plain player's numbers, so mounting changes nothing while no arm is latched.
            MountData.jumpHeight = 15;
            MountData.jumpSpeed = 5.01f;
            MountData.acceleration = 0.08f;
            MountData.runSpeed = 3f;
            MountData.dashSpeed = 3f;
            MountData.fallDamage = 1f;
            MountData.heightBoost = 0;
            MountData.flightTimeMax = 0;
            MountData.fatigueMax = 0;
            MountData.blockExtraJumps = false;
            MountData.constantJump = false;

            // One frame for every state, no offsets, upright player.
            MountData.totalFrames = 1;
            MountData.playerYOffsets = new int[] { 0 };
            MountData.xOffset = 0;
            MountData.yOffset = 0;
            MountData.playerHeadOffset = 0;
            MountData.bodyFrame = 0;
            MountData.standingFrameCount = 1;
            MountData.standingFrameDelay = 12;
            MountData.standingFrameStart = 0;
            MountData.runningFrameCount = 1;
            MountData.runningFrameDelay = 12;
            MountData.runningFrameStart = 0;
            MountData.flyingFrameCount = 0;
            MountData.flyingFrameDelay = 0;
            MountData.flyingFrameStart = 0;
            MountData.inAirFrameCount = 1;
            MountData.inAirFrameDelay = 12;
            MountData.inAirFrameStart = 0;
            MountData.idleFrameCount = 1;
            MountData.idleFrameDelay = 12;
            MountData.idleFrameStart = 0;
            MountData.idleFrameLoop = true;
            MountData.swimFrameCount = 1;
            MountData.swimFrameDelay = 12;
            MountData.swimFrameStart = 0;
        }

        // Runs on every machine; only the owner starts the suit (everyone else gets the flag from MechSuitNet).
        public override void SetMount(Player player, ref bool skipDust)
        {
            skipDust = true;

            if (!FactorrariaNet.IsOwner(player))
                return;

            MechSuitPlayer mech = player.GetModPlayer<MechSuitPlayer>();
            if (!mech.IsActive)
                mech.Activate();
        }

        // Mount key, clicking the item, another mount, death. The owner's suit goes off with it.
        public override void Dismount(Player player, ref bool skipDust)
        {
            skipDust = true;

            if (!FactorrariaNet.IsOwner(player))
                return;

            MechSuitPlayer mech = player.GetModPlayer<MechSuitPlayer>();
            if (mech.IsActive)
                mech.DeactivateCore();
        }
    }
}