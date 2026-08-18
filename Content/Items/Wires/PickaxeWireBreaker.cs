using Factorraria.Common;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Wires
{
    public class PickaxeWireBreaker : GlobalItem
    {
        public override bool? UseItem(Item item, Player player)
        //public override bool CanUseItem(Item item, Player player)
        {
            if(item.pick == 0)
            {
                return null;
            }

            int tileX = Player.tileTargetX;
            int tileY = Player.tileTargetY;

            if (CustomWireSystem.RemoveWire(tileX, tileY, CustomWireType.Tin))
            {
                Item.NewItem(
                    player.GetSource_ItemUse(item),
                    tileX * 16,
                    tileY * 16,
                    16,
                    16,
                    ModContent.ItemType<Items.Wires.TinWire>()
                    );

                SoundEngine.PlaySound(SoundID.Dig, new Vector2(tileX, tileY) * 16f);

                return false;
            }
            if (CustomWireSystem.RemoveWire(tileX, tileY, CustomWireType.Copper))
            {
                Item.NewItem(
                    player.GetSource_ItemUse(item),
                    tileX * 16,
                    tileY * 16,
                    16,
                    16,
                    ModContent.ItemType<Items.Wires.CopperWire>()
                    );

                SoundEngine.PlaySound(SoundID.Dig, new Vector2(tileX, tileY) * 16f);
                return false;
            }

            return true;
        }
    }
}
