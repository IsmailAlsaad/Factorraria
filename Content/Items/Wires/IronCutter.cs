using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Wires
{
    public class IronCutter : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.value = Item.buyPrice(copper: 50);

            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 5;
            Item.useTime = 5;
            Item.useStyle = ItemUseStyleID.Swing;
        }

        public override bool? UseItem(Player player)
        {
            int tileX = Player.tileTargetX;
            int tileY = Player.tileTargetY;

            if (CustomWireSystem.RemoveWire(tileX, tileY, CustomWireType.Tin))
            {
                Item.NewItem(
                    player.GetSource_ItemUse(Item),
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
                    player.GetSource_ItemUse(Item),
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

