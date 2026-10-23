using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Ores
{
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

            DustType = DustID.Obsidian;
            RegisterItemDrop(ItemID.Coal);
            HitSound = SoundID.Dig;

            MineResist = 1f;
            MinPick = 0;
        }
    }
}