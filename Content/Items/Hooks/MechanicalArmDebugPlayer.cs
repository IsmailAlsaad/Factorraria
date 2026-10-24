using Factorraria.Content.Projectiles.Hooks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Hooks
{
    // TEMPORARY diagnostic. On every Grapple keypress it prints (chat) what the hook slot holds and what vanilla's hook launch reads from
    // it, then three ticks later how many Mechanical Arm projectiles exist. Delete this file once the hook-slot launch works.
    public class MechanicalArmDebugPlayer : ModPlayer
    {
        public bool Enabled = true;

        int checkIn;
        int armsBefore;

        int CountArms(out string states)
        {
            int type = ModContent.ProjectileType<MechanicalArmProjectile>();
            int count = 0;
            states = "";
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Player.whoAmI && p.type == type)
                {
                    count++;
                    states += " ai0=" + p.ai[0];
                }
            }
            return count;
        }

        static void Say(string text)
        {
            Main.NewText("[ArmDebug] " + text, Color.Orange);
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!Enabled || Player.whoAmI != Main.myPlayer || !triggersSet.JustPressed.Grapple)
                return;

            Item slot = Player.miscEquips[4];
            int projType = ModContent.ProjectileType<MechanicalArmProjectile>();
            int arms = CountArms(out string states);
            bool hookFlag = slot.shoot >= 0 && slot.shoot < Main.projHook.Length && Main.projHook[slot.shoot];

            Say("Grapple pressed. slot=" + (slot.IsAir ? "(empty)" : slot.Name + " x" + slot.stack)
                + " isArm=" + (slot.type == ModContent.ItemType<MechanicalArmItem>())
                + " shoot=" + slot.shoot + " (arm projectile is " + projType + ") projHook[shoot]=" + hookFlag
                + " projHook[arm]=" + Main.projHook[projType]
                + " armsOut=" + arms + states
                + " held=" + Player.HeldItem.Name
                + " noItems=" + Player.noItems + " frozen=" + Player.frozen + " mount=" + Player.mount.Active
                + " controlHook=" + Player.controlHook + " releaseHook=" + Player.releaseHook);

            armsBefore = arms;
            checkIn = 3;
        }

        public override void PostUpdate()
        {
            if (checkIn <= 0 || Player.whoAmI != Main.myPlayer)
                return;

            if (--checkIn == 0)
            {
                int now = CountArms(out string states);
                Say("3 ticks later: arms=" + now + states + " (was " + armsBefore + "). "
                    + (now > armsBefore ? "an arm was launched." : "no arm was launched by anything."));
            }
        }
    }
}