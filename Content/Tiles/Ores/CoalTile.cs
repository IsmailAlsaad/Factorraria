using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Ores
{
    // Ore-style block that the vanilla Coal item places (the item is made placeable in VItemGlobalItem.SetDefaults).
    // Needs Content/Tiles/Ores/CoalTile.png: 288x270 merge sheet, same layout as SiliconOreTile.png.
    internal class CoalTile : ModTile
    {
        public override void SetStaticDefaults()
        {
            TileID.Sets.Ore[Type] = true;

            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileSpelunker[Type] = true;
            Main.tileOreFinderPriority[Type] = 200;

            AddMapEntry(new Color(45, 45, 50), CreateMapEntryName());

            DustType = DustID.Stone;
            RegisterItemDrop(ItemID.Coal);
            HitSound = SoundID.Tink;

            MineResist = 1f;
            MinPick = 0;
        }
    }
}