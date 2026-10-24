using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Factorraria.Common.Net;
using Factorraria.Common.Systems;
using Factorraria.Common;
using Terraria.DataStructures;

namespace Factorraria.Content.Items.Wires
{
    public class CopperWireItem : ModItem
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
        public override bool CanUseItem(Player player)
        {
            // Out of reach: refuse the use so the swing never starts and nothing is consumed. Only the owner has a meaningful
            // tileTargetX/Y; other copies of the player (server, other clients) must not be blocked by it.
            if (!FactorrariaNet.IsOwner(player))
                return true;

            return player.IsTargetTileInItemRange(Item);
        }

        public override bool? UseItem(Player player)
        {
            // Player.tileTargetX/Y is the LOCAL mouse tile, so only the owner may read it. Every other copy of this player (on the
            // server and on the other clients) does nothing: the wire reaches them from the server (WireChanged).
            if (!FactorrariaNet.IsOwner(player))
                return null;

            int tileX = Player.tileTargetX;
            int tileY = Player.tileTargetY;

            if (CustomWireSystem.HasWire(tileX, tileY, CustomWireType.Copper)) 
            {
                return false;
            }

            CustomWireNet.RequestEdit(true, tileX, tileY, CustomWireType.Copper);

            SoundEngine.PlaySound(SoundID.Dig,new Vector2(tileX * 16, tileY * 16));

            return true;
        }
    }
}
