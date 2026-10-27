using Factorraria.Common.Cosmic;
using System.IO;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Cosmic radiation netcode (roadmap: docs/cosmic_radiation/ROADMAP.md). Patch C1 only syncs the heatmap on join; the ray and
    // event packets (ids 55 and 56) arrive with patch C6.
    public class CosmicNetSystem : ModSystem
    {
        public override void Load()
        {
            JoinSync.Register("cosmic-heatmap", CosmicNet.WriteHeatmap, CosmicNet.ReadHeatmap);
        }
    }

    public static class CosmicNet
    {
        // Server: the complete heatmap state.
        public static void WriteHeatmap(BinaryWriter writer)
        {
            CosmicRadiationSystem.Instance.Heatmap.Write(writer);
        }

        // Client: replaces the local heatmap with the server's.
        public static void ReadHeatmap(BinaryReader reader)
        {
            if (!CosmicRadiationSystem.Instance.Heatmap.Read(reader))
                FactorrariaNet.Log("[Cosmic] the heatmap from the server could not be read (other version or invalid), keeping the old one");
        }
    }
}