using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Row helpers for the Harvester Boxes (docs/harvester_box/ROADMAP.md, 6c). A row is a maximal run of Harvester Box tiles of the SAME
    // kind touching left and right (the same rule the tile merge frames use). A row has ONE tile entity, at its leftmost box. Everything
    // about a row (length, geometry, power demand) is worked out from the tiles, never saved. Pure helpers, no state: the queue that
    // fixes entities after a place or a mine lives in HarvesterBoxRowSystem.
    public static class HarvesterBoxRows
    {
        public static bool IsBoxTile(int tileType) => HarvesterBoxRegistry.BoxTypes.Contains(tileType);

        static bool IsBoxAt(int x, int y, int tileType)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == tileType;
        }

        // The run that holds the box tile at x, y. false when that tile is not a Harvester Box.
        public static bool TryGetRun(int x, int y, out int left, out int right, out int tileType)
        {
            left = x;
            right = x;
            tileType = 0;

            if (!WorldGen.InWorld(x, y, 1))
                return false;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || !IsBoxTile(tile.TileType))
                return false;

            tileType = tile.TileType;
            while (IsBoxAt(left - 1, y, tileType))
                left--;
            while (IsBoxAt(right + 1, y, tileType))
                right++;

            return true;
        }

        // Boxes from x to the right end of the run (0 when there is no box at x, y).
        public static int RunLengthFrom(int x, int y)
        {
            return TryGetRun(x, y, out _, out int right, out _) ? right - x + 1 : 0;
        }

        // The entity of the row that holds the box at x, y: the first row entity from the left end of the run. Normally that is the one at the
        // left end itself; looking for the first one keeps this working in the moments before a reconcile has moved or merged entities.
        public static bool TryFindRowEntity(int x, int y, out TileEntity entity, out Point16 corner)
        {
            entity = null;
            corner = new Point16(x, y);

            if (!TryGetRun(x, y, out int left, out int right, out _))
                return false;

            for (int k = left; k <= right; k++)
            {
                if (TileEntity.ByPosition.TryGetValue(new Point16(k, y), out TileEntity found) && found is IRowMachine)
                {
                    entity = found;
                    corner = found.Position;
                    return true;
                }
            }

            return false;
        }

        // true when a row entity sits on a box tile from left to x - 1 (so the entity at x is not the row's first one).
        public static bool HasRowEntityBefore(int left, int x, int y)
        {
            for (int k = left; k < x; k++)
            {
                if (TileEntity.ByPosition.TryGetValue(new Point16(k, y), out TileEntity found) && found is IRowMachine)
                    return true;
            }

            return false;
        }

        // The geometry fields every machine has, set from the tiles (BaseMachine.InitializeGeometry only knows the 1x1 tile data).
        // Runs on every process: a client never runs Update, so HarvesterBoxRowSystem calls this for its replicas too.
        public static void RefreshGeometry(BaseMachine machine)
        {
            int length = RunLengthFrom(machine.Position.X, machine.Position.Y);
            if (length < 1)
                return;   // the tile is not here (yet): keep what the entity has

            machine.cornerPosition = machine.Position;
            machine.MachineWidth = length;
            machine.MachineHeight = 1;
            machine.MachineCenter = machine.Position.ToVector2() * 16f + new Vector2(length, 1) * 8f;
            machine.MachineInitialized = true;
        }

        // MachineRemoval.OnTileKilled hands every killed machine tile here first. Returns true when it was a Harvester Box tile (handled here),
        // false when it is another machine. The hook runs BEFORE the tile is removed.
        public static bool OnTileKilled(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return false;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || !IsBoxTile(tile.TileType))
                return false;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // A replica sitting exactly on the mined tile goes with it. The rest of the row keeps its replica; the server shares a new
                // one for a part that is split off.
                if (TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TileEntity replica) && replica is IRowMachine && replica is BaseMachine replicaMachine)
                    replicaMachine.Kill(x, y);

                return true;
            }

            // Server and single-player. The wire switch state goes to whatever row is left (a new entity for a split-off part inherits it).
            bool wireOff = false;
            if (TryFindRowEntity(x, y, out TileEntity rowEntity, out _) && rowEntity is BaseMachine rowMachine)
            {
                wireOff = rowMachine.WireToggle;

                // The entity sits on the tile that is being mined: it goes (OnKill leaves the power grid and closes any open panel).
                if (rowMachine.Position.X == x && rowMachine.Position.Y == y)
                    rowMachine.Kill(x, y);
            }

            HarvesterBoxRowSystem.MarkDirty(x, y, wireOff);
            return true;
        }
    }
}