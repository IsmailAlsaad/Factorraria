using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Temporary test command for patch 5.1 (remove in Phase 10): lists the machines THIS side has, one line each, so a server console
    // and a client can be compared line by line. Runs where it is typed: in the server console it shows the authoritative machines, in
    // a client's chat the replicas, in single-player the real ones.
    //   /fsnap          counters + the first 30 machines
    //   /fsnap all      every machine
    //   /fsnap share    server console only: re-sends every machine to every client with message 86 (tests the sharing path again)
    public class MachineSnapshotCommand : ModCommand
    {
        public override string Command => "fsnap";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fsnap [all|share]";
        public override string Description => "Multiplayer test (5.1): lists this side's machines and snapshot counters. 'share' (server console) re-sends all machines.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            List<BaseMachine> machines = TileEntity.ByID.Values.OfType<BaseMachine>().OrderBy(m => m.Position.Y).ThenBy(m => m.Position.X).ToList();

            if (mode == "share")
            {
                if (Main.netMode != NetmodeID.Server)
                {
                    caller.Reply("fsnap share: only works in the dedicated server console (it re-sends every machine to every client).", Color.Orange);
                    return;
                }

                foreach (BaseMachine machine in machines)
                    MachinePlacement.Share(machine);

                caller.Reply("fsnap: shared " + machines.Count + " machine(s) with every client (message 86).", Color.LightGreen);
                return;
            }

            string side = Main.netMode == NetmodeID.Server ? "server" : Main.netMode == NetmodeID.MultiplayerClient ? "client replicas" : "single-player";
            string summary = "fsnap [" + side + "]: " + machines.Count + " machine(s); snapshots sent " + MachineSnapshot.Sent + " (" + MachineSnapshot.SentBytes
                + " B), received " + MachineSnapshot.Received + " (" + MachineSnapshot.ReceivedBytes + " B), failed " + MachineSnapshot.Failed
                + "; light: packets sent " + MachineLightSync.PacketsSent + " (" + MachineLightSync.EntriesSent + " entries, " + MachineLightSync.BytesSent
                + " B), entries received " + MachineLightSync.EntriesReceived + " (applied " + MachineLightSync.Applied + ", missed " + MachineLightSync.Missed
                + ", failed " + MachineLightSync.Failed + ")";
            caller.Reply(summary, Color.LightGreen);
            FactorrariaNet.Log("[Snapshot] " + summary);

            int limit = mode == "all" ? machines.Count : 30;
            for (int i = 0; i < machines.Count && i < limit; i++)
            {
                string line = machines[i].DescribeSnapshot();
                caller.Reply(line, Color.White);
                FactorrariaNet.Log("[Snapshot] " + line);
            }

            if (machines.Count > limit)
                caller.Reply("... " + (machines.Count - limit) + " more (use /fsnap all)", Color.Gray);
        }
    }
}