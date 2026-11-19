namespace Factorraria.Common.Machines
{
    // A machine whose ONE entity covers a run of tiles that is worked out from the tiles themselves (today: the Harvester Box row, one
    // entity for every same-kind box touching left and right). The entity sits at the first tile of the run. PowerGridSystem reads the
    // whole span for wire connections, so a wire on any tile of the run connects the machine.
    public interface IRowMachine
    {
        int RowFirstX { get; }

        // Tiles from RowFirstX to the right end of the run (0 when the tile is gone).
        int RowLength { get; }
    }
}