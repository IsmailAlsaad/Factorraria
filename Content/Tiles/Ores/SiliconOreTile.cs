using Factorraria.Content.Items.Materials;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Ores
{
    internal class SiliconOreTile : ModTile
    {
        public override void SetStaticDefaults()
        {
            TileID.Sets.Ore[Type] = true;

            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileShine[Type] = 900;
            Main.tileShine2[Type] = true;
            Main.tileSpelunker[Type] = true;
            Main.tileOreFinderPriority[Type] = 255;

            AddMapEntry(new Color(164, 195, 188), CreateMapEntryName());

            DustType = DustID.Glass;
            RegisterItemDrop(ModContent.ItemType<SiliconOreItem>());
            HitSound = SoundID.Tink;

            MineResist = 1.5f;
            MinPick = 45;
        }
    }
}
