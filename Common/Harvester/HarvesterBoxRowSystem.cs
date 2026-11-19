using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Keeps ONE tile entity per Harvester Box row, at the row's leftmost box (docs/harvester_box/ROADMAP.md, 6c).
    //
    // Server and single-player (authority): placing a box, mining a box and world load only MARK a position dirty. One tick later (the kill
    // hook runs before the tile is gone) the marked runs are reconciled: extra entities are killed, a missing one is created at the left end
    // through MachinePlacement.ServerPlace (which shares it with the clients), and the wire switch state is carried over (off wins when rows
    // join, a split-off part inherits the state). Every 300 ticks a cheap check marks any entity that is not at the left end of its run or
    // shares its run with another entity. World load marks every entity once, which folds the one-entity-per-box rows saved by Phase 1.
    //
    // Multiplayer client: creates nothing. Every 20 ticks it removes any replica that has another replica to its left in the same run (the
    // server killed it in a merge; nothing tells the client) and refreshes the geometry of the rest (a client never runs Update).
    public class HarvesterBoxRowSystem : ModSystem
    {
        // The dirty queue is also filled from NetPlaceEntityAttempt, which may run on the network thread.
        readonly object gate = new object();
        readonly List<(Point16 tile, uint due)> pending = new();
        readonly Dictionary<Point16, bool> carried = new();
        readonly List<Point16> dueNow = new();
        readonly List<BaseMachine> doomed = new();
        bool worldScanDue;

        public override void OnWorldLoad()
        {
            lock (gate)
            {
                pending.Clear();
                carried.Clear();
            }

            worldScanDue = Main.netMode != NetmodeID.MultiplayerClient;
        }

        public override void OnWorldUnload()
        {
            lock (gate)
            {
                pending.Clear();
                carried.Clear();
            }

            worldScanDue = false;
        }

        // Safe to call from anywhere: does nothing on a multiplayer client.
        public static void MarkDirty(int x, int y, bool wireOff = false)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            ModContent.GetInstance<HarvesterBoxRowSystem>().Queue(x, y, wireOff);
        }

        void Queue(int x, int y, bool wireOff)
        {
            Point16 tile = new Point16(x, y);

            lock (gate)
            {
                pending.Add((tile, Main.GameUpdateCount + 1));
                if (wireOff)
                    carried[tile] = true;
            }
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                ClientTick();
            else
                ServerTick();
        }

        // ------------------------------------------------------------------ server and single-player

        void ServerTick()
        {
            uint now = Main.GameUpdateCount;

            if (worldScanDue)
            {
                worldScanDue = false;
                foreach (TileEntity te in TileEntity.ByID.Values)
                {
                    if (te is IRowMachine)
                        MarkDirty(te.Position.X, te.Position.Y);
                }
            }
            else if (now % 300 == 0)
            {
                foreach (TileEntity te in TileEntity.ByID.Values)
                {
                    if (te is IRowMachine && LooksWrong(te))
                        MarkDirty(te.Position.X, te.Position.Y);
                }
            }

            dueNow.Clear();
            lock (gate)
            {
                for (int n = pending.Count - 1; n >= 0; n--)
                {
                    if (pending[n].due > now)
                        continue;

                    dueNow.Add(pending[n].tile);
                    pending.RemoveAt(n);
                }
            }

            foreach (Point16 p in dueNow)
                Process(p);
        }

        // An entity that is not at the left end of its run, shares the run with another entity, or has lost its tile.
        static bool LooksWrong(TileEntity te)
        {
            int y = te.Position.Y;
            if (!HarvesterBoxRows.TryGetRun(te.Position.X, y, out int left, out int right, out _))
                return true;

            if (left != te.Position.X)
                return true;

            for (int k = left + 1; k <= right; k++)
            {
                if (TileEntity.ByPosition.TryGetValue(new Point16(k, y), out TileEntity other) && other is IRowMachine)
                    return true;
            }

            return false;
        }

        // The tile at p changed (a box was placed or mined there). The runs that can have changed are the ones through p - 1, p and p + 1.
        void Process(Point16 p)
        {
            bool wireOff;
            lock (gate)
                wireOff = carried.Remove(p, out bool value) && value;

            int y = p.Y;
            int lastLeft = int.MinValue;

            for (int dx = -1; dx <= 1; dx++)
            {
                if (!HarvesterBoxRows.TryGetRun(p.X + dx, y, out int left, out int right, out int tileType))
                    continue;

                if (left == lastLeft)
                    continue;   // the same run as the previous candidate

                lastLeft = left;
                ReconcileRun(left, right, y, tileType, wireOff);
            }

            // A row entity left on a tile that is no longer a box (a kill the hook did not see).
            if (TileEntity.ByPosition.TryGetValue(p, out TileEntity stray) && stray is IRowMachine && stray is BaseMachine strayMachine && !strayMachine.IsTileValidForEntity(p.X, p.Y))
                strayMachine.Kill(p.X, p.Y);
        }

        // Exactly one entity for the run left..right, at its left end, carrying the wire switch state of everything it replaces.
        void ReconcileRun(int left, int right, int y, int tileType, bool carriedOff)
        {
            List<BaseMachine> found = new();
            bool off = carriedOff;

            for (int k = left; k <= right; k++)
            {
                if (TileEntity.ByPosition.TryGetValue(new Point16(k, y), out TileEntity te) && te is IRowMachine && te is BaseMachine machine && machine.ValidTileType == tileType)
                {
                    found.Add(machine);   // left to right
                    off |= machine.WireToggle;
                }
            }

            BaseMachine keeper = found.Count > 0 && found[0].Position.X == left ? found[0] : null;
            int killed = 0;

            foreach (BaseMachine extra in found)
            {
                if (extra == keeper)
                    continue;

                extra.Kill(extra.Position.X, extra.Position.Y);
                killed++;
            }

            bool created = false;
            if (keeper == null)
            {
                keeper = MachinePlacement.ServerPlace(tileType, left, y);
                created = keeper != null;
            }

            if (keeper == null)
                return;   // not in the visual registry (cannot happen for a Harvester Box)

            keeper.WireToggle = off;
            HarvesterBoxRows.RefreshGeometry(keeper);

            if ((created || killed > 0) && Main.netMode == NetmodeID.Server)
                FactorrariaNet.Log("[Harvester] row at (" + left + ", " + y + ") length " + (right - left + 1) + ": " + (created ? "entity created" : "entity kept") + ", " + killed + " extra entity(ies) removed");
        }

        // ------------------------------------------------------------------ multiplayer client

        void ClientTick()
        {
            if (Main.GameUpdateCount % 20 != 0)
                return;

            doomed.Clear();

            foreach (TileEntity te in TileEntity.ByID.Values)
            {
                if (te is not IRowMachine || te is not BaseMachine machine)
                    continue;

                // The tile is not here (yet): leave the replica alone, the section data or the tile square may still be on its way.
                if (!HarvesterBoxRows.TryGetRun(te.Position.X, te.Position.Y, out int left, out _, out _))
                    continue;

                if (HarvesterBoxRows.HasRowEntityBefore(left, te.Position.X, te.Position.Y))
                    doomed.Add(machine);
                else
                    HarvesterBoxRows.RefreshGeometry(machine);
            }

            foreach (BaseMachine machine in doomed)
                machine.Kill(machine.Position.X, machine.Position.Y);

            doomed.Clear();
        }
    }
}