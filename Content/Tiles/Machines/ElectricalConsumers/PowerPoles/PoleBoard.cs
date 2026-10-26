using Microsoft.Xna.Framework;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    // One swaying crossboard of a pole. Its art is ONE texture sheet: a grid of equal cells (SheetColumns across, SheetRows down). PowerPoleCrossboardSystem
    // draws the board as several layers at the same position and rotation, each layer a different cell of the same sheet (column, row, 0-indexed):
    //   row 0: [0] base board (always) | [1] valid highlight, RIGHT point | [2] valid highlight, LEFT point | [3] invalid highlight, RIGHT point | [4] invalid highlight, LEFT point
    //   row 1: [0] copper wire on BOTH points | [1] copper on the RIGHT point | [2] copper on the LEFT point | [3] and [4] empty
    //   row 2: the same for tin     row 3: the same for gold
    // Plain data (no textures), so the server can use it for link math.
    public class PoleBoard
    {
        public readonly string TexturePath;       // the sheet, without extension
        public readonly Vector2 Size;             // ONE CELL in pixels (the server has no texture, so it is stated here; the client warns once if the sheet differs)
        public readonly int SheetColumns = 5;
        public readonly int SheetRows;            // the sheet is SheetColumns x SheetRows cells
        public readonly float TopPadding;         // transparent pixel rows added above the original board art in every cell; the pivot stays on the art's centre so nothing moved in the world
        public readonly Vector2[] LinkPoints;     // connection points as pixels inside ONE CELL, from its top-left (TopPadding included)
        public readonly Vector2 AttachOffset;     // where the pivot sits, in pixels from the pole footprint's top-left corner

        // The board hangs from, and rotates around, the centre of its art (the cell without the top padding).
        public Vector2 Pivot => new Vector2(Size.X / 2f, TopPadding + (Size.Y - TopPadding) / 2f);

        public Vector2 SheetSize => new Vector2(SheetColumns * Size.X, SheetRows * Size.Y);

        // rowFromBottom: the tile row the pivot sits on, counted from the bottom of the pole (0 = the bottom tile). The pivot is placed in the
        // middle of the pole column (x 8) and the middle of that row.
        public PoleBoard(string texturePath, Vector2 cellSize, float topPadding, int sheetRows, int poleHeightTiles, int rowFromBottom, Vector2[] linkPoints)
        {
            TexturePath = texturePath;
            Size = cellSize;
            TopPadding = topPadding;
            SheetRows = sheetRows;
            LinkPoints = linkPoints;
            AttachOffset = new Vector2(8f, (poleHeightTiles - 1 - rowFromBottom) * 16f + 8f);
        }

        // The source rectangle of one cell of the sheet.
        public Rectangle Cell(int column, int row) => new Rectangle(column * (int)Size.X, row * (int)Size.Y, (int)Size.X, (int)Size.Y);

        // True for the board's right connection point, false for its left one (decided by the point's x against the pivot).
        public bool IsRightPoint(int pointInBoard) => LinkPoints[pointInBoard].X > Pivot.X;
    }
}