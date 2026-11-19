using Terraria;

namespace Factorraria.Common.Machines
{
    // A machine that can be the target of the shared drop capture (RoboticArmDropCaptureSystem.Run): while it runs a tile break on its own
    // behalf, every item that break spawns is handed to RouteDrop, which may turn it into a vItem or leave it alone (it then drops into the
    // world like vanilla). Today: the Robotic Arm and the Harvester Boxes.
    public interface IDropCaptureTarget
    {
        void RouteDrop(Item item);
    }
}