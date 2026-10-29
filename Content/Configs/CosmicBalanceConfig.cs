using Factorraria.Common.Cosmic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Factorraria.Content.Configs
{
    // C9: every balancing number of the cosmic radiation power source that the SERVER decides, in the in-game Mod Config screen (Mods > Factorraria >
    // Cosmic Balance). Server side: in multiplayer every player gets the server's values, and only the server owner can change them while playing.
    // The values are copied into the public fields of CosmicRadiationSystem (Apply, also at world load), so no code path reads two copies; those fields'
    // own initializers are only a fallback. Change a default here (the [DefaultValue] and the initializer) and in CosmicRadiationSystem's matching field.
    // The per-color tables are one block of four numbers per color, in the order White, Violet, Blue, Green, Yellow, Orange, Red, Grey.
    // /fcosmic params shows the default next to the current value and /fcosmic set changes one for quick testing (roadmap section 7 lists them all).
    public class CosmicBalanceConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        // Only the owner of the server (the host of a Host & Play game, or the player at a local server) may change the balance while a game is running.
        public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
        {
            if (!IsServerOwner(Main.player[whoAmI]))
            {
                message = NetworkText.FromLiteral("Only the server owner can change the cosmic balance.");
                return false;
            }

            return true;
        }

        // True for the player who owns the server: the one connected from the same machine (a local address). tModLoader has no ready-made check
        // for this, so it is written out here. On the server it looks the player up among the connected clients; on a client it asks about its own connection.
        static bool IsServerOwner(Player player)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return Netplay.Connection.Socket.GetRemoteAddress().IsLocalHost();

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                RemoteClient client = Netplay.Clients[i];
                if (client.State == 10 && Main.player[i] == player && client.Socket.GetRemoteAddress().IsLocalHost())
                    return true;
            }

            return false;
        }

        public override void OnChanged()
        {
            CosmicRadiationSystem.Instance?.ApplyConfigs();
        }

        [Header("ColorWhite")]
        [DefaultValue(128f)]
        [Range(0f, 100000f)]
        public float WhiteBaseEnergy = 128f;

        [DefaultValue(3f)]
        [Range(0f, 20f)]
        public float WhiteMultiplier = 3f;

        [DefaultValue(1)]
        [Range(1, 100)]
        public int WhiteClusterMin = 1;

        [DefaultValue(1)]
        [Range(1, 200)]
        public int WhiteClusterMax = 1;

        [Header("ColorViolet")]
        [DefaultValue(64f)]
        [Range(0f, 100000f)]
        public float VioletBaseEnergy = 64f;

        [DefaultValue(2.6f)]
        [Range(0f, 20f)]
        public float VioletMultiplier = 2.6f;

        [DefaultValue(1)]
        [Range(1, 100)]
        public int VioletClusterMin = 1;

        [DefaultValue(2)]
        [Range(1, 200)]
        public int VioletClusterMax = 2;

        [Header("ColorBlue")]
        [DefaultValue(32f)]
        [Range(0f, 100000f)]
        public float BlueBaseEnergy = 32f;

        [DefaultValue(2.25f)]
        [Range(0f, 20f)]
        public float BlueMultiplier = 2.25f;

        [DefaultValue(2)]
        [Range(1, 100)]
        public int BlueClusterMin = 2;

        [DefaultValue(3)]
        [Range(1, 200)]
        public int BlueClusterMax = 3;

        [Header("ColorGreen")]
        [DefaultValue(16f)]
        [Range(0f, 100000f)]
        public float GreenBaseEnergy = 16f;

        [DefaultValue(1.95f)]
        [Range(0f, 20f)]
        public float GreenMultiplier = 1.95f;

        [DefaultValue(3)]
        [Range(1, 100)]
        public int GreenClusterMin = 3;

        [DefaultValue(4)]
        [Range(1, 200)]
        public int GreenClusterMax = 4;

        [Header("ColorYellow")]
        [DefaultValue(8f)]
        [Range(0f, 100000f)]
        public float YellowBaseEnergy = 8f;

        [DefaultValue(1.69f)]
        [Range(0f, 20f)]
        public float YellowMultiplier = 1.69f;

        [DefaultValue(5)]
        [Range(1, 100)]
        public int YellowClusterMin = 5;

        [DefaultValue(5)]
        [Range(1, 200)]
        public int YellowClusterMax = 5;

        [Header("ColorOrange")]
        [DefaultValue(4f)]
        [Range(0f, 100000f)]
        public float OrangeBaseEnergy = 4f;

        [DefaultValue(1.47f)]
        [Range(0f, 20f)]
        public float OrangeMultiplier = 1.47f;

        [DefaultValue(7)]
        [Range(1, 100)]
        public int OrangeClusterMin = 7;

        [DefaultValue(8)]
        [Range(1, 200)]
        public int OrangeClusterMax = 8;

        [Header("ColorRed")]
        [DefaultValue(2f)]
        [Range(0f, 100000f)]
        public float RedBaseEnergy = 2f;

        [DefaultValue(1.27f)]
        [Range(0f, 20f)]
        public float RedMultiplier = 1.27f;

        [DefaultValue(10)]
        [Range(1, 100)]
        public int RedClusterMin = 10;

        [DefaultValue(12)]
        [Range(1, 200)]
        public int RedClusterMax = 12;

        [Header("ColorGrey")]
        [DefaultValue(1f)]
        [Range(0f, 100000f)]
        public float GreyBaseEnergy = 1f;

        [DefaultValue(1.1f)]
        [Range(0f, 20f)]
        public float GreyMultiplier = 1.1f;

        [DefaultValue(14)]
        [Range(1, 100)]
        public int GreyClusterMin = 14;

        [DefaultValue(16)]
        [Range(1, 200)]
        public int GreyClusterMax = 16;

        [Header("Heatmap")]
        [DefaultValue(0.35f)]
        [Range(0.05f, 3f)]
        public float HeatSigmaFraction = 0.35f;

        [DefaultValue(0.04f)]
        [Range(0f, 1f)]
        public float HeatFloor = 0.04f;

        [Header("Spawning")]
        [DefaultValue(0.00025f)]
        [Range(0f, 0.05f)]
        public float RateDensity = 0.00025f;

        [DefaultValue(15f)]
        [Range(0f, 89f)]
        public float SpawnSpreadDeg = 15f;

        [DefaultValue(0.8f)]
        [Range(0.05f, 10f)]
        public float ParticleSpeed = 0.8f;

        [DefaultValue(160f)]
        [Range(10f, 1000f)]
        public float ParticleRange = 160f;

        [DefaultValue(10)]
        [Range(1, 120)]
        public int SampleStepTicks = 10;

        [DefaultValue(0.35f)]
        [Range(0.05f, 1f)]
        public float SpaceBottomFraction = 0.35f;

        [DefaultValue(10)]
        [Range(1, 200)]
        public int ScanStripsPerTick = 10;

        [DefaultValue(1)]
        [Range(0, 100)]
        public int ResyncStripsPerTick = 1;

        [Header("Cascade")]
        [DefaultValue(5f)]
        [Range(0f, 89f)]
        public float ReemitAngleMinDeg = 5f;

        [DefaultValue(10f)]
        [Range(0f, 89f)]
        public float ReemitAngleMaxDeg = 10f;

        [DefaultValue(0.2f)]
        [Range(0f, 0.5f)]
        public float ReemitVariation = 0.2f;

        [Header("Clouds")]
        [DefaultValue(0.5f)]
        [Range(0f, 1f)]
        public float CloudAbsorbChance = 0.5f;

        [DefaultValue(60)]
        [Range(1, 3600)]
        public int IonizedLifetimeTicks = 60;

        [DefaultValue(0)]
        [Range(0, 3600)]
        public int RefractoryTicks = 0;

        [Header("Strike")]
        [DefaultValue(30f)]
        [Range(0f, 500f)]
        public float StrikeRadiusTiles = 30f;

        [DefaultValue(100f)]
        [Range(0f, 10000f)]
        public float SurgePowerPerDecade = 100f;

        [DefaultValue(30)]
        [Range(1, 3600)]
        public int SurgeDurationTicks = 30;

        [DefaultValue(1000000f)]
        [Range(0f, 1000000000f)]
        public float MaxSurgePower = 1000000f;

        [Header("Limits")]
        [DefaultValue(200)]
        [Range(1, 5000)]
        public int MaxRaysPerTick = 200;

        [DefaultValue(20000)]
        [Range(100, 200000)]
        public int MaxQueuedEvents = 20000;

        [DefaultValue(120)]
        [Range(10, 1000)]
        public int NetInterestRadiusTiles = 120;

        [DefaultValue(300)]
        [Range(1, 5000)]
        public int NetMaxRaysPerTick = 300;

        [DefaultValue(150)]
        [Range(1, 2000)]
        public int NetMaxEventsPerTick = 150;

        // Copies every value into the live CosmicRadiationSystem fields (the only copy the rest of the code reads) and then lets Sanitize clamp them.
        public void Apply(CosmicRadiationSystem s)
        {
            if (s == null)
                return;

            s.BaseEnergy = new float[] { WhiteBaseEnergy, VioletBaseEnergy, BlueBaseEnergy, GreenBaseEnergy, YellowBaseEnergy, OrangeBaseEnergy, RedBaseEnergy, GreyBaseEnergy };
            s.Multiplier = new float[] { WhiteMultiplier, VioletMultiplier, BlueMultiplier, GreenMultiplier, YellowMultiplier, OrangeMultiplier, RedMultiplier, GreyMultiplier };
            s.ClusterMin = new int[] { WhiteClusterMin, VioletClusterMin, BlueClusterMin, GreenClusterMin, YellowClusterMin, OrangeClusterMin, RedClusterMin, GreyClusterMin };
            s.ClusterMax = new int[] { WhiteClusterMax, VioletClusterMax, BlueClusterMax, GreenClusterMax, YellowClusterMax, OrangeClusterMax, RedClusterMax, GreyClusterMax };
            s.HeatSigmaFraction = HeatSigmaFraction;
            s.HeatFloor = HeatFloor;
            s.RateDensity = RateDensity;
            s.SpawnSpreadDeg = SpawnSpreadDeg;
            s.ParticleSpeed = ParticleSpeed;
            s.ParticleRange = ParticleRange;
            s.SampleStepTicks = SampleStepTicks;
            s.SpaceBottomFraction = SpaceBottomFraction;
            s.ScanStripsPerTick = ScanStripsPerTick;
            s.ResyncStripsPerTick = ResyncStripsPerTick;
            s.ReemitAngleMinDeg = ReemitAngleMinDeg;
            s.ReemitAngleMaxDeg = ReemitAngleMaxDeg;
            s.ReemitVariation = ReemitVariation;
            s.CloudAbsorbChance = CloudAbsorbChance;
            s.IonizedLifetimeTicks = IonizedLifetimeTicks;
            s.RefractoryTicks = RefractoryTicks;
            s.StrikeRadiusTiles = StrikeRadiusTiles;
            s.SurgePowerPerDecade = SurgePowerPerDecade;
            s.SurgeDurationTicks = SurgeDurationTicks;
            s.MaxSurgePower = MaxSurgePower;
            s.MaxRaysPerTick = MaxRaysPerTick;
            s.MaxQueuedEvents = MaxQueuedEvents;
            s.NetInterestRadiusTiles = NetInterestRadiusTiles;
            s.NetMaxRaysPerTick = NetMaxRaysPerTick;
            s.NetMaxEventsPerTick = NetMaxEventsPerTick;

            s.Sanitize();
        }
    }
}
