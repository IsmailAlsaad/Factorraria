using Factorraria.Common.Cosmic;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Factorraria.Content.Configs
{
    // C9: every look and sound number of the cosmic radiation, client side: each player keeps their own, and a change shows at once. Mods > Factorraria >
    // Cosmic Visuals. Same mechanism as CosmicBalanceConfig (Apply copies the values into CosmicRadiationSystem's fields).
    public class CosmicVisualConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        public override void OnChanged()
        {
            CosmicRadiationSystem.Instance?.ApplyConfigs();
        }

        [Header("Particles")]
        [DefaultValue(1500)]
        [Range(16, 20000)]
        public int ClientParticlePool = 1500;

        [DefaultValue(6000)]
        [Range(100, 100000)]
        public int ClientDrawBudget = 6000;

        [DefaultValue(4f)]
        [Range(1f, 20f)]
        public float ClientParticleSizePx = 4f;

        [DefaultValue(1f)]
        [Range(0f, 4f)]
        public float ClientBrightness = 1f;

        [DefaultValue(5f)]
        [Range(0.5f, 40f)]
        public float ClientTrailTiles = 5f;

        [Header("Trail")]
        [DefaultValue(2f)]
        [Range(1f, 8f)]
        public float ClientTrailPixelPx = 2f;

        [DefaultValue(4f)]
        [Range(0f, 16f)]
        public float ClientWaveAmplitudePx = 4f;

        [DefaultValue(1.25f)]
        [Range(0.2f, 8f)]
        public float ClientWaveLengthTiles = 1.25f;

        [DefaultValue(14f)]
        [Range(2f, 120f)]
        public float ClientHaloPeriodTicks = 14f;

        [DefaultValue(28f)]
        [Range(4f, 240f)]
        public float ClientHaloLifeTicks = 28f;

        [DefaultValue(3.5f)]
        [Range(1f, 12f)]
        public float ClientHaloScale = 3.5f;

        [DefaultValue(0.45f)]
        [Range(0f, 2f)]
        public float ClientHaloBrightness = 0.45f;

        [Header("Lightning")]
        [DefaultValue(14)]
        [Range(1, 120)]
        public int BoltLifeTicks = 14;

        [DefaultValue(1.5f)]
        [Range(0.5f, 20f)]
        public float BoltBaseWidthPx = 1.5f;

        [DefaultValue(1.2f)]
        [Range(0f, 20f)]
        public float BoltWidthPerDecadePx = 1.2f;

        [DefaultValue(30f)]
        [Range(1f, 100f)]
        public float BoltMaxWidthPx = 30f;

        [DefaultValue(1.2f)]
        [Range(0f, 4f)]
        public float BoltLightIntensity = 1.2f;

        [DefaultValue(40f)]
        [Range(0f, 200f)]
        public float StrikeFlashSizePx = 40f;

        [DefaultValue(0.9f)]
        [Range(0f, 4f)]
        public float IonizedLightIntensity = 0.9f;

        [Header("Ambient")]
        [DefaultValue(true)]
        public bool LocalSimEnabled = true;

        [DefaultValue(1f)]
        [Range(0f, 10f)]
        public float LocalSimDensityBoost = 1f;

        [DefaultValue(100f)]
        [Range(10f, 400f)]
        public float LocalSimHalfWidthTiles = 100f;

        [DefaultValue(60f)]
        [Range(10f, 300f)]
        public float LocalSimHalfHeightTiles = 60f;

        [DefaultValue(400)]
        [Range(0, 5000)]
        public int LocalSimMaxParticles = 400;

        [DefaultValue(120f)]
        [Range(0f, 1000f)]
        public float LocalSimOffRadiusTiles = 120f;

        [DefaultValue(0.7f)]
        [Range(0f, 4f)]
        public float LocalSimBrightness = 0.7f;

        [DefaultValue(2f)]
        [Range(0f, 20f)]
        public float LocalSimFadeSeconds = 2f;

        [Header("Sound")]
        [DefaultValue(20f)]
        [Range(0f, 255f)]
        public float ThunderBigTier = 20f;

        // Copies every value into the live CosmicRadiationSystem fields (the only copy the rest of the code reads) and then lets Sanitize clamp them.
        public void Apply(CosmicRadiationSystem s)
        {
            if (s == null)
                return;

            s.ClientParticlePool = ClientParticlePool;
            s.ClientDrawBudget = ClientDrawBudget;
            s.ClientParticleSizePx = ClientParticleSizePx;
            s.ClientBrightness = ClientBrightness;
            s.ClientTrailTiles = ClientTrailTiles;
            s.ClientTrailPixelPx = ClientTrailPixelPx;
            s.ClientWaveAmplitudePx = ClientWaveAmplitudePx;
            s.ClientWaveLengthTiles = ClientWaveLengthTiles;
            s.ClientHaloPeriodTicks = ClientHaloPeriodTicks;
            s.ClientHaloLifeTicks = ClientHaloLifeTicks;
            s.ClientHaloScale = ClientHaloScale;
            s.ClientHaloBrightness = ClientHaloBrightness;
            s.BoltLifeTicks = BoltLifeTicks;
            s.BoltBaseWidthPx = BoltBaseWidthPx;
            s.BoltWidthPerDecadePx = BoltWidthPerDecadePx;
            s.BoltMaxWidthPx = BoltMaxWidthPx;
            s.BoltLightIntensity = BoltLightIntensity;
            s.StrikeFlashSizePx = StrikeFlashSizePx;
            s.IonizedLightIntensity = IonizedLightIntensity;
            s.LocalSimEnabled = LocalSimEnabled;
            s.LocalSimDensityBoost = LocalSimDensityBoost;
            s.LocalSimHalfWidthTiles = LocalSimHalfWidthTiles;
            s.LocalSimHalfHeightTiles = LocalSimHalfHeightTiles;
            s.LocalSimMaxParticles = LocalSimMaxParticles;
            s.LocalSimOffRadiusTiles = LocalSimOffRadiusTiles;
            s.LocalSimBrightness = LocalSimBrightness;
            s.LocalSimFadeSeconds = LocalSimFadeSeconds;
            s.ThunderBigTier = ThunderBigTier;

            s.Sanitize();
        }
    }
}
