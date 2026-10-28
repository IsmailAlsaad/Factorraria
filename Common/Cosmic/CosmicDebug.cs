using Factorraria.Common.Net;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // /fcosmic: debug and tuning command for the cosmic radiation source. C8: spawn, log and strike are test commands that only work while
    // EnableDebugs is on in the mod config (they are hidden from normal players but kept for balancing); heat, params, stats, rescan and fx always work.
    //   /fcosmic heat [tileX]   the color order by section and each color's share of the particles at a column
    //   /fcosmic params         every balancing parameter with its value, unit and what it does
    //   /fcosmic stats [reset]  (C2) receivers, sampling areas, rays, hits per color, queue, cost per tick; "reset" zeroes the counters
    //   /fcosmic spawn <color> [tx ty]   (C2, temporary) fires one ray straight down at the cursor (single-player) or at a tile
    //   /fcosmic rescan         (C2) re-reads the sky for gemspark and cloud blocks from the start
    //   /fcosmic log [on|off]   (C4, temporary) prints a chat line for every discharge; with no argument it toggles. Natural sky clouds discharge about once a second
    //   /fcosmic strike <energy> [tx ty]   (C5, temporary) a thunder of that energy at a tile (default: where the player stands) goes through the real
    //                           strike path: the nearest machine within the strike radius gets the surge, otherwise it spits into the sky
    //   /fcosmic fx             (C6) what this process draws: client particles, pool, ionized glow, bolts, what the server sent (works on every side)
    // stats, spawn, rescan, log and strike only work where the simulation runs: single-player and the server console, not on a multiplayer client.
    public class CosmicCommand : ModCommand
    {
        public override string Command => "fcosmic";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fcosmic heat [tileX] | params | stats [reset] | spawn <color> [tx ty] | rescan | log [on|off] | strike <energy> [tx ty] | fx";
        public override string Description => "Cosmic radiation: the color heatmap, every balancing parameter, and (C2) simulation stats and a test ray.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (sub == "heat")
                Heat(caller, args);
            else if (sub == "params")
                Params(caller);
            else if (sub == "stats")
                Stats(caller, args);
            else if (sub == "spawn")
            {
                if (TestCommandsOn(caller, sub))
                    Spawn(caller, args);
            }
            else if (sub == "rescan")
                Rescan(caller);
            else if (sub == "log")
            {
                if (TestCommandsOn(caller, sub))
                    LogToggle(caller, args);
            }
            else if (sub == "strike")
            {
                if (TestCommandsOn(caller, sub))
                    StrikeNow(caller, args);
            }
            else if (sub == "fx")
                Fx(caller);
            else
                caller.Reply("Usage: " + Usage, Color.OrangeRed);
        }

        // C8: the test commands only work while EnableDebugs is on in the Factorraria mod config.
        static bool TestCommandsOn(CommandCaller caller, string what)
        {
            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            if (config != null && config.EnableDebugs)
                return true;

            caller.Reply("fcosmic " + what + " is a test command: turn on EnableDebugs in the Factorraria mod config first.", Color.OrangeRed);
            return false;
        }

        static string Side()
        {
            return Main.netMode == NetmodeID.Server ? "server" : Main.netMode == NetmodeID.MultiplayerClient ? "client" : "single-player";
        }

        static void Heat(CommandCaller caller, string[] args)
        {
            CosmicRadiationSystem system = CosmicRadiationSystem.Instance;
            CosmicHeatmap map = system.Heatmap;

            if (!map.Ready)
            {
                string hint = Main.netMode == NetmodeID.MultiplayerClient ? " (the server sends it on join; try /fjoin)" : " (it is generated on the first world tick)";
                caller.Reply("fcosmic heat [" + Side() + "]: no heatmap yet" + hint, Color.OrangeRed);
                return;
            }

            int worldTiles = Main.maxTilesX;
            float width = map.SectionWidth(worldTiles);

            caller.Reply("fcosmic heat [" + Side() + "]: world " + worldTiles + " tiles wide, " + CosmicHeatmap.Sections + " sections of " + width.ToString("0.#", CultureInfo.InvariantCulture) + " tiles", Color.LightGreen);

            for (int s = 0; s < CosmicHeatmap.Sections; s++)
            {
                int from = (int)(s * width);
                int to = (int)((s + 1) * width) - 1;
                caller.Reply("  section " + s + " (tiles " + from + " to " + to + "): hot spot " + map.HotColorOfSection(s), CosmicColors.DrawColor(map.HotColorOfSection(s)));
            }

            int x;
            if (args.Length > 1 && int.TryParse(args[1], out int typed))
                x = typed;
            else if (caller.Player != null && !Main.dedServ)
                x = (int)(caller.Player.Center.X / 16f);
            else
            {
                caller.Reply("  give a tile X to see the shares there: /fcosmic heat <tileX>", Color.OrangeRed);
                return;
            }

            x = Math.Clamp(x, 0, worldTiles - 1);

            float total = 0f;
            float[] weights = new float[CosmicColors.Count];
            for (int c = 0; c < CosmicColors.Count; c++)
            {
                weights[c] = map.Weight((CosmicColor)c, x, worldTiles, system.HeatFloor, system.HeatSigmaFraction);
                total += weights[c];
            }

            caller.Reply("  share of new particles at tile X " + x + " (section " + map.SectionAt(x, worldTiles) + "):", Color.LightGreen);
            for (int c = 0; c < CosmicColors.Count; c++)
            {
                CosmicColor color = (CosmicColor)c;
                float share = total > 0f ? weights[c] / total * 100f : 0f;
                caller.Reply("    " + color + ": " + share.ToString("0.0", CultureInfo.InvariantCulture) + "%  (weight " + weights[c].ToString("0.000", CultureInfo.InvariantCulture) + ")", CosmicColors.DrawColor(color));
            }
        }

        static void Params(CommandCaller caller)
        {
            CosmicRadiationSystem system = CosmicRadiationSystem.Instance;
            caller.Reply("fcosmic params [" + Side() + "]: values on this side (they live as public fields on CosmicRadiationSystem). Per-color lists are in the order White, Violet, Blue, Green, Yellow, Orange, Red, Grey.", Color.LightGreen);

            foreach (FieldInfo field in typeof(CosmicRadiationSystem).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                CosmicParamAttribute info = field.GetCustomAttribute<CosmicParamAttribute>();
                if (info == null)
                    continue;

                string unit = string.IsNullOrEmpty(info.Unit) ? "" : " " + info.Unit;
                caller.Reply(field.Name + " = " + Format(field.GetValue(system)) + unit + "  -  " + info.Description, Color.White);
            }
        }

        // ------------------------------------------------------------ C6: what is drawn and sent
        static void Fx(CommandCaller caller)
        {
            CosmicEventRouter router = Main.netMode != NetmodeID.MultiplayerClient ? CosmicRadiationSystem.Instance.Sim.Sink as CosmicEventRouter : null;

            if (!Main.dedServ)
                caller.Reply("fcosmic fx [" + Side() + "]: " + CosmicParticleSystem.Instance.StatsText() + ".", Color.LightGreen);

            if (router != null)
                caller.Reply("fcosmic fx [" + Side() + "] events out: " + router.StatsText() + ".", Color.White);
            else if (Main.dedServ)
                caller.Reply("fcosmic fx [server]: no event router.", Color.OrangeRed);
        }

        // ------------------------------------------------------------ C2: stats, spawn, rescan

        static bool NeedSimulation(CommandCaller caller, string what)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return true;

            caller.Reply("fcosmic " + what + ": the simulation runs on the server. Use it in single-player or in the server console.", Color.OrangeRed);
            return false;
        }

        static string Inv(float value, string format = "0.##") => value.ToString(format, CultureInfo.InvariantCulture);

        static string PerColor(long[] counts)
        {
            string text = "";
            for (int c = 0; c < CosmicColors.Count; c++)
                text += (c > 0 ? ", " : "") + (CosmicColor)c + " " + counts[c];
            return text;
        }

        static void Stats(CommandCaller caller, string[] args)
        {
            if (!NeedSimulation(caller, "stats"))
                return;

            CosmicRadiationSystem system = CosmicRadiationSystem.Instance;
            CosmicSimulation sim = system.Sim;

            if (args.Length > 1 && args[1].ToLowerInvariant() == "reset")
            {
                sim.ResetCounters();
                caller.Reply("fcosmic stats [" + Side() + "]: counters reset (the receiver index and the queue are untouched).", Color.LightGreen);
                return;
            }

            if (sim.Fault != null)
            {
                caller.Reply("fcosmic stats [" + Side() + "]: the simulation stopped after an error: " + sim.Fault + " (see the log; it restarts itself after 5 seconds, up to 5 times, restarts so far " + sim.FaultCount + ")", Color.OrangeRed);
                return;
            }

            if (sim.Now == 0)
            {
                caller.Reply("fcosmic stats [" + Side() + "]: the simulation has not started yet (it starts on the first world tick, after the heatmap exists).", Color.OrangeRed);
                return;
            }

            CosmicReceiverIndex index = sim.Index;

            caller.Reply("fcosmic stats [" + Side() + "]: simulation tick " + sim.Now + ". Particle band: rows 0 to " + Inv(sim.BandBottom, "0") + " (world surface x " + Inv(system.SpaceBottomFraction) + "), receivers are indexed down to row " + Math.Min(index.MaxY, Main.maxTilesY - 1) + ".", Color.LightGreen);

            string gems = "";
            for (int c = 0; c < CosmicColors.Count; c++)
                gems += (c > 0 ? ", " : "") + (CosmicColor)c + " " + index.GemsparkCount(c);

            string scan = sim.InitialScanDone ? "first pass done, slow re-check pass " + sim.ScanPasses : "first pass " + sim.ScanPercent + "%";
            caller.Reply("  receivers: " + index.Total + " in " + index.StripCount + " strips. Cloud blocks " + index.CloudCount + ". Gemsparks: " + gems + ". Scan: " + scan + ".", Color.White);

            caller.Reply("  sampling: " + sim.Rigs + " rigs in " + sim.SamplingStrips + " strips, area " + Inv(sim.SamplingAreaTiles, "0") + " tiles^2, expecting about " + Inv(system.RateDensity * sim.DensitySum, "0.##") + " new rays per second.", Color.White);

            caller.Reply("  rays: sampled " + sim.RaysSampled + ", traced " + sim.RaysTraced + " (" + Inv(sim.RaysPerSecond, "0.#") + "/s over the last second); blocked by lead bricks or the world edge " + sim.DeadSolid + ", out of range " + sim.DeadRange + "; test rays fired " + sim.DebugRays + ".", Color.White);
            caller.Reply("  absorbed by gemsparks: " + PerColor(sim.Absorbed), Color.White);
            caller.Reply("  child rays made by gemsparks (re-emitted, per color of the child): " + PerColor(sim.Reemitted) + ". Chain ended at the lowest color (no next step) " + sim.ChainEnds + ", gemspark broken before arrival " + sim.Vanished + ".", Color.White);

            if (sim.HasLastCascade)
                caller.Reply("  last cascade: a " + sim.LastCascadeFrom + " ray was absorbed and made two " + CosmicColors.Next(sim.LastCascadeFrom) + " rays at +" + Inv(sim.LastChildDegA, "0.0") + " and -" + Inv(sim.LastChildDegB, "0.0") + " degrees from its path.", Color.White);
            caller.Reply("  hit clouds: " + PerColor(sim.Ionized), Color.White);
            caller.Reply("  ionized blocks: " + sim.IonField.Count + " now, " + sim.BlocksIonized + " made; hits added to an already ionized block " + sim.HitsAdded + ", hits on a cooling block " + sim.HitsRefractory + ", cloud broken before arrival " + sim.CloudGone + ".", Color.White);
            caller.Reply("  discharges: " + sim.Discharges + " (" + sim.DischargedBlocks + " blocks), biggest energy " + CosmicSimulation.EnergyText(sim.BiggestEnergy) + ".", Color.White);

            int shownDischarges = Math.Min(3, sim.RecentDischargeCount);
            if (shownDischarges > 0)
            {
                string latestDischarges = "";
                for (int k = 0; k < shownDischarges; k++)
                {
                    CosmicDischarge dc = sim.RecentDischarges[(sim.RecentDischargeCount - 1 - k) % sim.RecentDischarges.Length];
                    latestDischarges += (k > 0 ? "; " : "") + "energy " + CosmicSimulation.EnergyText(dc.Energy) + " (" + dc.Blocks + " blocks, base " + CosmicSimulation.EnergyText(dc.BaseSum) + ", at (" + (int)dc.CenterX + ", " + (int)dc.CenterY + "), tick " + dc.Tick + ")";
                }
                caller.Reply("  latest discharges: " + latestDischarges, Color.White);
            }
            caller.Reply("  strikes: " + sim.Strikes + " on machines (biggest surge " + CosmicSimulation.EnergyText(sim.BiggestSurge) + ", clamped " + sim.SurgesClamped + "), " + sim.EnemyStrikes + " hit an enemy (no machine in range), " + sim.Spits + " spat into the sky (no machine or enemy within " + Inv(system.StrikeRadiusTiles) + " tiles). Surging machines now: " + sim.ActiveSurges() + ".", Color.White);
            if (sim.HasLastStrike)
                caller.Reply("  last strike: " + sim.LastStrikeMachine + " at (" + sim.LastStrikeX + ", " + sim.LastStrikeY + "), " + Inv(sim.LastStrikeDistance, "0.#") + " tiles from the thunder, energy " + CosmicSimulation.EnergyText(sim.LastStrikeEnergy) + ", surge " + CosmicSimulation.EnergyText(sim.LastStrikePower) + " for " + system.SurgeDurationTicks + " ticks, tick " + sim.LastStrikeTick + ".", Color.White);
            caller.Reply("  queue: " + sim.Heap.Count + " now, peak " + sim.PeakQueue + ", dropped " + sim.Dropped + " (cap " + system.MaxQueuedEvents + ").", Color.White);
            caller.Reply("  limits (C8): ticks where MaxRaysPerTick (" + system.MaxRaysPerTick + ") held rays back " + sim.RayBacklogTicks + ", restarts after an error " + sim.FaultCount + " of 5.", Color.White);
            caller.Reply("  cost: " + Inv((float)sim.AvgMsPerTick, "0.000") + " ms per tick on average over the last second, worst tick " + Inv((float)sim.MaxMsPerTick, "0.000") + " ms.", Color.White);
            if (sim.Sink is CosmicEventRouter router)
                caller.Reply("  events out (C6): " + router.StatsText() + ".", Color.White);

            int shown = Math.Min(5, sim.RecentCount);
            if (shown > 0)
            {
                string last = "";
                for (int i = 0; i < shown; i++)
                {
                    CosmicQueuedEvent e = sim.Recent[(sim.RecentCount - 1 - i) % sim.Recent.Length];
                    last += (i > 0 ? "; " : "") + e.Kind + " " + e.Color + " at (" + e.TileX + ", " + e.TileY + ") tick " + e.Tick;
                }
                caller.Reply("  latest arrivals: " + last, Color.White);
            }

            string ids = "";
            for (int c = 0; c < CosmicColors.Count; c++)
                ids += (c > 0 ? ", " : "") + (CosmicColor)c + " " + CosmicColors.GemsparkTile((CosmicColor)c);
            caller.Reply("  gemspark tile ids used: " + ids + ". -1 means no gemspark absorbs that color (Grey). If placing a gemspark does not raise its count above, these may be the offline variants.", Color.Gray);
        }

        static bool TryParseColor(string text, out CosmicColor color)
        {
            color = CosmicColor.White;
            if (!Enum.TryParse(text, true, out CosmicColor parsed) || !Enum.IsDefined(typeof(CosmicColor), parsed))
                return false;

            color = parsed;
            return true;
        }

        static void Spawn(CommandCaller caller, string[] args)
        {
            if (!NeedSimulation(caller, "spawn"))
                return;

            if (args.Length < 2 || !TryParseColor(args[1], out CosmicColor color))
            {
                caller.Reply("Usage: /fcosmic spawn <white|violet|blue|green|yellow|orange|red|grey> [tileX tileY]  (without a tile it uses the cursor, single-player only)", Color.OrangeRed);
                return;
            }

            int tx;
            int ty;
            if (args.Length >= 4 && int.TryParse(args[2], out tx) && int.TryParse(args[3], out ty))
            {
            }
            else if (Main.netMode == NetmodeID.SinglePlayer && !Main.dedServ)
            {
                Vector2 mouse = Main.MouseWorld;
                tx = (int)(mouse.X / 16f);
                ty = (int)(mouse.Y / 16f);
            }
            else
            {
                caller.Reply("fcosmic spawn: give the target tile: /fcosmic spawn <color> <tileX> <tileY>", Color.OrangeRed);
                return;
            }

            tx = Math.Clamp(tx, 0, Main.maxTilesX - 1);
            ty = Math.Clamp(ty, 0, Main.maxTilesY - 1);

            caller.Reply("fcosmic spawn [" + Side() + "]: target tile (" + tx + ", " + ty + "). " + CosmicRadiationSystem.Instance.Sim.FireDebugRay(CosmicRadiationSystem.Instance, color, tx, ty), CosmicColors.DrawColor(color));
        }

        static void Rescan(CommandCaller caller)
        {
            if (!NeedSimulation(caller, "rescan"))
                return;

            CosmicRadiationSystem.Instance.Sim.RequestRescan();
            caller.Reply("fcosmic rescan [" + Side() + "]: re-reading the sky from the first strip (watch /fcosmic stats for the progress).", Color.LightGreen);
        }

        static void StrikeNow(CommandCaller caller, string[] args)
        {
            if (!NeedSimulation(caller, "strike"))
                return;

            if (args.Length < 2 || !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double energy) || double.IsNaN(energy) || double.IsInfinity(energy) || energy < 0.0)
            {
                caller.Reply("Usage: /fcosmic strike <energy> [tileX tileY]  (energy is a number, 1e30 works; without a tile it uses where the player stands, single-player only). A white hit is 192.", Color.OrangeRed);
                return;
            }

            float cx;
            float cy;
            if (args.Length >= 4 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float tx) && float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float ty))
            {
                cx = tx + 0.5f;
                cy = ty + 0.5f;
            }
            else if (Main.netMode == NetmodeID.SinglePlayer && !Main.dedServ && caller.Player != null)
            {
                cx = caller.Player.Center.X / 16f;
                cy = caller.Player.Center.Y / 16f;
            }
            else
            {
                caller.Reply("fcosmic strike: give the tile: /fcosmic strike <energy> <tileX> <tileY>", Color.OrangeRed);
                return;
            }

            CosmicRadiationSystem system = CosmicRadiationSystem.Instance;
            CosmicSimulation.StrikeResult result = system.Sim.Strike(system, energy, cx, cy);
            string where = "(" + Inv(cx, "0.#") + ", " + Inv(cy, "0.#") + ")";

            if (result.HitEnemy)
                caller.Reply("fcosmic strike [" + Side() + "]: energy " + CosmicSimulation.EnergyText(energy) + " at " + where + CosmicSimulation.StrikeLogText(system, result) + ".", Color.Yellow);
            else if (result.Struck)
                caller.Reply("fcosmic strike [" + Side() + "]: energy " + CosmicSimulation.EnergyText(energy) + " at " + where + CosmicSimulation.StrikeLogText(system, result) + ". The grid gets the surge on its next power tick.", Color.Yellow);
            else
                caller.Reply("fcosmic strike [" + Side() + "]: energy " + CosmicSimulation.EnergyText(energy) + " at " + where + CosmicSimulation.StrikeLogText(system, result) + " (counted as a spit).", Color.OrangeRed);
        }

        static void LogToggle(CommandCaller caller, string[] args)
        {
            if (!NeedSimulation(caller, "log"))
                return;

            CosmicSimulation sim = CosmicRadiationSystem.Instance.Sim;
            sim.LogDischarges = args.Length > 1 ? args[1].ToLowerInvariant() != "off" : !sim.LogDischarges;

            caller.Reply("fcosmic log [" + Side() + "]: " + (sim.LogDischarges
                ? "every discharge now prints a line. Natural sky clouds discharge about once a second, so expect a lot of lines; /fcosmic stats shows the latest three without this."
                : "discharge lines are off.") , Color.LightGreen);
        }

        static string Format(object value)
        {
            if (value is Array array)
            {
                string text = "";
                foreach (object item in array)
                    text += (text.Length > 0 ? ", " : "") + Format(item);
                return "[" + text + "]";
            }

            if (value is float f)
                return f.ToString("0.####", CultureInfo.InvariantCulture);

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}