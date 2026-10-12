using Factorraria.Content.Items.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.MechanicalAssembler
{
    public class MechanicalAssemblerTile : ModTile
    {
        // The tile is drawn through MachineVisualOverride and its placement hologram by MachineOverhangSystem (vanilla's
        // own preview is skipped for overhang machines), so the Off sheet only has to exist as the tile's texture.
        public override string Texture => "Factorraria/Content/Tiles/Machines/ElectricalConsumers/MechanicalAssembler/MechanicalAssembler_Off";

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<MechanicalAssemblerTileEntity>().Hook_AfterPlacement, -1, 0, true);
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(160, 160, 170), CreateMapEntryName());

            DustType = DustID.Iron;
            RegisterItemDrop(ModContent.ItemType<MechanicalAssemblerItem>());
        }
    }
}