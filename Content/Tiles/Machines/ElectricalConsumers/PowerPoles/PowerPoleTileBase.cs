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
    // Shared setup of both pole tiles: a 1-wide, non-solid column that stands on a solid tile. The tile is drawn through the overhang
    // system (registered in MachineRegisterationSystem), so the Off sheet only has to exist as the tile's texture.
    public abstract class PowerPoleTileBase : ModTile
    {
        protected abstract int PoleHeight { get; }                 // tiles (footprint height)
        protected abstract string ArtName { get; }
        protected abstract BaseMachine PlacementEntity { get; }    // the ModContent instance whose Hook_AfterPlacement runs after placing
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

            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(PlacementEntity.Hook_AfterPlacement, -1, 0, true);
            TileObjectData.addTile(Type);

            AddMapEntry(MapColor, CreateMapEntryName());

            DustType = BreakDust;
            RegisterItemDrop(DropItemType);
        }
    }
}