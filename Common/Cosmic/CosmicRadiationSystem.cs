using Factorraria.Common.Net;
using Factorraria.Content.Configs;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Cosmic
{
    // Cosmic radiation power source (roadmap: docs/cosmic_radiation/ROADMAP.md).
    // Patch C1: the per-world color heatmap (generated and saved on the server / single-player, sent to clients through the
    // "cosmic-heatmap" join provider in Common/Net/CosmicNet.cs) and every balancing parameter. Later phases add the receiver index,
    // ray sampling, ionization, strikes and the client particles to this system.
    //
    // Authority: the server (and single-player) owns the heatmap. A multiplayer client only holds a replica, and generates nothing.
    //
    // Balancing parameters are public instance fields (so hot reload can change them). Fields marked [CosmicParam] are listed by
    // /fcosmic params. In C1 only HeatSigmaFraction and HeatFloor are read by code; the others are read by the phase named in
    // their description.
    public class CosmicRadiationSystem : ModSystem
    {
        public static CosmicRadiationSystem Instance => ModContent.GetInstance<CosmicRadiationSystem>();

        public CosmicHeatmap Heatmap = new CosmicHeatmap();

        // The receiver index, ray sampler and event queue (patch C2). Server and single-player only; never saved.
        public CosmicSimulation Sim = new CosmicSimulation();

        // ------------------------------------------------------------ per-color tables (index = CosmicColor value)
        // Order in every table: White, Violet, Blue, Green, Yellow, Orange, Red, Grey (Grey added by N2 in patch C7).

        [CosmicParam("Base energy of one particle per color. Halves per step down, so the cascade keeps the total (used from C4).", "energy")]
        public float[] BaseEnergy = { 128f, 64f, 32f, 16f, 8f, 4f, 2f, 1f };

        [CosmicParam("Ionization multiplier per cloud block per color. Every ionized block multiplies the thunder energy by this (used from C4).", "x")]
        public float[] Multiplier = { 3f, 2.6f, 2.25f, 1.95f, 1.69f, 1.47f, 1.27f, 1.1f };

        [CosmicParam("Smallest cluster of cloud blocks one hit ionizes, per color (used from C4).", "blocks")]
        public int[] ClusterMin = { 1, 1, 2, 3, 5, 7, 10, 14 };

        [CosmicParam("Largest cluster of cloud blocks one hit ionizes, per color (used from C4).", "blocks")]
        public int[] ClusterMax = { 1, 2, 3, 4, 5, 8, 12, 16 };

        // ------------------------------------------------------------ heatmap (used now, C1)

        [CosmicParam("How wide each color's hot spot is, as a fraction of its section (1/8 of the world). Bigger = more bleed into neighbors.", "fraction of section")]
        public float HeatSigmaFraction = 0.35f;

        [CosmicParam("Constant chance every color keeps everywhere, relative to its hot spot peak of 1. 0 = a color never appears outside its bump.", "relative weight")]
        public float HeatFloor = 0.04f;

        // ------------------------------------------------------------ spawning and flight (C2)

        [CosmicParam("Particles per tile^2 per second at the very top of the world, before heat and altitude (used from C2).", "particles/tile^2/s")]
        public float RateDensity = 0.00025f;

        [CosmicParam("Random tilt of a new particle away from straight down (used from C2).", "degrees")]
        public float SpawnSpreadDeg = 15f;

        [CosmicParam("How fast a particle flies (used from C2).", "tiles/tick")]
        public float ParticleSpeed = 0.8f;

        [CosmicParam("How far a particle flies before it dies (used from C2).", "tiles")]
        public float ParticleRange = 160f;

        [CosmicParam("How often the server samples new particles for each sky rig (used from C2).", "ticks")]
        public int SampleStepTicks = 10;

        [CosmicParam("Where the particle band ends, as a fraction of the world surface depth (vanilla's space layer ends at 0.35). Particles start between the top of the world and this depth, and get rarer the lower they start (used from C2).", "fraction of worldSurface")]
        public float SpaceBottomFraction = 0.35f;

        [CosmicParam("How many 8-tile-wide strips of the sky the receiver scan reads per tick while the world is first indexed (used from C2).", "strips/tick")]
        public int ScanStripsPerTick = 10;

        [CosmicParam("How many strips the slow re-check reads per tick afterwards. It repairs the receiver index when a tile changes without a hook (gemspark wire toggles, other mods). 0 = off (used from C2).", "strips/tick")]
        public int ResyncStripsPerTick = 1;

        // ------------------------------------------------------------ cascade (C3)

        [CosmicParam("Smallest angle between a re-emitted particle and the path it came along (used from C3).", "degrees")]
        public float ReemitAngleMinDeg = 5f;

        [CosmicParam("Largest angle between a re-emitted particle and the path it came along (used from C3).", "degrees")]
        public float ReemitAngleMaxDeg = 10f;

        [CosmicParam("Each re-emitted particle gets its own random speed and lifespan, up to this fraction faster or slower and longer or shorter than the plain values (0.2 = 20%, 0 = off).", "fraction")]
        public float ReemitVariation = 0.2f;

        // ------------------------------------------------------------ clouds (C4)

        [CosmicParam("Chance a particle passing through one cloud block is absorbed by it (same for every color, used from C4).", "chance 0-1")]
        public float CloudAbsorbChance = 0.5f;

        [CosmicParam("How long a block stays ionized before the whole cluster discharges (used from C4).", "ticks")]
        public int IonizedLifetimeTicks = 60;

        [CosmicParam("Cooldown after a discharge before those cloud blocks can be ionized again; 0 = off (used from C4).", "ticks")]
        public int RefractoryTicks = 0;

        // ------------------------------------------------------------ strike and grid (C5)

        [CosmicParam("How far from the thunder a machine can be and still be struck. The nearest electric machine (poles count) inside this radius takes the strike.", "tiles")]
        public float StrikeRadiusTiles = 30f;

        [CosmicParam("Grid power added per decade (power of ten) of thunder energy during a surge: surge power = this x log10(energy). One white hit is 192 energy (2.3 decades), a power pole draws 5 to 15, an Autohammer 100. The thunder energy itself stays unbounded; only the number sent to the grid is logarithmic, so it never gets astronomical.", "power/decade")]
        public float SurgePowerPerDecade = 100f;

        [CosmicParam("How long one strike's power spike lasts on the grid. Strikes on the same machine inside this window stack their power and restart it.", "ticks")]
        public int SurgeDurationTicks = 30;

        [CosmicParam("The most surge power one machine can hold (stacked strikes add up to this). With the logarithmic surge one strike is at most about 308 decades x SurgePowerPerDecade, so this only matters for many stacked strikes; lower it for a gameplay cap.", "power")]
        public float MaxSurgePower = 1000000f;

        // ------------------------------------------------------------ safety limits and client (C3, C6)

        [CosmicParam("Most rays the server resolves in one tick; the rest wait (used from C3).", "rays")]
        public int MaxRaysPerTick = 200;

        [CosmicParam("Most queued events; beyond this new ones are dropped and counted (used from C3).", "events")]
        public int MaxQueuedEvents = 20000;

        [CosmicParam("Most particles the client draws at once (used from C6).", "particles")]
        public int ClientParticlePool = 1500;

        [CosmicParam("Pixel quads (trail cells and heads) the client draws per frame before it simplifies: past this a trail loses its sine wave and is drawn as one plain line, past twice this only the heads are drawn. Real particles are drawn before the ambient ones, so the ambient ones simplify first. /fcosmic fx shows the quads drawn (client only, C8, C9b, C9c, C9d).", "quads")]
        public int ClientDrawBudget = 6000;

        [CosmicParam("Players farther than this from an event get no packet for it (used from C6).", "tiles")]
        public int NetInterestRadiusTiles = 120;

        [CosmicParam("Most rays one player is sent per server tick; beyond this they are dropped (cosmetic only, used from C6).", "rays")]
        public int NetMaxRaysPerTick = 300;

        [CosmicParam("Most events (absorb, ionize, discharge, strike, spit) one player is sent per server tick; beyond this they are dropped (cosmetic only, used from C6).", "events")]
        public int NetMaxEventsPerTick = 150;

        [CosmicParam("Length of the fading trail behind a particle on screen (client only, used from C6).", "tiles")]
        public float ClientTrailTiles = 5f;

        [CosmicParam("Diameter of the head of the lowest frequency particle (Grey) on screen. The head size grows with the frequency up to ClientHeadSizeMaxPx for White, the colors in between are interpolated by their order (client only, C9c).", "pixels")]
        public float ClientHeadSizeMinPx = 3f;

        [CosmicParam("Diameter of the head of the highest frequency particle (White) on screen. The head is a hard-edged, fully opaque disc (or the optional Content/Particles/CosmicParticle.png scaled to it). Grey uses ClientHeadSizeMinPx, the colors in between are interpolated by their order (client only, C9c).", "pixels")]
        public float ClientHeadSizeMaxPx = 5f;

        [CosmicParam("How long a new particle takes to grow from size zero to full size (its head and the swing of its trail). 0 = it appears at full size at once (client only, C9c).", "ticks")]
        public float ClientHeadGrowTicks = 10f;

        [CosmicParam("When the head of a particle starts to fade out, as a fraction of its flight time (ray length / speed). 0.8 = the head fades smoothly over the last fifth of the flight and is invisible on arrival. The trail does not fade: it stays where it is and drains away from its rear end until it has caught up with the spot where the head died. 1 = the head vanishes on arrival (client only, C9e).", "fraction")]
        public float ClientFadeOutStart = 0.8f;

        [CosmicParam("Brightness multiplier of the particles and their trails (client only, used from C6).", "x")]
        public float ClientBrightness = 1f;

        [CosmicParam("How long a lightning bolt (strike or spit) stays on screen (client only, used from C6).", "ticks")]
        public int BoltLifeTicks = 14;

        [CosmicParam("Width of a lightning bolt's core at the lowest energy (client only, C6b).", "px")]
        public float BoltBaseWidthPx = 1.5f;

        [CosmicParam("Extra bolt core width per decade (power of ten) of thunder energy, so a stronger strike is a thicker bolt (client only, C6b). 192 energy is 2.3 decades, 1e10 is 10.", "px/decade")]
        public float BoltWidthPerDecadePx = 1.2f;

        [CosmicParam("The thickest a bolt core can get (client only, C6b). The glow around it is 3 times as wide.", "px")]
        public float BoltMaxWidthPx = 30f;

        [CosmicParam("How much light a flashing (ionized) cloud block gives off, in its own color, as a multiple of its tint strength (client only, C6e). 0 = no light.", "x")]
        public float IonizedLightIntensity = 0.9f;

        // ------------------------------------------------------------ C7: ambient local simulation, strike polish (client only)

        [CosmicParam("Ambient local simulation (C7): simulate the cosmic particles that fall through empty sky around the local player, client only, never counted or sent. It switches itself off near any receiver (cloud or lit gemspark), where the real particles are. false = no ambient particles at all.", "on/off")]
        public bool LocalSimEnabled = true;

        [CosmicParam("Ambient density relative to the real sky (it uses the same RateDensity, heatmap and altitude). 1 = the same as the real sky, 2 = twice as many (client only, C7).", "x")]
        public float LocalSimDensityBoost = 1f;

        [CosmicParam("Half the width of the area around the player where ambient particles start (client only, C7).", "tiles")]
        public float LocalSimHalfWidthTiles = 100f;

        [CosmicParam("Half the height of the area around the player that ambient particles must pass through (client only, C7). Particles start up to ParticleRange above it.", "tiles")]
        public float LocalSimHalfHeightTiles = 60f;

        [CosmicParam("The most ambient particles alive at once. When the pool is full new ones are skipped (client only, C7).", "particles")]
        public int LocalSimMaxParticles = 400;

        [CosmicParam("The ambient simulation switches off when a receiver (cloud block or lit gemspark) is within this many tiles of the player in both directions. The server only simulates near receivers, so there the real particles fill the sky (client only, C7).", "tiles")]
        public float LocalSimOffRadiusTiles = 120f;

        [CosmicParam("Brightness of the ambient particles relative to the real ones (client only, C7).", "x")]
        public float LocalSimBrightness = 0.7f;

        [CosmicParam("How much light a lightning bolt gives off at both ends while it is on screen. 0 = no light (client only, C7).", "x")]
        public float BoltLightIntensity = 1.2f;

        [CosmicParam("Size of the short flash where lightning starts and lands, in its first 6 ticks. 0 = no flash (client only, C7).", "pixels")]
        public float StrikeFlashSizePx = 40f;

        [CosmicParam("How long the ambient local simulation takes to fade all the way in or out when it switches (near a receiver, off or on, after a teleport). 0 = switch at once (client only, C7b).", "seconds")]
        public float LocalSimFadeSeconds = 2f;

        [CosmicParam("Thunder at or above this energy tier (log10 of the energy x 4, the number that also sets the bolt width) plays the big SoundID.Thunder, weaker thunder plays the small bug zap. Tiers are whole numbers; /fcosmic stats shows the biggest energy, tier = log10(energy) x 4 (client only, C7b).", "tier")]
        public float ThunderBigTier = 20f;

        // ------------------------------------------------------------ C9b: the look of an energetic particle (client only)

        [CosmicParam("Size of one pixel of a particle's trail, in world pixels. The trail is drawn on this grid, snapped like the power pole wire (client only, C9b).", "pixels")]
        public float ClientTrailPixelPx = 2f;

        [CosmicParam("How far the trail's sine wave swings to each side, for the White particle (the highest frequency). Lower frequencies swing less, in even steps down to Grey, whose trail is a plain straight line. The swing is the same along the whole trail and never exceeds half the particle's head width, so the trail does not stick out past the head. 0 = straight trails for every color (client only, C9b, C9d).", "pixels")]
        public float ClientWaveAmplitudePx = 4f;

        [CosmicParam("Distance between two peaks of the trail's sine wave. The wave stands still in the world while the particle flies through it, so the peaks seem to slide back along the trail at exactly the particle's speed (client only, C9b).", "tiles")]
        public float ClientWaveLengthTiles = 1.25f;

        // ------------------------------------------------------------ C8: hot reload and bad values
        // Runs every tick on the server and in single-player, right before the simulation. A tunable that is edited to something the simulation
        // cannot work with (a per-color table with the wrong length after a hot reload, a negative chance, NaN) is put back inside a safe range
        // here, instead of throwing an exception in the middle of a tick and stopping the whole simulation. Values that are fine are not touched.
        public void Sanitize()
        {
            BaseEnergy = FitTable(BaseEnergy, 1f);
            Multiplier = FitTable(Multiplier, 1f);
            ClusterMin = FitTable(ClusterMin, 1);
            ClusterMax = FitTable(ClusterMax, 1);

            for (int c = 0; c < CosmicColors.Count; c++)
            {
                ClusterMin[c] = Math.Clamp(ClusterMin[c], 1, 4096);
                ClusterMax[c] = Math.Clamp(ClusterMax[c], ClusterMin[c], 4096);
            }

            HeatSigmaFraction = Fix(HeatSigmaFraction, 0.01f, 10f);
            HeatFloor = Fix(HeatFloor, 0f, 1f);
            RateDensity = Fix(RateDensity, 0f, 1000f);
            SpawnSpreadDeg = Fix(SpawnSpreadDeg, 0f, 89f);
            ParticleSpeed = Fix(ParticleSpeed, 0.01f, 100f);
            ParticleRange = Fix(ParticleRange, 1f, 4000f);
            SampleStepTicks = Math.Clamp(SampleStepTicks, 1, 600);
            SpaceBottomFraction = Fix(SpaceBottomFraction, 0.01f, 1f);
            ScanStripsPerTick = Math.Clamp(ScanStripsPerTick, 1, 1000);
            ResyncStripsPerTick = Math.Clamp(ResyncStripsPerTick, 0, 1000);
            ReemitAngleMinDeg = Fix(ReemitAngleMinDeg, 0f, 89f);
            ReemitAngleMaxDeg = Fix(ReemitAngleMaxDeg, 0f, 89f);
            ReemitVariation = Fix(ReemitVariation, 0f, 0.5f);
            CloudAbsorbChance = Fix(CloudAbsorbChance, 0f, 1f);
            IonizedLifetimeTicks = Math.Clamp(IonizedLifetimeTicks, 1, 36000);
            RefractoryTicks = Math.Clamp(RefractoryTicks, 0, 36000);
            StrikeRadiusTiles = Fix(StrikeRadiusTiles, 0f, 5000f);
            SurgePowerPerDecade = Fix(SurgePowerPerDecade, 0f, 1000000f);
            SurgeDurationTicks = Math.Clamp(SurgeDurationTicks, 1, 36000);
            MaxSurgePower = Fix(MaxSurgePower, 0f, 1000000000f);
            MaxRaysPerTick = Math.Clamp(MaxRaysPerTick, 1, 100000);
            MaxQueuedEvents = Math.Clamp(MaxQueuedEvents, 100, 1000000);
        }

        // NaN becomes the minimum; everything else is clamped into the range.
        static float Fix(float value, float min, float max) => float.IsNaN(value) ? min : Math.Clamp(value, min, max);

        // A per-color table needs exactly one entry per color and no NaN, negative or infinite entry. A too-short table keeps its entries and is
        // padded with its last one; a too-long one is cut; a null one becomes the fallback in every slot.
        static float[] FitTable(float[] table, float fallback)
        {
            if (table == null || table.Length != CosmicColors.Count)
            {
                float[] fitted = new float[CosmicColors.Count];
                for (int i = 0; i < fitted.Length; i++)
                    fitted[i] = table != null && table.Length > 0 ? table[Math.Min(i, table.Length - 1)] : fallback;
                table = fitted;
            }

            for (int i = 0; i < table.Length; i++)
            {
                if (float.IsNaN(table[i]) || float.IsInfinity(table[i]) || table[i] < 0f)
                    table[i] = fallback;
            }

            return table;
        }

        static int[] FitTable(int[] table, int fallback)
        {
            if (table == null || table.Length != CosmicColors.Count)
            {
                int[] fitted = new int[CosmicColors.Count];
                for (int i = 0; i < fitted.Length; i++)
                    fitted[i] = table != null && table.Length > 0 ? table[Math.Min(i, table.Length - 1)] : fallback;
                table = fitted;
            }

            return table;
        }

        // ------------------------------------------------------------ C9: the configs
        // The two mod configs (Cosmic Balance, server side; Cosmic Visuals, client side) hold the values you edit in the Mod Config screen; this copies
        // them into the fields above, which are the only values the rest of the code reads. Called when a config changes (OnChanged), when the mod
        // finishes loading and when a world loads (so a config that was read before this system existed is still applied).
        public void ApplyConfigs()
        {
            ModContent.GetInstance<CosmicBalanceConfig>()?.Apply(this);
            ModContent.GetInstance<CosmicVisualConfig>()?.Apply(this);
        }

        public override void PostSetupContent() => ApplyConfigs();

        public override void OnWorldLoad() => ApplyConfigs();

        // ------------------------------------------------------------ lifecycle

        public override void PostUpdateWorld()
        {
            // Server and single-player only. A client waits for the join provider. Generating here (not in OnWorldLoad) is safe
            // whatever order tModLoader calls LoadWorldData and OnWorldLoad in: it only runs once the world is fully loaded.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (!Heatmap.Ready)
            {
                Heatmap.Generate(new Random());
                FactorrariaNet.Log("[Cosmic] generated a new heatmap for this world, order by section: " + OrderText());
            }

            // Patch C2: receiver index, particle sampling and the event queue (server and single-player only).
            Sanitize();
            Sim.Update(this);
        }

        public override void OnWorldUnload()
        {
            Heatmap.Clear();
            Sim.Reset();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (!Heatmap.Ready)
                return;

            tag["CosmicHeatmapVersion"] = Heatmap.FormatVersion;
            tag["CosmicHeatmapOrder"] = (byte[])Heatmap.Order.Clone();
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Heatmap.Clear();

            if (!tag.ContainsKey("CosmicHeatmapOrder") || tag.GetInt("CosmicHeatmapVersion") != Heatmap.FormatVersion)
                return; // a world from before this feature: PostUpdateWorld generates and the next save keeps it

            if (!Heatmap.SetOrder(tag.GetByteArray("CosmicHeatmapOrder")))
                FactorrariaNet.Log("[Cosmic] the saved heatmap order is not valid, a new one will be generated");
        }

        public string OrderText()
        {
            string text = "";
            for (int s = 0; s < CosmicHeatmap.Sections; s++)
                text += (s > 0 ? ", " : "") + Heatmap.HotColorOfSection(s);
            return text;
        }
    }
}