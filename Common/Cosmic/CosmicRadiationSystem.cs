using Factorraria.Common.Net;
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
        // Order in every table: White, Violet, Blue, Green, Yellow, Orange, Red.

        [CosmicParam("Base energy of one particle per color. Halves per step down, so the cascade keeps the total (used from C4).", "energy")]
        public float[] BaseEnergy = { 64f, 32f, 16f, 8f, 4f, 2f, 1f };

        [CosmicParam("Ionization multiplier per cloud block per color. Every ionized block multiplies the thunder energy by this (used from C4).", "x")]
        public float[] Multiplier = { 3f, 2.54f, 2.15f, 1.82f, 1.54f, 1.3f, 1.1f };

        [CosmicParam("Smallest cluster of cloud blocks one hit ionizes, per color (used from C4).", "blocks")]
        public int[] ClusterMin = { 1, 1, 2, 3, 5, 7, 10 };

        [CosmicParam("Largest cluster of cloud blocks one hit ionizes, per color (used from C4).", "blocks")]
        public int[] ClusterMax = { 1, 2, 3, 4, 5, 8, 12 };

        // ------------------------------------------------------------ heatmap (used now, C1)

        [CosmicParam("How wide each color's hot spot is, as a fraction of its section (1/7 of the world). Bigger = more bleed into neighbors.", "fraction of section")]
        public float HeatSigmaFraction = 0.35f;

        [CosmicParam("Constant chance every color keeps everywhere, relative to its hot spot peak of 1. 0 = a color never appears outside its bump.", "relative weight")]
        public float HeatFloor = 0.08f;

        // ------------------------------------------------------------ spawning and flight (C2)

        [CosmicParam("Particles per tile^2 per second at the very top of the world, before heat and altitude (used from C2).", "particles/tile^2/s")]
        public float RateDensity = 0.0004f;

        [CosmicParam("Random tilt of a new particle away from straight down (used from C2).", "degrees")]
        public float SpawnSpreadDeg = 5f;

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

        [CosmicParam("Grid power added per point of thunder energy during a surge. One white hit is 192 energy, a power pole draws 5 to 15, an Autohammer 100.", "power/energy")]
        public float SurgePowerPerEnergy = 1f;

        [CosmicParam("How long one strike's power spike lasts on the grid. Strikes on the same machine inside this window stack their power and restart it.", "ticks")]
        public int SurgeDurationTicks = 30;

        [CosmicParam("Overflow guard only: the most surge power one machine can hold. The thunder energy itself is never capped (a huge merged cluster really is astronomical); lower this for a gameplay cap.", "power")]
        public float MaxSurgePower = 1e30f;

        // ------------------------------------------------------------ safety limits and client (C3, C6)

        [CosmicParam("Most rays the server resolves in one tick; the rest wait (used from C3).", "rays")]
        public int MaxRaysPerTick = 200;

        [CosmicParam("Most queued events; beyond this new ones are dropped and counted (used from C3).", "events")]
        public int MaxQueuedEvents = 20000;

        [CosmicParam("Most particles the client draws at once (used from C6).", "particles")]
        public int ClientParticlePool = 1500;

        [CosmicParam("Players farther than this from an event get no packet for it (used from C6).", "tiles")]
        public int NetInterestRadiusTiles = 120;

        [CosmicParam("Most rays one player is sent per server tick; beyond this they are dropped (cosmetic only, used from C6).", "rays")]
        public int NetMaxRaysPerTick = 300;

        [CosmicParam("Most events (absorb, ionize, discharge, strike, spit) one player is sent per server tick; beyond this they are dropped (cosmetic only, used from C6).", "events")]
        public int NetMaxEventsPerTick = 150;

        [CosmicParam("Length of the fading trail behind a particle on screen (client only, used from C6).", "tiles")]
        public float ClientTrailTiles = 5f;

        [CosmicParam("Diameter of a particle's head on screen. The optional Content/Particles/CosmicParticle.png is scaled to it (client only, used from C6).", "pixels")]
        public float ClientParticleSizePx = 3f;

        [CosmicParam("Brightness multiplier of the particles and their trails (client only, used from C6).", "x")]
        public float ClientBrightness = 1f;

        [CosmicParam("How long a lightning bolt (strike or spit) stays on screen (client only, used from C6).", "ticks")]
        public int BoltLifeTicks = 14;

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
            for (int s = 0; s < 7; s++)
                text += (s > 0 ? ", " : "") + Heatmap.HotColorOfSection(s);
            return text;
        }
    }
}