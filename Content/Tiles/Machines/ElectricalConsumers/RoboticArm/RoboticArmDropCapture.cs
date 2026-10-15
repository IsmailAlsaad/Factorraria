using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // Holds which arm (if any) is currently striking. A ModSystem because a plain GlobalItem may not have instance fields
    // (tModLoader refuses to load it); the GlobalItem below stays stateless and just asks this system.
    public class RoboticArmDropCaptureSystem : ModSystem
    {
        public RoboticArmTileEntity Active { get; private set; }

        // Runs one strike with this arm as the capture target. Never nests; always cleared, even if the strike throws.
        public void Run(RoboticArmTileEntity arm, Action strike)
        {
            Active = arm;
            try
            {
                strike();
            }
            finally
            {
                Active = null;
            }
        }

        public override void Unload()
        {
            Active = null;
        }
    }

    // Catches the items a tile, wall or tree break spawns and hands them to the arm, which turns them into vItems where it should.
    // Scoped: it only listens while an arm's strike is running (RoboticArmDropCaptureSystem.Run), because the drops are all spawned
    // synchronously inside the strike call and the spike showed they are NOT keyed by the target's coordinates (a felled tree reports
    // one source per trunk tile).
    public class RoboticArmDropCapture : GlobalItem
    {
        public override void OnSpawn(Item item, IEntitySource source)
        {
            RoboticArmTileEntity active = ModContent.GetInstance<RoboticArmDropCaptureSystem>().Active;
            if (active == null || item.IsAir)
                return;

            if (source is not (EntitySource_TileBreak or EntitySource_ShakeTree))
                return;

            active.RouteDrop(item);
        }
    }
}