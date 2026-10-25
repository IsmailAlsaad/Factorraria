using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles
{
    // Shared setup of both pole tiles: a 1-wide, non-solid column that stands on a solid tile. Both are drawn through the overhang
    // system: wooden with a 3x14 canvas (a few pixels poke out left and right), platinum with a 1x16 canvas (no overhang, kept for consistency). Registered
    // in MachineRegisterationSystem. The crossboards are separate PNGs drawn by PowerPoleCrossboardSystem so they can sway in the wind.
    public abstract class PowerPoleTileBase : ModTile
    {
        protected abstract int PoleHeight { get; }                 // tiles (footprint height)
        protected abstract string ArtName { get; }
        public abstract PowerPoleTileEntityBase PlacementEntity { get; }   // the ModContent instance whose Hook_AfterPlacement runs after placing (also the data prototype the placement hologram reads)
        protected abstract int DropItemType { get; }
        protected abstract Color MapColor { get; }
        protected abstract int BreakDust { get; }

        public override string Texture => "Factorraria/Content/Tiles/Machines/ElectricalConsumers/PowerPoles/" + ArtName;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;   // walk-through: tileSolid stays false

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
            TileObjectData.newTile.Width = 1;
            TileObjectData.newTile.Height = PoleHeight;
            TileObjectData.newTile.CoordinateHeights = new int[PoleHeight];
            for (int k = 0; k < PoleHeight; k++)
                TileObjectData.newTile.CoordinateHeights[k] = 16;

            TileObjectData.newTile.Origin = new Point16(0, PoleHeight - 1);   // the cursor tile is the bottom tile
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, 1, 0);
            TileObjectData.newTile.DrawYOffset = 0;
            TileObjectData.newTile.CoordinatePadding = 0;   // plain 16px rows, matching the overhang canvases

            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(PlacementEntity.Hook_AfterPlacement, -1, 0, true);
            TileObjectData.addTile(Type);

            AddMapEntry(MapColor, CreateMapEntryName());

            DustType = BreakDust;
            RegisterItemDrop(DropItemType);
        }
    }
}