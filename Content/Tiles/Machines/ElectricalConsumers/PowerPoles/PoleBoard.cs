using Microsoft.Xna.Framework;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    // One swaying crossboard of a pole: its own PNG, drawn on top of the pole's footprint and rotated around its own centre.
    // Plain data (no textures), so the server can use it for link math.
    public class PoleBoard
    {
        public readonly string TexturePath;       // without extension
        public readonly Vector2 Size;             // the PNG's size in pixels (the server has no texture, so it is stated here; the client warns once if the PNG differs)
        public readonly Vector2[] LinkPoints;     // connection points as pixels inside the PNG, from its top-left
        public readonly Vector2 AttachOffset;     // where the board's centre sits, in pixels from the pole footprint's top-left corner

        // The board hangs from, and rotates around, its centre.
        public Vector2 Pivot => Size / 2f;

        // rowFromBottom: the tile row the board's centre sits on, counted from the bottom of the pole (0 = the bottom tile). The centre is
        // placed in the middle of the pole column (x 8) and the middle of that row.
        public PoleBoard(string texturePath, Vector2 size, int poleHeightTiles, int rowFromBottom, Vector2[] linkPoints)
        {
            TexturePath = texturePath;
            Size = size;
            LinkPoints = linkPoints;
            AttachOffset = new Vector2(8f, (poleHeightTiles - 1 - rowFromBottom) * 16f + 8f);
        }
    }
}