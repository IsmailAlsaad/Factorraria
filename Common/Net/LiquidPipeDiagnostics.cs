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
            int pipes = 0, motors = 0, machines = 0, world = 0, typed = 0, flowing = 0;
            foreach (LiquidNetwork net in LiquidNetworkSystem.ActiveNetworks)
            {
                pipes += net.PipeTiles.Count;
                motors += net.Motors.Count;
                machines += net.MachineAttachments.Count;
                world += net.WorldLiquidAttachments.Count;
                if (net.LiquidType != -1) typed++;
                if (net.ResolvedFlow.Count > 0) flowing++;
            }

            return "rebuild #" + LiquidNetworkSystem.RebuildCount + ": " + pipes + " pipes in networks, " + motors + " motor(s), " + machines + " machine attachment(s), "
                + world + " world-liquid attachment(s), " + typed + " typed network(s), " + flowing + " with flow";
        }

        static string P(Point p) => "(" + p.X + "," + p.Y + ")";

        public static List<string> Report(bool scan)
        {
            var lines = new List<string>();
            lines.Add("fpipes: " + Summary() + "; AllPipeTiles " + LiquidNetworkSystem.AllPipeTiles.Count + "; rebuild flag " + LiquidNetworkSystem.networkNeedsRebuilding + ", flow flag " + LiquidNetworkSystem.flowNeedsRecalculating);

            // Every motor entity that exists, whether or not a network contains it. A motor missing from the networks below is the clue.
            var inNetworks = new HashSet<Point>();
            foreach (LiquidNetwork net in LiquidNetworkSystem.ActiveNetworks)
                foreach (MotorAttachment m in net.Motors)
                    inNetworks.Add(m.Position);

            foreach (TileEntity te in TileEntity.ByID.Values)
            {
                if (te is MotorTileEntityBase motor)
                {
                    var pos = new Point(motor.Position.X, motor.Position.Y);
                    lines.Add("fpipes: motor " + motor.GetType().Name + " " + P(pos) + " facing " + motor.Facing + ", isOn " + motor.isOn + ", in a network: " + inNetworks.Contains(pos));
                }
            }

            int shown = 0, hidden = 0;
            for (int i = 0; i < LiquidNetworkSystem.ActiveNetworks.Count; i++)
            {
                LiquidNetwork net = LiquidNetworkSystem.ActiveNetworks[i];
                bool interesting = net.Motors.Count > 0 || net.MachineAttachments.Count > 0 || net.WorldLiquidAttachments.Count > 0;
                if (!interesting || shown >= 12)
                {
                    if (interesting) hidden++;
                    continue;
                }
                shown++;

                var sb = new StringBuilder();
                sb.Append("fpipes: network ").Append(i).Append(": ").Append(net.PipeTiles.Count).Append(" pipe(s), liquid ")
                  .Append(net.LiquidType == -1 ? "none yet" : LiquidTypeRegistry.Get(net.LiquidType).Name)
                  .Append(", max flow ").Append(net.MaxFlowRate >= float.MaxValue ? "none" : net.MaxFlowRate.ToString("0")).Append(", flow tiles ").Append(net.ResolvedFlow.Count);

                foreach (MotorAttachment m in net.Motors)
                    sb.Append(" | motor ").Append(P(m.Position)).Append(" ").Append(m.Motor.Facing).Append(m.Motor.isOn ? " on" : " OFF");

                foreach (PipeAttachment a in net.MachineAttachments)
                    sb.Append(" | machine ").Append(a.Machine.GetType().Name).Append(" ").Append(P(a.Position)).Append(" via pipe ").Append(P(a.PipePosition)).Append(" ").Append(a.MouthDirection);

                foreach (PipeAttachment a in net.WorldLiquidAttachments)
                {
                    Tile t = Main.tile[a.Position.X, a.Position.Y];
                    sb.Append(" | world ").Append(P(a.Position)).Append(" via pipe ").Append(P(a.PipePosition)).Append(" ").Append(a.MouthDirection)
                      .Append(" liquid ").Append(t.LiquidAmount).Append("/255 type ").Append(t.LiquidType);
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