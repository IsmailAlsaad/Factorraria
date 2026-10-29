using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.SuperWeapons
{
    // Stone shield of the Wall of Flesh (super weapons plan, patch S1.1). Only the Wall of Flesh (the mouth) and its eyes get this
    // instance; the Hungry, Hungry II and the leeches never do, so they take damage as usual.
    //
    // AUTHORITY: the server (or single-player) owns ShieldUp. It is set in SetDefaults (so the very first NPC sync of a new wall already
    // carries it) and only BreakShield / RaiseShield change it afterwards. SYNC: ShieldUp rides the NPC's own sync (SendExtraAI /
    // ReceiveExtraAI), no packet of ours; BreakShield and RaiseShield send the NPC right away. Not saved (NPCs are not saved): every new
    // wall starts shielded.
    //
    // DAMAGE BLOCK (verified in the tModLoader source, V2): ModifyIncomingHit only runs where the hit is calculated, and the server applies
    // a client's already calculated hit (MessageID.StrikeNPC) without it, and a damage-over-time tick or a direct life change never goes
    // through it. So the block has four layers: ModifyIncomingHit zeroes the damage and knockback (and stops the combat text); UpdateLifeRegen
    // zeroes damage over time; CheckDead refuses death; and PostAI puts the life back to what it was whenever it dropped while the shield
    // is up (every side, so a client does not even flicker). The last two are the leak-proof net.
    public class WallShieldGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return IsWallType(entity.type);
        }

        /// <summary>True while the stone shield is up. Server / single-player writes it, clients copy it from the NPC sync.</summary>
        public bool ShieldUp;

        // The life the mouth had last tick while shielded; -1 = not tracking. Only the mouth (realLife < 0) is tracked: the eyes mirror its life.
        int lockedLife = -1;

        public static bool IsWallType(int type)
        {
            return type == NPCID.WallofFlesh || type == NPCID.WallofFleshEye;
        }

        /// <summary>The mouth and the eyes that carry a shield (the Hungry and the leeches are not on this list).</summary>
        public static bool IsShielded(NPC npc)
        {
            return npc.active && npc.TryGetGlobalNPC(out WallShieldGlobalNPC shield) && shield.ShieldUp;
        }

        /// <summary>Server / single-player only. Drops the shield of the live wall (mouth and every eye) for good. Returns how many NPCs changed, 0 on a multiplayer client.</summary>
        public static int BreakShield()
        {
            return SetShield(false);
        }

        /// <summary>Server / single-player only (debug). Puts the shield back on the live wall. Returns how many NPCs changed.</summary>
        public static int RaiseShield()
        {
            return SetShield(true);
        }

        static int SetShield(bool up)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return 0;

            int changed = 0;
            Vector2 effectAt = Vector2.Zero;
            bool haveEffectAt = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.TryGetGlobalNPC(out WallShieldGlobalNPC shield) || shield.ShieldUp == up)
                    continue;

                shield.ShieldUp = up;
                shield.lockedLife = -1;
                npc.netUpdate = true;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
                if (!haveEffectAt || npc.type == NPCID.WallofFlesh)
                {
                    effectAt = npc.Center;
                    haveEffectAt = true;
                }

                changed++;
            }

            if (!up && haveEffectAt)
                SuperWeaponNet.SendEffect(SuperWeaponEffectKind.ShieldBreak, effectAt);

            return changed;
        }

        public override void SetDefaults(NPC npc)
        {
            ShieldUp = true;
            lockedLife = -1;
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(ShieldUp);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            ShieldUp = bitReader.ReadBit();
        }

        // Layer 1: a hit calculated while the shield is up does nothing.
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (!ShieldUp)
                return;

            modifiers.FinalDamage *= 0f;
            modifiers.DisableKnockback();
        }

        // Layer 2: debuffs and other damage over time do nothing.
        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (!ShieldUp)
                return;

            if (npc.lifeRegen < 0)
                npc.lifeRegen = 0;
            if (npc.lifeRegenCount < 0)
                npc.lifeRegenCount = 0;
            damage = 0;
        }

        // Layer 3: nothing can kill a shielded wall; the life goes back to what it was.
        public override bool CheckDead(NPC npc)
        {
            if (!ShieldUp)
                return true;

            if (npc.life < 1)
                npc.life = lockedLife > 0 ? lockedLife : 1;
            npc.netUpdate = true;
            return false;
        }

        // Layer 4: whatever got through (a hit packet applied as is, a mod that edits life directly) is undone here. Follows healing upward.
        public override void PostAI(NPC npc)
        {
            if (!ShieldUp || npc.realLife >= 0)
            {
                lockedLife = -1;
                return;
            }

            if (lockedLife < 0 || npc.life > lockedLife)
            {
                lockedLife = npc.life;
                return;
            }

            if (npc.life < lockedLife)
            {
                npc.life = lockedLife;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    npc.netUpdate = true;
            }
        }
    }
}