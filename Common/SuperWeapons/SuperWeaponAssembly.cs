using System;

namespace Factorraria.Common.SuperWeapons
{
    // Super weapons patch S3.1: the pure geometry check of the missile. NO Terraria types, so it is tested outside the game (tools/sw_tests).
    // The tank entity feeds it the world through a cell reader (Func<int, int, PartCell>) and stores the result.
    //
    // The weapon is anchored on the tank (4 wide x 3 tall, its top-left tile is tankX, tankY). It is valid when:
    //   - a drill (2 wide x 3 tall) has its top-left tile exactly at the tank's row, touching one end:
    //       drill on the right  -> origin (tankX + 4, tankY), the weapon faces right (+1)
    //       drill on the left   -> origin (tankX - 2, tankY), the weapon faces left  (-1)
    //   - the drill's own facing equals that side;
    //   - the three thruster tiles stand in the column touching the OPPOSITE end, one per tank row, each with the same facing.
    // Anything else standing around (extra thrusters, other tiles) is ignored. The first problem found is reported, in this order: the drill,
    // the drill's facing, then the thrusters top to bottom (missing first, then wrong facing).

    public enum SuperWeaponState : byte
    {
        Incomplete = 0,
        Assembled = 1,
        Primed = 2,      // S3.2: assembled and the tank holds a full 5000 Fuel
        Igniting = 3,    // S4.1
        Launched = 4     // S4.2
    }

    public enum SuperWeaponPartKind : byte
    {
        None = 0,
        Drill = 1,
        Thruster = 2
    }

    public enum AssemblyIssue : byte
    {
        None = 0,
        NoDrill = 1,              // no drill touches either end of the tank
        DrillRowOff = 2,          // a drill touches an end, but not on the tank's rows (IssueX/Y = where that drill stands)
        DrillsOnBothEnds = 3,
        DrillWrongFacing = 4,
        ThrusterMissing = 5,      // IssueRow = the tank row without a thruster
        ThrusterRowOff = 6,       // like ThrusterMissing, and a thruster stands just above or below the column
        ThrusterWrongFacing = 7   // IssueRow = the row whose thruster faces the other way
    }

    /// <summary>What the world holds at one tile, as far as the assembly cares. Facing is +1 right, -1 left (only meaningful for a part).</summary>
    public struct PartCell
    {
        public SuperWeaponPartKind Kind;
        public int Facing;
        public bool IsOrigin;   // the top-left tile of the part's footprint (always true for a 1x1 thruster)

        public PartCell(SuperWeaponPartKind kind, int facing, bool isOrigin)
        {
            Kind = kind;
            Facing = facing;
            IsOrigin = isOrigin;
        }
    }

    public struct AssemblyResult
    {
        public bool Assembled;
        public int Facing;            // +1 / -1 when a drill side was found (even if incomplete), 0 when unknown
        public AssemblyIssue Issue;
        public int IssueRow;          // thruster issues: tank row 0..2
        public int IssueX;            // absolute tile of the piece's expected (or offending) top-left tile; meaningful when Issue != None
        public int IssueY;
    }

    public static class SuperWeaponAssembly
    {
        public const int TankWidth = 4;
        public const int TankHeight = 3;
        public const int DrillWidth = 2;
        public const int DrillHeight = 3;

