using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Networks.Flow;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Networks
{
    // Text view of the new flow topology (liquid overhaul step 2). Later steps add the visual overlay; this is the quick check
    // that the new system understood the pipes, pumps and machines the way you built them.
    public static class FlowNetworkDiagnostics
    {
        static string P(Point p) => "(" + p.X + "," + p.Y + ")";

        static Point MachinePos(BaseMachine machine) => new Point(machine.Position.X, machine.Position.Y);

        public static string Describe(FlowSource source)
        {
            if (source.IsWorld) return "world " + P(source.WorldPos);

            string tank = source.Tank.IsEmpty ? "empty" : LiquidTypeRegistry.Get(source.Tank.LiquidType).Name + " " + source.Tank.Amount.ToString("0");
            return source.Machine.GetType().Name + " " + P(MachinePos(source.Machine)) + " out#" + source.TankIndex + " [" + tank + "]";
        }

        public static string Describe(FlowSink sink)
        {
            if (sink.IsWorld) return "world " + P(sink.WorldPos);
            return sink.Machine.GetType().Name + " " + P(MachinePos(sink.Machine)) + " in";
        }

        static string Rate(float perMinute) => (perMinute / 3600f).ToString("0.##") + "/tick";

        public static List<string> Report(bool all)
        {
            var lines = new List<string>();
            var networks = LiquidNetworkSystem.FlowNetworks;

            int motors = 0, sources = 0, sinks = 0, routes = 0;
            double slowest = 0;
            foreach (FlowNetwork net in networks)
            {
                foreach (var m in net.Motors) if (m != null) motors++;
                sources += net.Sources.Count;
                sinks += net.Sinks.Count;
                routes += net.Routes.Count;
                if (net.BuildMs > slowest) slowest = net.BuildMs;
            }

            lines.Add("fflow: " + networks.Count + " network(s), " + motors + " motor(s), "
                + sources + " source port(s), " + sinks + " sink port(s), " + routes + " route(s); slowest build " + slowest.ToString("0.00") + " ms; rebuild #" + LiquidNetworkSystem.RebuildCount);

            int shownNetworks = 0, hiddenNetworks = 0;
            int maxNetworks = all ? 100 : 8;
            int maxRoutes = all ? 500 : 20;

            for (int i = 0; i < networks.Count; i++)
            {
                FlowNetwork net = networks[i];
                bool interesting = net.Sources.Count > 0 || net.Sinks.Count > 0 || net.Routes.Count > 0;
                if (!interesting) continue;
                if (shownNetworks >= maxNetworks) { hiddenNetworks++; continue; }
                shownNetworks++;

                int motorCount = 0;
                var sb = new StringBuilder();
                for (int n = 0; n < net.Nodes.Length; n++)
                {
                    if (net.Motors[n] == null) continue;
                    motorCount++;
                    sb.Append(" | motor ").Append(P(net.Positions[n])).Append(" ").Append(net.Nodes[n].Facing).Append(net.Nodes[n].MotorOn ? " on" : " OFF");
                }

                lines.Add("fflow: network " + i + ": " + (net.Nodes.Length - motorCount) + " pipe(s), " + motorCount + " motor(s), cap "
                    + (net.Cap >= float.MaxValue ? "none" : Rate(net.Cap)) + ", " + net.Sources.Count + " source(s), " + net.Sinks.Count + " sink(s), "
                    + net.Routes.Count + " route(s), active liquid " + (net.Solver.ActiveType == -1 ? "none" : LiquidTypeRegistry.Get(net.Solver.ActiveType).Name)
                    + ", build " + net.BuildMs.ToString("0.00") + " ms" + sb);

                int shownRoutes = 0;
                foreach (FlowRoute route in net.Routes)
                {
                    if (shownRoutes >= maxRoutes) { lines.Add("fflow:   ... " + (net.Routes.Count - shownRoutes) + " more route(s)"); break; }
                    shownRoutes++;

                    var pumps = new StringBuilder();
                    foreach (int pump in route.Pumps) pumps.Append(P(net.Positions[pump]));

                    lines.Add("fflow:   " + Describe(net.Sources[route.Src]) + " -> " + Describe(net.Sinks[route.Dst]) + ": " + Rate(route.RatePerMinute)
                        + ", climbs " + route.Climbs + ", pumps " + (route.Pumps.Length == 0 ? "none" : pumps.ToString()));
                }
            }

            if (hiddenNetworks > 0)
                lines.Add("fflow: " + hiddenNetworks + " more network(s) with ports not listed (use /fflow all)");

            return lines;
        }
    }

    // /fflow       server console or single-player: what the new liquid topology looks like right now.
    // /fflow all   the same with every network and up to 500 routes each.
    public class FlowNetworkCommand : ModCommand
    {
        public override string Command => "fflow";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fflow [all]";
        public override string Description => "Liquid overhaul: prints the new flow topology (networks, pumps, ports, routes) the server really has.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("fflow: the liquid networks live on the server; type /fflow in the server console.", Color.Yellow);
                return;
            }

            bool all = args.Length > 0 && args[0].ToLowerInvariant() == "all";
            foreach (string line in FlowNetworkDiagnostics.Report(all))
            {
                caller.Reply(line, Color.LightGreen);
                global::Factorraria.Common.Net.FactorrariaNet.Log("[Flow] " + line);
            }
        }
    }
}
