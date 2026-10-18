using Factorraria.Common;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Factorraria.Common.Net;

namespace Factorraria.Content.Items.Wires
{
    public class TinWireItem : ModItem
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
            // Player.tileTargetX/Y is the LOCAL mouse tile, so only the owner may read it. Every other copy of this player (on the
            // server and on the other clients) does nothing: the wire reaches them from the server (WireChanged).
            if (!FactorrariaNet.IsOwner(player))
                return null;

            int tileX = Player.tileTargetX;
            int tileY = Player.tileTargetY;

            if (CustomWireSystem.HasWire(tileX, tileY, CustomWireType.Tin))
            {
                return false;
            }

            CustomWireNet.RequestEdit(true, tileX, tileY, CustomWireType.Tin);

            SoundEngine.PlaySound(SoundID.Dig, new Vector2(tileX * 16, tileY * 16));

            return true;
        }
    }
}
