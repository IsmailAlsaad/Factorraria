using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Machines
{
    // Vanilla wires flip electric consumers and producers (BaseMachine.ToggleByWire). Wiring only runs on the server and in
    // single-player, so this is server-authoritative: the new state reaches clients through the machine's normal snapshot, light and
    // detail sync (flag bit 4). General machines (Furnace and so on) are left alone: CanBeWireToggled is false for them.
    //
    // PreHitWire runs for every wired tile before vanilla's own handling, whatever the tile type, so it also covers vanilla tiles that
    // carry a machine (Autohammer, Extractinator, Steampunk Boiler, ...). It never blocks the vanilla code (always returns true).
    public class MachineWireGlobalTile : GlobalTile
    {
        public override bool PreHitWire(int i, int j, int type)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return true;

            if (!TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 corner))
                return true;

            if (!(entity is BaseMachine machine) || !machine.CanBeWireToggled)
                return true;

            // Skip every tile of the machine so one pulse cannot run this once per wired tile (the same pattern the statues use).
            // ToggleByWire also allows only one flip per tick, so this is a second safety net.
            TileObjectData data = TileObjectData.GetTileData(machine.ValidTileType, 0);
            int width = data != null ? data.Width : 1;
            int height = data != null ? data.Height : 1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    Wiring.SkipWire(corner.X + x, corner.Y + y);
            }

            machine.ToggleByWire();
            return true;
        }
    }
}