using Factorraria.Content.Items.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.BeeWheel
{
    public class BeeWheelTile : ModTile
    {
        // The base is drawn through MachineVisualOverride and its placement hologram by MachineOverhangSystem (registered in
        // MachineRegisterationSystem with a 5x5 canvas = the footprint, no overhang). The wheel (BeeWheel.png) is NOT part of this
        // texture: BeeWheelTileEntity draws it, and BeeWheelVisual.DrawHologram adds it to the placement preview.
        public override string Texture => "Factorraria/Content/Tiles/Machines/ElectricalProducers/BeeWheel/BeeWheel_Base";

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 5;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16, 16, 16 };
            TileObjectData.newTile.CoordinatePadding = 0;
            TileObjectData.newTile.Origin = new Point16(2, 4);   // the cursor tile is the middle of the bottom row
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, 5, 0);
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<BeeWheelTileEntity>().Hook_AfterPlacement, -1, 0, true);
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(205, 160, 50), CreateMapEntryName());

            DustType = DustID.Hive;
            RegisterItemDrop(ModContent.ItemType<BeeWheelItem>());
        }
    }
}