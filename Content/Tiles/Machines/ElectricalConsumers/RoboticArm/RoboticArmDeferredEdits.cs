using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // WorldGen.UpdateWorld_Inner loops over TileEntity.ByID.Values and calls Update() on each entity. An arm's strike can place or
    // break a tile that owns a tile entity (another machine, a chest, even another Robotic Arm), and that adds to / removes from
    // ByID in the middle of the loop ("Collection was modified"). So the arm only queues its world edits here; they run in
    // PostUpdateEverything, after the whole world update (and its entity loop) is finished. Runs on the server and in single-player,
    // the same places the worker runs.
    public class RoboticArmDeferredEditSystem : ModSystem
    {
        readonly Queue<Action> edits = new Queue<Action>();

        public void Enqueue(Action edit) => edits.Enqueue(edit);

        public override void PostUpdateEverything()
        {
            while (edits.Count > 0)
            {
                Action edit = edits.Dequeue();
                try
                {
                    edit();
                }
                catch (Exception e)
                {
                    Mod.Logger.Error("Robotic Arm deferred world edit failed: " + e);
                }
            }
        }

        public override void ClearWorld()
        {
            edits.Clear();
        }

        public override void Unload()
        {
            edits.Clear();
        }
    }
}