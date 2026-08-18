using Factorraria.Content.TileEntities;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace Factorraria.Content.Tiles.Conveyors
{
    internal class CounterClockwisePriorityConveyorTile : ModTile
    {
        public override string Texture => "Factorraria/Content/Tiles/Conveyors/PriorityConveyorTileSpriteSheet";
        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileFrameImportant[Type] = false;

            Main.tileMerge[Type][TileID.ConveyorBeltRight] = true;
            Main.tileMerge[TileID.ConveyorBeltRight][Type] = true;

            AnimationFrameHeight = 90;

            AddMapEntry(new Color(180, 180, 180));
        }
        public override void NumDust(int i, int j, bool fail, ref int num)
        {
            num = 0;
        }
        public override void AnimateTile(ref int frame, ref int frameCounter)
        {
            frameCounter++;
            if (frameCounter >= 4)
            {
                frameCounter = 0;

                frame--;

                if (frame < 0)
                {
                    frame = 3;
                }
            }
        }
    }
}