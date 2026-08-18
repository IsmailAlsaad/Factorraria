using Factorraria.Common;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Wires
{
    public class TinWire : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.value = Item.buyPrice(copper: 50);

            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 5;
            Item.useTime = 5;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
        }

        public override bool? UseItem(Player player)
        {
            int tileX = Player.tileTargetX;
            int tileY = Player.tileTargetY;

            if (CustomWireSystem.HasWire(tileX, tileY, CustomWireType.Tin))
            {
                return false;
            }

            CustomWireSystem.AddWire(tileX, tileY, CustomWireType.Tin);

            SoundEngine.PlaySound(SoundID.Dig, new Vector2(tileX * 16, tileY * 16));

            return true;
        }
    }
}
