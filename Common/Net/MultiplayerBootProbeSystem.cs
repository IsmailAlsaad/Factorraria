using Factorraria.Common.Carts;
using Factorraria.Common.Machines;
using Factorraria.Common.Statues;
using Factorraria.Common.Systems;
using System;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // TEMPORARY multiplayer diagnostics (roadmap Phase 0, tasks 0.2 and 0.4). Read-only: it only writes to the mod log, never sends
    // packets and never touches client-only state, so it is safe on a dedicated server, a Host & Play host, a joining client and
    // in single-player. Every line starts with [MP-Boot]. The log goes to client.log for any process that has a client and to
    // server.log on a dedicated server. Switch it off with EnableProbe (hot reload) or delete the file once Phase 0 is closed.
    public class MultiplayerBootProbeSystem : ModSystem
    {
        public static bool EnableProbe = true;
        public static int SummaryDelayTicks = 120; // world data (tile entities, pipes) is only fully populated a moment after OnWorldLoad

        int ticksSinceLoad;
        bool summaryPending;

        // Fires once at mod load, before any world exists, on every side. If this line is missing from the log, the loaded
        // .tmod does not contain this class (stale or failed build, or a different copy of the mod is enabled).
        public override void Load()
        {
            Mod.Logger.Info("[MP-Boot] probe system Load(): " + Describe() + " enabled=" + EnableProbe);
        }

        public override void PostSetupContent()
        {
            Mod.Logger.Info("[MP-Boot] probe system PostSetupContent(): " + Describe());
        }

        public override void OnWorldLoad()
        {
            if (!EnableProbe)
                return;

            ticksSinceLoad = 0;
            summaryPending = true;
            Mod.Logger.Info("[MP-Boot] world loaded: " + Describe());
        }

        public override void OnWorldUnload()
        {
            summaryPending = false;
        }

        public override void PostUpdateEverything()
        {
            if (!EnableProbe || !summaryPending || Main.gameMenu)
                return;

            if (++ticksSinceLoad < SummaryDelayTicks)
                return;

            summaryPending = false;
            LogSummary();
        }

        // Which process and side this is. Run it as host, as a joining client and on the dedicated server and compare the pid and
        // netMode values: that answers whether Host & Play runs the server in the same process or a separate one (verification item 1).
        static string Describe()
        {
            string mode = Main.netMode switch
            {
                NetmodeID.SinglePlayer => "SinglePlayer",
                NetmodeID.MultiplayerClient => "MultiplayerClient",
                NetmodeID.Server => "Server",
                _ => Main.netMode.ToString()
            };
            string local = Main.dedServ ? "n/a" : Main.myPlayer.ToString();
            return $"pid={Environment.ProcessId} netMode={mode} dedServ={Main.dedServ} myPlayer={local}";
        }

        void LogSummary()
        {
            Mod.Logger.Info("[MP-Boot] summary after " + SummaryDelayTicks + " ticks: " + Describe());

            Probe("TrackData (item 4, C7)", () =>
            {
                bool ok = TrackData.EnsureLoaded();
                return ok ? "loaded" : "NOT loaded, reason=" + (TrackData.FailureReason ?? "none (it will retry)");
            });

            Probe("pipe tiles registered", () => LiquidNetworkSystem.AllPipeTiles.Count.ToString());

            Probe("machine entities", () =>
            {
                int total = 0;
                int initialised = 0;
                foreach (TileEntity entity in TileEntity.ByPosition.Values)
                {
                    if (entity is BaseMachine machine)
                    {
                        total++;
                        if (machine.MachineInitialized)
                            initialised++;
                    }
                }
                return total + " (geometry initialised: " + initialised + ")";
            });

            Probe("machine visual registry", () =>
            {
                var definitions = MachineVisualRegistry.Definitions.Values;
                int missing = definitions.Count(d => d.OnTexture == null || d.OffTexture == null);
                int loaded = definitions.Count(d => d.OnTexture != null && d.OnTexture.IsLoaded);
                return definitions.Count + " definitions, " + missing + " without a texture request, " + loaded + " On textures loaded";
            });

            Probe("awakened statue entries (A11)", () => ModContent.GetInstance<AwakenedStatueRegistry>().EntryCount + " entries");
        }

        // One failing probe must not hide the others.
        void Probe(string name, Func<string> read)
        {
            try
            {
                Mod.Logger.Info("[MP-Boot]   " + name + ": " + read());
            }
            catch (Exception e)
            {
                Mod.Logger.Warn("[MP-Boot]   " + name + " FAILED: " + e);
            }
        }
    }
}