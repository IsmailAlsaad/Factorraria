using Factorraria.Common.Machines;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Machine kill path (roadmap patch 4.4, finding M6). A broken machine tile reaches MachineVisualOverride.KillTile in EVERY process
    // that sees the break: the breaking client (it runs the break locally and tells the server), the server, and every other client
    // (the server's tile change makes them run the same break). Before this patch each of them dropped the slot contents and killed
    // the entity, and the server's copy could even be created just to be killed.
    //
    // Authority: the server (or single-player) owns the machine's contents and its entity.
    //  - server / single-player: drop the slot contents once, then remove the entity (BaseMachine.OnKill clears the power grid and
    //    liquid network entries and closes any open UI); nothing is created, a tile without an entity has nothing to drop;
    //  - multiplayer client: only remove the local replica (no drops, they come from the server; OnKill still closes the open UI).
    // Nothing is sent from here: the tile removal is already broadcast by vanilla, and the clients' own KillTile hook removes the replica.
    // Idempotent: a second call finds no entity and does nothing.
    public static class MachineRemoval
    {
        public static void OnTileKilled(int x, int y)
        {
            if (!TileEntityHelper.TryGetEntityFromTile(x, y, out TileEntity entity, out Point16 corner) || entity is not BaseMachine machine)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                machine.Kill(corner.X, corner.Y);
                return;
            }

            int id = machine.ID;
            string name = machine.GetType().Name;
            int stacks = 0;

            foreach (Item item in machine.InputSlots.Concat(machine.OutputSlots))
            {
                if (item.IsAir)
                    continue;

                Item.NewItem(new EntitySource_TileEntity(machine), corner.X * 16 + 16, corner.Y * 16 + 8, 16, 16, item);
                stacks++;
            }

            machine.Kill(corner.X, corner.Y);

            if (Main.netMode == NetmodeID.Server)
                FactorrariaNet.Log("[Machines] removed " + name + " at (" + corner.X + ", " + corner.Y + "), id " + id + ", dropped " + stacks + " stack(s)");
        }
    }
}