using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    // Shared logic of both poles. A pole is an electric consumer with a small constant draw (transport loss), no slots and no recipes.
    // Authority: the server (and single-player) runs the power grid and decides isOn; clients get isOn / isWorking through the normal machine snapshot.
    public abstract class PowerPoleTileEntityBase : ElectricConsumerMachine
    {
        protected override int InputSlotCount => 0;
        protected override int OutputSlotCount => 0;

        // The swaying crossboards (wooden 1, platinum 2: upper then lower). Each one is its own PNG, drawn by PowerPoleCrossboardSystem on
        // top of the footprint. Link (connection) points live on the boards, as pixels inside each board's PNG.
        public abstract PoleBoard[] Boards { get; }

        // How much of the full wind sway this pole's boards get (1 = full). Platinum overrides it with 0.5.
        public virtual float SwayScale => 1f;

        // All link points of all boards, in board order, then point order inside the board. Used by the link patches (P3 onward).
        public int LinkPointCount
        {
            get
            {
                int count = 0;
                foreach (PoleBoard board in Boards)
                    count += board.LinkPoints.Length;
                return count;
            }
        }

        // Which board a flat link point index belongs to (returns the board index) and which point of that board it is.
        public int GetLinkPointBoardIndex(int index, out int pointInBoard)
        {
            PoleBoard[] boards = Boards;
            for (int b = 0; b < boards.Length; b++)
            {
                int count = boards[b].LinkPoints.Length;
                if (index < count)
                {
                    pointInBoard = index;
                    return b;
                }

                index -= count;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        // Resting world position of a link point (no sway). Deterministic on every process, so the server uses this one for span checks.
        public Vector2 GetLinkPointWorld(int index)
        {
            PoleBoard board = Boards[GetLinkPointBoardIndex(index, out int point)];
            return Position.ToVector2() * 16f + board.AttachOffset + (board.LinkPoints[point] - board.Pivot);
        }

        // Client only (drawing): the same point rotated around its board's centre by that board's current sway angle
        // (PowerPoleCrossboardSystem.GetSwayAngle(Position, GetLinkPointBoardIndex(index, out _), SwayScale)).
        public Vector2 GetLinkPointWorldSwayed(int index, float swayAngle)
        {
            PoleBoard board = Boards[GetLinkPointBoardIndex(index, out int point)];
            return Position.ToVector2() * 16f + board.AttachOffset + (board.LinkPoints[point] - board.Pivot).RotatedBy(swayAngle);
        }

        // A vanilla wire pulse that switches the pole off (the electricity icon) cuts its hanging wires out of the grid: the flood fill
        // (PowerGridSystem.EnqueueLinkedPoles / DrainLinkedPoles) skips a pole with WireToggle set, so the grid has to be rebuilt when it flips.
        protected override void OnWireToggled()
        {
            PowerGridSystem.gridNeedsRebuilding = true;
        }

        public override void Update()
        {
            base.Update();

            // BaseMachine only sets isWorking while a recipe runs, and a pole has none. PowerNetwork.Tick counts demand only from
            // machines that are working, so the pole sets it itself or its transport loss would never reach the grid.
            isWorking = true;
        }
    }
}