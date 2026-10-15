using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // Catches the items a tile, wall or tree break spawns and puts them into an arm's output slot.
    // Scoped: it only listens while an arm's strike is running (Run), because the drops are all spawned synchronously inside the strike
    // call and the spike showed they are NOT keyed by the target's coordinates (a felled tree reports one source per trunk tile).
    public class RoboticArmDropCapture : GlobalItem
    {
        RoboticArmTileEntity active;

        // Runs one strike with this arm as the capture target. Never nests; always cleared, even if the strike throws.
        public void Run(RoboticArmTileEntity arm, Action strike)
        {
            active = arm;
            try
            {
                strike();
            }
            finally
            {
                active = null;
            }
        }

        public override void OnSpawn(Item item, IEntitySource source)
        {
            if (active == null || item.IsAir)
                return;

            if (source is not (EntitySource_TileBreak or EntitySource_ShakeTree))
                return;

            active.AbsorbDrop(item);
        }
    }
}