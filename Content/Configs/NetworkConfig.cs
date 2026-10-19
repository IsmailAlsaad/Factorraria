using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Factorraria.Content.Configs
{
    // Every multiplayer tunable in one place (roadmap section 2, patch 1.4). Server-side: the server's values rule, and a client
    // never needs them. Code reads them through NetworkConfig.Current, so a change in the config UI takes effect immediately.
    // Fields marked "reserved" are not read yet; the phase that needs them reads them from here instead of adding a constant.
    public class NetworkConfig : ModConfig
    {
        public static NetworkConfig Current => ModContent.GetInstance<NetworkConfig>();

        public override ConfigScope Mode => ConfigScope.ServerSide;

        // ---- request validation and the per-player rate limiter (used by every ToServer packet)

        [DefaultValue(3f)]
        [Range(0f, 10f)]
        public float ReachSlackTiles = 3f;

        // Token bucket per player: RequestsPerSecond refills per second, RequestBurst is the bucket size. 0 = limiter off.
        [DefaultValue(40)]
        [Range(0, 500)]
        public int RequestsPerSecond = 40;

        [DefaultValue(80)]
        [Range(1, 1000)]
        public int RequestBurst = 80;

        // ---- join sync

        [DefaultValue(6000)]
        [Range(500, 30000)]
        public int JoinChunkSize = 6000;

        [DefaultValue(8000000)]
        [Range(100000, 64000000)]
        public int JoinMaxProviderBytes = 8000000;

        [DefaultValue(120)]
        [Range(0, 1800)]
        public int JoinRequestCooldownTicks = 120;

        // ---- reserved for later phases

        [DefaultValue(15)]
        [Range(1, 60)]
        public int CartUpdatesPerSecond = 15;

        [DefaultValue(200)]
        [Range(30, 600)]
        public int CartInterestRadiusTiles = 200;

        [DefaultValue(100)]
        [Range(0, 500)]
        public int CartInterpolationBufferMs = 100;

        [DefaultValue(4)]
        [Range(1, 20)]
        public int MachineStateUpdatesPerSecond = 4;

        [DefaultValue(20)]
        [Range(1, 200)]
        public int LiquidBroadcastsPerTick = 20;

        // ---- vItem replication (patch 7.2): a player is sent the vItems inside a rectangle around them

        [DefaultValue(110)]
        [Range(20, 400)]
        public int VItemInterestHalfWidthTiles = 110;

        [DefaultValue(70)]
        [Range(20, 300)]
        public int VItemInterestHalfHeightTiles = 70;

        // Most spawn entries and most update entries one player receives in one tick (removals get twice this). A big backlog is spread over ticks.
        [DefaultValue(100)]
        [Range(10, 400)]
        public int VItemMaxEntriesPerPacket = 100;
    }
}