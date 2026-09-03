
using Microsoft.Xna.Framework;
using Terraria;

namespace Factorraria.Common.Liquids
{
    // Same relationship an Item has to InputSlots/OutputSlots — a small holder that says
    // "what kind of liquid, how much." Nothing fancier than that; it's data, not behavior.
    public class LiquidStack
    {
        // Which liquid this is — an ID handed out by LiquidTypeRegistry when a liquid type
        // registers itself. -1 means "empty, holds nothing right now."
        public int LiquidType = -1;

        // How much is currently held. Float, not int — lets flow rates move fractional
        // amounts per tick without everything rounding down to zero at low flow rates.
        public float Amount = 0f;

        public bool IsEmpty => LiquidType == -1 || Amount <= 0f;

        public LiquidStack() { }

        public LiquidStack(int liquidType, float amount)
        {
            LiquidType = liquidType;
            Amount = amount;
        }
    }

    public enum Direction { Up, Down, Left, Right }

    public static class DirectionExtensions
    {
        // Turns a Direction into a tile offset, e.g. Up -> (0, -1).
        public static Point ToOffset(this Direction dir) => dir switch
        {
            Direction.Up => new Point(0, -1),
            Direction.Down => new Point(0, 1),
            Direction.Left => new Point(-1, 0),
            Direction.Right => new Point(1, 0),
            _ => Point.Zero
        };

        public static Direction Rotate90(this Direction dir) => dir switch
        {
            Direction.Up => Direction.Right,
            Direction.Right => Direction.Down,
            Direction.Down => Direction.Left,
            Direction.Left => Direction.Up,
            _ => dir
        };

        // LiquidRegistries.cs — add to DirectionExtensions
        public static Direction Opposite(this Direction dir) => dir switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => dir
        };

        public static int ToMaskBit(this Direction dir) => dir switch
        {
            Direction.Up => 1,
            Direction.Down => 2,
            Direction.Left => 4,
            Direction.Right => 8,
            _ => 0
        };

        public static Direction FromOffset(Point offset) => offset switch
        {
            { X: 0, Y: -1 } => Direction.Up,
            { X: 0, Y: 1 } => Direction.Down,
            { X: -1, Y: 0 } => Direction.Left,
            { X: 1, Y: 0 } => Direction.Right,
            _ => Direction.Right
        };

        // Reads the mask ReframeSelf already wrote into TileFrameX and checks one side.
        public static bool IsPipeMouthOpen(int i, int j, Direction dir)
        {
            int mask = Main.tile[i, j].TileFrameX / 18;
            return (mask & dir.ToMaskBit()) != 0;
        }
    }
}
