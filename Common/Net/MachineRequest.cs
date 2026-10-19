using Factorraria.Common.Machines;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace Factorraria.Common.Net
{
    // Outcome of MachineRequest.Validate. Ignored: nobody to answer (bad player or position, or not the server). Refused: the request is
    // well formed but cannot be served (no machine there, or the player is out of reach); the caller sends its own rejection.
    public enum RequestCheck : byte
    {
        Ok,
        Ignored,
        Refused
    }

    // Patch 6.1: the one server-side validation every machine request goes through (open now; slot edits, recipe selection and toggles
    // in 6.2 to 6.4). Server only. It resolves the machine at any tile of its footprint (creating and sharing a missing entity through
    // MachinePlacement.EnsureEntity), initializes its geometry and checks that the player is alive and inside reach.
    public static class MachineRequest
    {
        public static RequestCheck Validate(int player, int x, int y, float extraReachTiles, out BaseMachine machine, out Point16 corner)
        {
            machine = null;
            corner = new Point16((short)x, (short)y);

            if (Main.netMode != NetmodeID.Server || player < 0 || player >= Main.maxPlayers)
                return RequestCheck.Ignored;

            Player who = Main.player[player];
            if (who == null || !who.active || who.dead || !WorldGen.InWorld(x, y, 1))
                return RequestCheck.Ignored;

            machine = MachinePlacement.EnsureEntity(x, y);
            if (machine == null)
                return RequestCheck.Refused;

            corner = machine.Position;

            if (!machine.MachineInitialized)
                machine.InitializeGeometry();

            if (!MachineDetailSystem.WithinReach(who, machine, extraReachTiles))
                return RequestCheck.Refused;

            return RequestCheck.Ok;
        }
    }
}