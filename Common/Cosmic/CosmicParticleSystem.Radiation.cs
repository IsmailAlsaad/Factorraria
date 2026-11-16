using System;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // C11 (client only): the radiation hit check, the third half of CosmicParticleSystem. It looks at the particles THIS client already has
    // (the real ones the server sent and the ambient local ones) and counts each one once when its head first comes within
    // RadiationRadiusTiles of the local player. What you see flying at you is what irradiates you. Nothing here is sent, saved or counted
    // in the server's counters, and it never changes a particle except by setting its Hit flag.
    public partial class CosmicParticleSystem : ModSystem
    {
        public long RadiationHits;   // for /fcosmic fx style debugging, client only

        void CheckRadiation(uint now)
        {
            if (Main.dedServ || Main.gamePaused)
                return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead)
                return;

            CosmicRadiationPlayer radiation = player.GetModPlayer<CosmicRadiationPlayer>();
            if (radiation.Shielded)
                return;

            CosmicRadiationSystem s = CosmicRadiationSystem.Instance;
            float radius = s.RadiationRadiusTiles;
            if (radius <= 0f)
                return;

            float px = player.Center.X / 16f;
            float py = player.Center.Y / 16f;
            float r2 = radius * radius;

            ScanRadiation(pool, now, px, py, r2, radiation, s);
            ScanRadiation(ambient, now, px, py, r2, radiation, s);
        }

        void ScanRadiation(Particle[] set, uint now, float px, float py, float r2, CosmicRadiationPlayer radiation, CosmicRadiationSystem s)
        {
            for (int i = 0; i < set.Length; i++)
            {
                ref Particle p = ref set[i];
                if (!p.Active || p.Hit)
                    continue;

                float age = (int)(now - p.Born);
                if (age < 0f)
                    continue;   // an ambient child that starts at a tick that has not come yet

                float travelled = age * p.Speed;
                if (travelled >= p.Length)
                    continue;   // the head has arrived (or died): only the trail is left

                float dx = p.X + p.DirX * travelled - px;
                float dy = p.Y + p.DirY * travelled - py;
                if (dx * dx + dy * dy > r2)
                    continue;

                p.Hit = true;
                RadiationHits++;

                float multiplier = p.Color < s.Multiplier.Length ? s.Multiplier[p.Color] : 1f;
                radiation.RegisterHit(multiplier);
            }
        }
    }
}