        /// <summary>Checks the weapon around a tank whose top-left tile is (tankX, tankY). getCell must tolerate any coordinate.</summary>
        public static AssemblyResult Evaluate(int tankX, int tankY, Func<int, int, PartCell> getCell)
        {
            var result = new AssemblyResult();
            int rightDrillX = tankX + TankWidth;
            int leftDrillX = tankX - DrillWidth;

            bool right = IsDrillOrigin(getCell(rightDrillX, tankY));
            bool left = IsDrillOrigin(getCell(leftDrillX, tankY));

            if (right && left)
            {
                result.Issue = AssemblyIssue.DrillsOnBothEnds;
                result.IssueX = leftDrillX;
                result.IssueY = tankY;
                return result;
            }

            if (!right && !left)
            {
                // A drill one or two rows off still touches the tank's end: say so instead of "no drill".
                for (int dy = -(DrillHeight - 1); dy <= TankHeight - 1; dy++)
                {
                    if (dy == 0)
                        continue;
                    if (IsDrillOrigin(getCell(rightDrillX, tankY + dy)))
                        return Fail(result, AssemblyIssue.DrillRowOff, 0, rightDrillX, tankY + dy);
                    if (IsDrillOrigin(getCell(leftDrillX, tankY + dy)))
                        return Fail(result, AssemblyIssue.DrillRowOff, 0, leftDrillX, tankY + dy);
                }

                // No drill at all: point at the end whose opposite end does not already hold a thruster column.
                bool thrustersOnRight = HasAnyThruster(tankX + TankWidth, tankY, getCell);
                bool thrustersOnLeft = HasAnyThruster(tankX - 1, tankY, getCell);
                int expectedX = (thrustersOnRight && !thrustersOnLeft) ? leftDrillX : rightDrillX;
                return Fail(result, AssemblyIssue.NoDrill, 0, expectedX, tankY);
            }

            int facing = right ? 1 : -1;
            int drillX = right ? rightDrillX : leftDrillX;
            result.Facing = facing;

            if (getCell(drillX, tankY).Facing != facing)
                return Fail(result, AssemblyIssue.DrillWrongFacing, 0, drillX, tankY);

            int thrusterX = right ? tankX - 1 : tankX + TankWidth;
            for (int row = 0; row < TankHeight; row++)
            {
                PartCell cell = getCell(thrusterX, tankY + row);
                if (cell.Kind != SuperWeaponPartKind.Thruster)
                {
                    bool strayAbove = getCell(thrusterX, tankY - 1).Kind == SuperWeaponPartKind.Thruster;
                    bool strayBelow = getCell(thrusterX, tankY + TankHeight).Kind == SuperWeaponPartKind.Thruster;
                    AssemblyIssue issue = (strayAbove || strayBelow) ? AssemblyIssue.ThrusterRowOff : AssemblyIssue.ThrusterMissing;
                    return Fail(result, issue, row, thrusterX, tankY + row);
                }

                if (cell.Facing != facing)
                    return Fail(result, AssemblyIssue.ThrusterWrongFacing, row, thrusterX, tankY + row);
            }

            result.Assembled = true;
            return result;
        }

        static bool IsDrillOrigin(PartCell cell)
        {
            return cell.Kind == SuperWeaponPartKind.Drill && cell.IsOrigin;
        }

        static bool HasAnyThruster(int x, int tankY, Func<int, int, PartCell> getCell)
        {
            for (int row = 0; row < TankHeight; row++)
                if (getCell(x, tankY + row).Kind == SuperWeaponPartKind.Thruster)
                    return true;
            return false;
        }

        static AssemblyResult Fail(AssemblyResult result, AssemblyIssue issue, int row, int x, int y)
        {
            result.Assembled = false;
            result.Issue = issue;
            result.IssueRow = row;
            result.IssueX = x;
            result.IssueY = y;
            return result;
        }

        /// <summary>One short line for the debug overlay and the tank UI (S3.2): what is wrong, or that the weapon is complete.</summary>
        public static string Describe(bool assembled, int facing, AssemblyIssue issue, int issueRow)
        {
            if (assembled)
                return "Assembled, facing " + (facing > 0 ? "right" : "left");

            switch (issue)
            {
                case AssemblyIssue.NoDrill: return "Missing: drill head at an end of the tank";
                case AssemblyIssue.DrillRowOff: return "Drill head is on the wrong rows (its top must line up with the tank's top)";
                case AssemblyIssue.DrillsOnBothEnds: return "Drill heads on both ends of the tank";
                case AssemblyIssue.DrillWrongFacing: return "Drill head faces the wrong way";
                case AssemblyIssue.ThrusterMissing: return "Missing: thruster, row " + (issueRow + 1);
                case AssemblyIssue.ThrusterRowOff: return "Thruster on the wrong row (missing at row " + (issueRow + 1) + ")";
                case AssemblyIssue.ThrusterWrongFacing: return "Thruster, row " + (issueRow + 1) + ", faces the wrong way";
                default: return "Incomplete";
            }
        }
    }
}