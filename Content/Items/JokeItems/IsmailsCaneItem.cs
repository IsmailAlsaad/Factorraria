using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.JokeItems
{
    public class IsmailsCaneItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;

            Item.holdStyle = ItemHoldStyleID.HoldFront;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 20;
            Item.useAnimation = 20;
        }

        public override void HoldStyle(Player player, Rectangle heldItemFrame)
        {
            Vector2 Offset = new Vector2(-16f * player.direction, 4f).RotatedBy(player.itemRotation);
            player.itemLocation += Offset;
        }
    }
}
