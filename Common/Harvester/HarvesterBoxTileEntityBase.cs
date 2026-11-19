using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Common.UI;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // The powered entity of a Harvester Box ROW: one entity for every box of the same kind touching left and right, at the row's leftmost box
    // (HarvesterBoxRows / HarvesterBoxRowSystem keep that true). It draws power and joins the grid through custom wire on ANY box of the row. The gears, the harvest
    // cycle and the drops come in later phases (docs/harvester_box/ROADMAP.md, 6c).
    public abstract class HarvesterBoxTileEntityBase<TTile> : ElectricConsumerMachine, IRowMachine where TTile : ModTile
    {
        public override int ValidTileType => ModContent.TileType<TTile>();

        public int RowFirstX => Position.X;

        public int RowLength => HarvesterBoxRows.RunLengthFrom(Position.X, Position.Y);

        public override float PowerDemand => 10f * System.Math.Max(1, RowLength);   // placeholder balance: 10 per box of the row

        public override void Update()
        {
            base.Update();
            isWorking = true;   // draws power the whole time it is placed (the Phase 2 gears turn whenever the box is powered)
        }

        // Single-player and server run this when the placing player is the one running the game (a multiplayer client only sends its request).
        // The row's entity is sorted out one tick later by HarvesterBoxRowSystem (joins, a new left end).
        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            int id = base.Hook_AfterPlacement(i, j, type, style, direction, alternate);
            HarvesterBoxRowSystem.MarkDirty(i, j);
            return id;
        }

        // Server: a client asked for an entity for the box it placed (message 87). A row has one entity: when this box joined a row that
        // already has one, nothing is created here; the reconcile moves or merges it if the new box became the left end.
        public override void NetPlaceEntityAttempt(int i, int j)
        {
            HarvesterBoxRowSystem.MarkDirty(i, j);

            if (HarvesterBoxRows.TryFindRowEntity(i, j, out _, out _))
                return;

            base.NetPlaceEntityAttempt(i, j);
        }

        // The entity sits at the row's first box, but the panel belongs on the tile that was clicked (MachineVisualOverride.RightClick records it).
        public override void OnRightClick(int i, int j)
        {
            if (!MachineUIRegistry.Definitions.ContainsKey(ValidTileType))
                return;

            Point16 clicked = RightClickedTile;
            if (clicked.Y != j || clicked.X < i || clicked.X >= i + RowLength)
                clicked = new Point16(i, j);

            ModContent.GetInstance<MachineUISystem>().OpenUI(clicked.X, clicked.Y, ValidTileType, this);
        }
    }
}