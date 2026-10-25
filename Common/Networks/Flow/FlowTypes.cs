using System;

namespace Factorraria.Common.Networks.Flow
{
    // Pure data types of the liquid flow core (patch "liquid overhaul", step 1). Nothing in this folder may reference
    // Terraria / tModLoader types: the whole core is compiled and tested outside the game.
    // The order matches Factorraria.Common.Liquids.Direction (Up, Down, Left, Right), so the game layer can cast.
    public enum FlowDir : byte { Up = 0, Down = 1, Left = 2, Right = 3 }

    public static class FlowDirs
    {
        public static readonly int[] Dx = { 0, 0, -1, 1 };
        public static readonly int[] Dy = { -1, 1, 0, 0 };

        public static FlowDir Opposite(FlowDir d) => d switch
        {
            FlowDir.Up => FlowDir.Down,
            FlowDir.Down => FlowDir.Up,
            FlowDir.Left => FlowDir.Right,
            _ => FlowDir.Left
        };

        public static bool SameAxis(FlowDir a, FlowDir b) => a == b || a == Opposite(b);
    }

    // One tile of the network: a pipe or a motor. Motors are the only pumps.
    public struct FlowNode
    {
        public int X, Y;
        public bool IsMotor;
        public bool MotorOn;
        public FlowDir Facing;
        public float Strength;   // units per minute, only meaningful for an ON motor
    }

    // A tank-side endpoint attached to a node. Mouth = which way, from the node, the attached thing sits.
    // Owner groups the ports of one machine (-1 = none): a machine never pumps into itself.
    public struct FlowPort
    {
        public int Node;
        public FlowDir Mouth;
        public int Owner;
    }

    // One allowed source -> sink connection through at least one ON pump.
    public sealed class FlowRoute
    {
        public int Src, Dst;
        public int Climbs;           // tiles the liquid has to rise along the cheapest route
        public int[] Pumps;          // node indices of the ON motors on the route
        public float RatePerMinute;  // after climb loss and the network cap
        public float RatePerTick => RatePerMinute / 3600f;
    }

    public struct FlowTransfer
    {
        public int Route, Src, Dst, Type;
        public float Amount;
    }
}