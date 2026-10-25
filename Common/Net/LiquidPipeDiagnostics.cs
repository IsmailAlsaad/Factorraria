using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Networks;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // TEMPORARY diagnostics for patch 8.1b (finding R9, remove in Phase 10). The liquid networks live on the server only, so a pool,
    // pipes, a motor and a machine built by a CLIENT that "did not deliver liquid until a reload" cannot be seen from the client. This
    // prints what the server's networks actually contain: every network with its pipes, motors (position, Facing, isOn), machine and
    // world-liquid attachments, plus every motor entity in the world (even one no network contains) and, with "scan", a world scan
    // that compares the pipe list with the pipe tiles really in the world and counts machine tiles that have no entity.
    public static class LiquidPipeDiagnostics
    {
        /// <summary>One short line for the "[Pipes] networks rebuilt" log line.</summary>
        public static string Summary()
        {
            int pipes = 0, motors = 0, sources = 0, sinks = 0, routes = 0, active = 0;
            foreach (FlowNetwork net in LiquidNetworkSystem.FlowNetworks)
            {
                int netMotors = net.Motors.Count(m => m != null);
                motors += netMotors;
                pipes += net.Nodes.Length - netMotors;
                sources += net.Sources.Count;
                sinks += net.Sinks.Count;
                routes += net.Routes.Count;
                if (net.Solver.ActiveType != -1) active++;
            }

            return "rebuild #" + LiquidNetworkSystem.RebuildCount + ": " + LiquidNetworkSystem.FlowNetworks.Count + " network(s), " + pipes + " pipes, " + motors + " motor(s), "
                + sources + " source port(s), " + sinks + " sink port(s), " + routes + " route(s), " + active + " network(s) with an active liquid";
        }

        static string P(Point p) => "(" + p.X + "," + p.Y + ")";

        public static List<string> Report(bool scan)
        {
            var lines = new List<string>();
            lines.Add("fpipes: " + Summary() + "; AllPipeTiles " + LiquidNetworkSystem.AllPipeTiles.Count + "; rebuild flag " + LiquidNetworkSystem.networkNeedsRebuilding);

            // Every motor entity that exists, whether or not a network contains it. A motor missing from the networks below is the clue.
            var inNetworks = new HashSet<Point>();
            foreach (FlowNetwork net in LiquidNetworkSystem.FlowNetworks)
                for (int n = 0; n < net.Nodes.Length; n++)
                    if (net.Motors[n] != null) inNetworks.Add(net.Positions[n]);

            foreach (TileEntity te in TileEntity.ByID.Values)
            {
                if (te is MotorTileEntityBase motor)
                {
                    var pos = new Point(motor.Position.X, motor.Position.Y);
                    lines.Add("fpipes: motor " + motor.GetType().Name + " " + P(pos) + " facing " + motor.Facing + ", isOn " + motor.isOn + ", in a network: " + inNetworks.Contains(pos));
                }
            }

            int shown = 0, hidden = 0;
            for (int i = 0; i < LiquidNetworkSystem.FlowNetworks.Count; i++)
            {
                FlowNetwork net = LiquidNetworkSystem.FlowNetworks[i];
                int motorCount = net.Motors.Count(m => m != null);
                bool interesting = motorCount > 0 || net.Sources.Count > 0 || net.Sinks.Count > 0;
                if (!interesting || shown >= 12)
                {
                    if (interesting) hidden++;
                    continue;
                }
                shown++;

                var sb = new StringBuilder();
                sb.Append("fpipes: network ").Append(i).Append(": ").Append(net.Nodes.Length - motorCount).Append(" pipe(s), liquid ")
                  .Append(net.Solver.ActiveType == -1 ? "none yet" : LiquidTypeRegistry.Get(net.Solver.ActiveType).Name)
                  .Append(", cap ").Append(net.Cap >= float.MaxValue ? "none" : (net.Cap / 3600f).ToString("0.##") + "/tick").Append(", routes ").Append(net.Routes.Count);

                for (int n = 0; n < net.Nodes.Length; n++)
                {
                    if (net.Motors[n] == null) continue;
                    sb.Append(" | motor ").Append(P(net.Positions[n])).Append(" ").Append(net.Motors[n].Facing).Append(net.Nodes[n].MotorOn ? " on" : " OFF");
                }

                foreach (FlowSource source in net.Sources)
                {
                    sb.Append(" | source ").Append(FlowNetworkDiagnostics.Describe(source));
                    if (source.IsWorld)
                    {
                        Tile t = Main.tile[source.WorldPos.X, source.WorldPos.Y];
                        sb.Append(" liquid ").Append(t.LiquidAmount).Append("/255 type ").Append(t.LiquidType);
                    }
                }

                foreach (FlowSink sink in net.Sinks)
                {
                    if (!sink.IsWorld) sb.Append(" | sink ").Append(FlowNetworkDiagnostics.Describe(sink));
                }

                lines.Add(sb.ToString());
            }

            if (hidden > 0)
                lines.Add("fpipes: " + hidden + " more network(s) with attachments not listed");

            if (scan)
            {
                int pipeTiles = 0, missing = 0, machineTilesWithoutEntity = 0, machineTiles = 0;
                for (int x = 0; x < Main.maxTilesX; x++)
                {
                    for (int y = 0; y < Main.maxTilesY; y++)
                    {
                        Tile tile = Main.tile[x, y];
                        if (!tile.HasTile)
                            continue;

                        if (PipeTierRegistry.IsPipeTile(tile.TileType))
                        {
                            pipeTiles++;
                            if (!LiquidNetworkSystem.AllPipeTiles.Contains(new Point(x, y)))
                                missing++;
                        }
                        else if (MachineVisualRegistry.Definitions.ContainsKey(tile.TileType))
                        {
                            machineTiles++;
                            if (!TileEntityHelper.TryGetEntityFromTile(x, y, out _, out _))
                                machineTilesWithoutEntity++;
                        }
                    }
                }

                int stale = LiquidNetworkSystem.AllPipeTiles.Count(p => !WorldGen.InWorld(p.X, p.Y) || !Main.tile[p.X, p.Y].HasTile || !PipeTierRegistry.IsPipeTile(Main.tile[p.X, p.Y].TileType));
                lines.Add("fpipes scan: " + pipeTiles + " pipe tile(s) in the world, " + missing + " of them NOT in AllPipeTiles, " + stale + " stale position(s) in AllPipeTiles; "
                    + machineTiles + " machine tile(s) (every tile of a machine counts), " + machineTilesWithoutEntity + " of them with no entity");
            }

            return lines;
        }
    }

    // /fpipes        server console or single-player: the networks as they are now.
    // /fpipes scan   the same plus the world scan (a few seconds on a big world).
    public class LiquidPipeCommand : ModCommand
    {
        public override string Command => "fpipes";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fpipes [scan]";
        public override string Description => "Multiplayer test (8.1b): prints the liquid networks the server (or single-player) really has.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("fpipes: the liquid networks live on the server; type /fpipes in the server console.", Color.Yellow);
                return;
            }

            bool scan = args.Length > 0 && args[0].ToLowerInvariant() == "scan";
            foreach (string line in LiquidPipeDiagnostics.Report(scan))
            {
                caller.Reply(line, Color.LightGreen);
                FactorrariaNet.Log("[Pipes] " + line);
            }
        }
    }
}