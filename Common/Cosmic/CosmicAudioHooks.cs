using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Factorraria.Common.Cosmic
{
    // C7: the sound hooks. They are EMPTY on purpose: audio can be dropped in here later without touching any logic.
    //
    // Client only. CosmicParticleSystem calls them when it draws what the server (or single-player) told it happened, so a dedicated
    // server never calls them and nothing here can change a counter, a particle or a machine. Positions are in tile units (world pixels / 16).
    // The ambient local simulation never calls them (ambient particles make no sound).
    //
    // To add a sound later: fill in a body, for example SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.3f }, new Vector2(tileX, tileY) * 16f).
    public static class CosmicAudioHooks
    {
        // A gemspark absorbed a particle of this color (an Absorb event arrived).
        public static void OnAbsorbed(CosmicColor color, float tileX, float tileY)
        {
        }

        // The gemspark re-emitted two particles of the next color (called together with OnAbsorbed when the color has a next step).
        public static void OnReemitted(CosmicColor childColor, float tileX, float tileY)
        {
            int soundIndex = Main.rand.Next(1, 11);
            string soundPath = $"Sounds/GeigerCounterClick/GeigerCounterClick{soundIndex}";
            SoundEngine.PlaySound(new SoundStyle(soundPath) with { Volume = 0.4f , PitchVariance = 0.2f, MaxInstances = 0}, new Vector2(tileX, tileY) * 16f);
        }

        // A cloud cluster was ionized. (tileX, tileY) is the first block of the cluster, blocks how many blocks it has.
        public static void OnIonized(CosmicColor color, float tileX, float tileY, int blocks)
        {
        }

        // Thunder: a strike (strike = true, the bolt goes from (ax, ay) to the machine at (bx, by)) or a spit (strike = false, no machine in range).
        // tier is log10(energy) x 4 (0 for energy 1 or less), the same number that sets the bolt width.
        public static void OnThunder(float ax, float ay, float bx, float by, byte tier, bool strike)
        {
            // LATER MAKE STRONG THUNDER SOUND USE SoundID.Thunder
            SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with { Volume = 0.6f }, new Vector2(ax, ay) * 16f);
        }
    }
}