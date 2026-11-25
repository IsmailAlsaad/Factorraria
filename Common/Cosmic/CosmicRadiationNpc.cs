using Factorraria.Common.Net;
using Factorraria.Content.Buffs;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // C12: cosmic radiation on NPCs and enemies (roadmap: docs/cosmic_radiation/ROADMAP.md, C12). Same style as the player (CosmicRadiationPlayer):
    // each hit adds the particle's multiplier to a LEVEL and that many seconds to the RadiationBuff; higher level = faster life loss; the level
    // goes back to 0 when the buff ends. Lead armor does not matter here.
    //
    // AUTHORITY: the server (or single-player) owns the NPC, so the level, the buff and the life drain live there only. Particles exist only on
    // clients (the server holds rays as events), so the client that sees a particle pass an NPC counts the hit (CosmicParticleSystem.CheckNpcRadiation)
    // and reports it in the batched packet CosmicNpcRadiation (client -> server). To avoid every client reporting the same real particle, an NPC is
    // only counted by the client whose player is the nearest active player to it. In single-player the same code calls the handler directly.
    // The server clamps each reported value, checks the NPC and that the reporter is near it, then calls RegisterHit. Vanilla syncs the buff
    // (AddBuff on the server sends the NPC buff packet) and the NPC's life; the level itself is never sent (clients never need it).
    public class CosmicRadiationNpc : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public float Level;          // server / single-player only, never saved, never sent
        public uint LastBuffSync;    // server only: when the buff time was last re-sent to clients

        // ---------------------------------------------------------------- the wire (client -> server)
        // byte count, then per NPC: short npc index, float multiplier summed since the last report.

        public static void WriteReport(BinaryWriter writer, float[] pending)
        {
            int count = 0;
            for (int i = 0; i < pending.Length && i < Main.maxNPCs && count < 255; i++)
            {
                if (pending[i] > 0f)
                    count++;
            }

            writer.Write((byte)count);

            int written = 0;
            for (int i = 0; i < pending.Length && i < Main.maxNPCs && written < count; i++)
            {
                if (pending[i] <= 0f)
                    continue;

                writer.Write((short)i);
                writer.Write(pending[i]);
                written++;
            }
        }

        public static void HandleReport(BinaryReader reader, int sender)
        {
            int count = reader.ReadByte();
            int[] index = new int[count];
            float[] amount = new float[count];
            for (int i = 0; i < count; i++)
            {
                index[i] = reader.ReadInt16();
                amount[i] = reader.ReadSingle();
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            CosmicRadiationSystem s = CosmicRadiationSystem.Instance;

            // Single-player runs this directly (sender = the local player, no checks needed); a server needs a live reporter.
            Player reporter = null;
            if (Main.netMode == NetmodeID.Server)
            {
                if (sender < 0 || sender >= Main.maxPlayers)
                    return;

                reporter = Main.player[sender];
                if (reporter == null || !reporter.active || reporter.dead)
                    return;
            }

            float reach = s.NetInterestRadiusTiles + s.RadiationRadiusTiles + FactorrariaNet.ReachSlackTiles;

            for (int i = 0; i < count; i++)
            {
                if (index[i] < 0 || index[i] >= Main.maxNPCs || !float.IsFinite(amount[i]) || amount[i] <= 0f)
                    continue;

                NPC npc = Main.npc[index[i]];
                if (npc == null || !npc.active)
                    continue;

                if (reporter != null && Vector2.Distance(reporter.Center, npc.Center) / 16f > reach)
                    continue;

                RegisterHit(npc, amount[i]);
            }
        }

        // ---------------------------------------------------------------- server: one (summed) hit on an NPC

        public static void RegisterHit(NPC npc, float multiplier)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || npc == null || !npc.active || npc.life <= 0 || npc.dontTakeDamage || npc.immortal)
                return;

            // A worm's body shares the head's life and buffs: put the radiation on the head.
            if (npc.realLife >= 0 && npc.realLife != npc.whoAmI)
            {
                if (npc.realLife >= Main.maxNPCs || !Main.npc[npc.realLife].active)
                    return;

                npc = Main.npc[npc.realLife];
            }

            CosmicRadiationSystem s = CosmicRadiationSystem.Instance;
            multiplier = Math.Clamp(multiplier, 0f, Math.Max(0f, s.RadiationMaxLevel));
            if (multiplier <= 0f)
                return;

            CosmicRadiationNpc data = npc.GetGlobalNPC<CosmicRadiationNpc>();
            int type = ModContent.BuffType<RadiationBuff>();
            int addTicks = (int)(multiplier * 60f);
            int capTicks = (int)(Math.Max(1f, s.RadiationMaxSeconds) * 60f);
            int buffIndex = npc.FindBuffIndex(type);

            if (buffIndex < 0)
            {
                data.Level = 0f;
                npc.AddBuff(type, Math.Min(Math.Max(1, addTicks), capTicks));   // on a server this also sends the buff to the clients

                if (!npc.HasBuff(type))
                    return;   // this NPC is immune to the debuff: no level, no drain
            }
            else
            {
                npc.buffTime[buffIndex] = Math.Min(npc.buffTime[buffIndex] + addTicks, capTicks);   // vanilla AddBuff would only keep the longer time

                // Re-send the longer time at most twice a second, so a thick sky does not flood the clients.
                if (Main.netMode == NetmodeID.Server && (int)(Main.GameUpdateCount - data.LastBuffSync) >= 30)
                {
                    data.LastBuffSync = Main.GameUpdateCount;
                    NetMessage.SendData(MessageID.NPCBuffs, -1, -1, null, npc.whoAmI);
                }
            }

            data.Level = Math.Min(data.Level + multiplier, Math.Max(0f, s.RadiationMaxLevel));
        }

        // ---------------------------------------------------------------- the effect

        public override void PostAI(NPC npc)
        {
            if (Level <= 0f || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (!npc.HasBuff(ModContent.BuffType<RadiationBuff>()))
                Level = 0f;   // the debuff ran out (or was cleared): the radiation is gone
        }

        // Life loss, same units as the player: lifeRegen is in half HP per second (2 = 1 HP/s). Level is only above 0 on the server and in
        // single-player, which is where the NPC's life is decided; a multiplayer client follows the life the server sends.
        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (Level <= 0f || !npc.HasBuff(ModContent.BuffType<RadiationBuff>()))
                return;

            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            int loss = Math.Max(1, (int)Math.Round(Level * CosmicRadiationSystem.Instance.NpcRadiationLifeRegenPerLevel));
            npc.lifeRegen -= loss;

            // The number that pops up over the NPC: about loss / 6, the ratio vanilla poison uses (12 lifeRegen shows 2).
            damage = Math.Max(damage, Math.Max(2, loss / 6));
        }
    }
}