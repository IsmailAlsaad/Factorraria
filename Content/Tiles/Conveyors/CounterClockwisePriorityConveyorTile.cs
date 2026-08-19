using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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

        public override bool RightClick(int i, int j)
        {
            Player player = Main.LocalPlayer;
            Item heldItem = player.HeldItem;

            // Reject empty hand / air items
            if (heldItem == null || heldItem.IsAir || heldItem.type == ItemID.None)
            {
                return false;
            }

            // Apply filter across entire network group
            if (VirtualItemSystem.SetConveyorFilter(i, j, heldItem.type))
            {
                // Visual feedback sound
                Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, new Microsoft.Xna.Framework.Vector2(i * 16, j * 16));
                return true;
            }

            return false;
        }

        public override void PlaceInWorld(int i, int j, Item item)
        {
            VirtualItemSystem.OnPriorityConveyorPlaced(i, j);
        }

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!fail && !effectOnly)
            {
                VirtualItemSystem.OnPriorityConveyorKilled(i, j);
            }
        }
    }
}