using Factorraria.Common.Cosmic;
using System.IO;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Cosmic radiation netcode (roadmap: docs/cosmic_radiation/ROADMAP.md). Patch C1 syncs the heatmap on join; patch C6 adds the two
    // cosmetic packets CosmicRays (55) and CosmicEvent (56), server -> nearby clients. Their layout is documented on CosmicWire and the
    // server side (collecting, the interest filter, the per-tick caps) is CosmicEventRouter, both in CosmicEventSink.cs.
    public class CosmicNetSystem : ModSystem
    {
        public override void Load()
        {
            JoinSync.Register("cosmic-heatmap", CosmicNet.WriteHeatmap, CosmicNet.ReadHeatmap);
            FactorrariaNet.Register(FactorrariaPacket.CosmicRays, PacketDirection.ToClient, CosmicNet.HandleRays);
            FactorrariaNet.Register(FactorrariaPacket.CosmicEvent, PacketDirection.ToClient, CosmicNet.HandleEvents);
        }
    }

    public static class CosmicNet
    {
        // Server: the complete heatmap state.
        public static void WriteHeatmap(BinaryWriter writer)
        {
            CosmicRadiationSystem.Instance.Heatmap.Write(writer);
        }

        // Client: the rays to draw. The handler may run off the main thread, so it only decodes into a queue; CosmicParticleSystem applies it.
        public static void HandleRays(BinaryReader reader, int sender)
        {
            CosmicWire.ReadRays(reader, CosmicParticleSystem.Instance);
        }

        // Client: absorb, ionize, discharge, strike and spit events, queued the same way.
        public static void HandleEvents(BinaryReader reader, int sender)
        {
            CosmicWire.ReadEvents(reader, CosmicParticleSystem.Instance);
        }

        // Client: replaces the local heatmap with the server's.
        public static void ReadHeatmap(BinaryReader reader)
        {
            if (!CosmicRadiationSystem.Instance.Heatmap.Read(reader))
                FactorrariaNet.Log("[Cosmic] the heatmap from the server could not be read (other version or invalid), keeping the old one");
        }
    }
}