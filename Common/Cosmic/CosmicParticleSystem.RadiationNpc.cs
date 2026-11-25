using Factorraria.Common.Net;
using Factorraria.Content.Buffs;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // C12 (client only): the fourth part of CosmicParticleSystem. Like CheckRadiation for the local player, but for NPCs: a particle counts as a
    // hit on an NPC at the moment its head ENTERS RadiationRadiusTiles of the NPC's hitbox (distance to the box, so big NPCs count by their
    // size). Entering is stateless: now inside and one tick earlier outside (or the particle is brand new), so one particle hits each NPC once
    // and can hit several NPCs, and the particle's own Hit flag (the player's) is untouched. Hits are summed per NPC and sent to the server in
    // one batched packet (CosmicNpcRadiation, every NpcRadiationReportTicks ticks; single-player applies them every tick). Only NPCs whose
    // nearest player is the local player are counted, so two clients do not both report the same real particle.
    public partial class CosmicParticleSystem : ModSystem
    {
        public long NpcRadiationHits;   // for /fcosmic fx style debugging, client only

        struct NpcTarget
        {
            public int Index;
            public float X0, Y0, X1, Y1;   // the hitbox in tiles
        }

        NpcTarget[] npcTargets = new NpcTarget[64];
        int npcTargetCount;
        float npcBoundX0, npcBoundY0, npcBoundX1, npcBoundY1;   // all targets together, grown by the radius
        float[] npcPending = new float[0];
        bool npcAnyPending;
        uint npcLastReport;

        void CheckNpcRadiation(uint now)
        {
            if (Main.dedServ || Main.gamePaused)
                return;

            if (npcPending.Length != Main.npc.Length)
                npcPending = new float[Main.npc.Length];

            Player me = Main.LocalPlayer;
            if (me == null || !me.active || me.dead)
            {
                ClearNpcPending();
                return;
            }

            CosmicRadiationSystem s = CosmicRadiationSystem.Instance;
            if (s.RadiationRadiusTiles > 0f)
            {
                BuildNpcTargets(me, s);

                if (npcTargetCount > 0)
                {
                    ScanNpcRadiation(pool, now, s);
                    ScanNpcRadiation(ambient, now, s);
                }
            }

            FlushNpcRadiation(now, s);
        }

        void ClearNpcPending()
        {
            if (!npcAnyPending)
                return;

            Array.Clear(npcPending, 0, npcPending.Length);
            npcAnyPending = false;
        }

        void BuildNpcTargets(Player me, CosmicRadiationSystem s)
        {
            npcTargetCount = 0;

            int buff = ModContent.BuffType<RadiationBuff>();
            float radius = s.RadiationRadiusTiles;
            float window = s.NetInterestRadiusTiles + radius;
            float window2 = window * window;
            float mx = me.Center.X / 16f;
            float my = me.Center.Y / 16f;

            npcBoundX0 = float.MaxValue;
            npcBoundY0 = float.MaxValue;
            npcBoundX1 = float.MinValue;
            npcBoundY1 = float.MinValue;

            for (int i = 0; i < Main.maxNPCs && npcTargetCount < npcTargets.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active || npc.life <= 0 || npc.dontTakeDamage || npc.immortal)
                    continue;

                if (npc.realLife >= 0 && npc.realLife != i)
                    continue;   // a worm's body segment: the head carries the radiation

                if (npc.buffImmune[buff])
                    continue;

                float dx = npc.Center.X / 16f - mx;
                float dy = npc.Center.Y / 16f - my;
                float d2 = dx * dx + dy * dy;
                if (d2 > window2)
                    continue;

                if (!IsNearestPlayer(npc, me, d2))
                    continue;

                Rectangle box = npc.Hitbox;
                NpcTarget t = new NpcTarget
                {
                    Index = i,
                    X0 = box.Left / 16f,
                    Y0 = box.Top / 16f,
                    X1 = box.Right / 16f,
                    Y1 = box.Bottom / 16f
                };
                npcTargets[npcTargetCount++] = t;

                npcBoundX0 = Math.Min(npcBoundX0, t.X0 - radius);
                npcBoundY0 = Math.Min(npcBoundY0, t.Y0 - radius);
                npcBoundX1 = Math.Max(npcBoundX1, t.X1 + radius);
                npcBoundY1 = Math.Max(npcBoundY1, t.Y1 + radius);
            }
        }

        // Ties go to the lower player index. In single-player there is only the local player.
        static bool IsNearestPlayer(NPC npc, Player me, float myDistance2)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return true;

            float nx = npc.Center.X / 16f;
            float ny = npc.Center.Y / 16f;

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player other = Main.player[p];
                if (other == null || !other.active || other.dead || p == me.whoAmI)
                    continue;

                float dx = other.Center.X / 16f - nx;
                float dy = other.Center.Y / 16f - ny;
                float d2 = dx * dx + dy * dy;
                if (d2 < myDistance2 || (d2 == myDistance2 && p < me.whoAmI))
                    return false;
            }

            return true;
        }

        static float DistanceToBox2(float x, float y, in NpcTarget t)
        {
            float dx = x < t.X0 ? t.X0 - x : (x > t.X1 ? x - t.X1 : 0f);
            float dy = y < t.Y0 ? t.Y0 - y : (y > t.Y1 ? y - t.Y1 : 0f);
            return dx * dx + dy * dy;
        }

        void ScanNpcRadiation(Particle[] set, uint now, CosmicRadiationSystem s)
        {
            float radius = s.RadiationRadiusTiles;
            float r2 = radius * radius;

            for (int i = 0; i < set.Length; i++)
            {
                ref Particle p = ref set[i];
                if (!p.Active)
                    continue;

                int age = (int)(now - p.Born);
                if (age < 0)
                    continue;   // an ambient child that starts at a tick that has not come yet

                float travelled = age * p.Speed;
                if (travelled >= p.Length)
                    continue;   // the head has arrived (or died): only the trail is left

                float hx = p.X + p.DirX * travelled;
                float hy = p.Y + p.DirY * travelled;
                if (hx < npcBoundX0 || hx > npcBoundX1 || hy < npcBoundY0 || hy > npcBoundY1)
                    continue;

                float px = hx - p.DirX * p.Speed;   // where the head was one tick ago
                float py = hy - p.DirY * p.Speed;
                float multiplier = p.Color < s.Multiplier.Length ? s.Multiplier[p.Color] : 1f;

                for (int t = 0; t < npcTargetCount; t++)
                {
                    if (DistanceToBox2(hx, hy, in npcTargets[t]) > r2)
                        continue;

                    if (age > 0 && DistanceToBox2(px, py, in npcTargets[t]) <= r2)
                        continue;   // it was already inside one tick ago: that was the hit

                    npcPending[npcTargets[t].Index] += multiplier;
                    npcAnyPending = true;
                    NpcRadiationHits++;
                }
            }
        }

        void FlushNpcRadiation(uint now, CosmicRadiationSystem s)
        {
            if (!npcAnyPending)
                return;

            int interval = Main.netMode == NetmodeID.MultiplayerClient ? Math.Max(1, s.NpcRadiationReportTicks) : 1;
            if ((int)(now - npcLastReport) < interval)
                return;

            npcLastReport = now;

            // SendRequest writes the body right away, so clearing afterwards is safe. In single-player it runs the server handler directly.
            FactorrariaNet.SendRequest(FactorrariaPacket.CosmicNpcRadiation, writer => CosmicRadiationNpc.WriteReport(writer, npcPending));
            ClearNpcPending();
        }
    }
}