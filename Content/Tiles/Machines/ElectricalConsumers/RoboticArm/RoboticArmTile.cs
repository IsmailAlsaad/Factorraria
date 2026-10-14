using Factorraria.Content.Items.Hooks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    public class RoboticArmTile : ModTile
    {
        // The tile is drawn through MachineVisualOverride (registered in MachineRegisterationSystem), so the Off sheet
        // only has to exist as the tile's texture.
        public override string Texture => "Factorraria/Content/Tiles/Machines/ElectricalConsumers/RoboticArm/RoboticArm_Off";

        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileFrameImportant[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);

            // A real solid block: Style1x1 anchors to the tile below, which would make the arm pop off when that tile is mined.
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
            TileObjectData.newTile.AnchorTop = AnchorData.Empty;
            TileObjectData.newTile.AnchorLeft = AnchorData.Empty;
            TileObjectData.newTile.AnchorRight = AnchorData.Empty;
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<RoboticArmTileEntity>().Hook_AfterPlacement, -1, 0, true);
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(150, 150, 160), CreateMapEntryName());

            DustType = DustID.Iron;
            RegisterItemDrop(ModContent.ItemType<MechanicalArmItem>());
        }

        // Hammering must not slope or half-brick the base: the arm and its overlay assume a full block.
        public override bool Slope(int i, int j) => false;
    }
}