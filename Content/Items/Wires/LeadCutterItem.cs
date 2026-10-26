using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Factorraria.Common.Net;

namespace Factorraria.Content.Items.Wires
{
    public class LeadCutterItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.value = Item.buyPrice(copper: 50);

            Item.UseSound = SoundID.Item1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 10;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
        }

        public override bool? UseItem(Player player)
        {
            // Owner only (Player.tileTargetX/Y is the local mouse tile). The owner picks which wire to cut from its own copy of the
            // wire grid (kept complete by join sync) and asks the server; the server removes it, spawns the returned wire item once
            // and tells everyone else.
            if (!FactorrariaNet.IsOwner(player))
                return null;

            // Pole links first (P7): the cursor on a hanging wire, or on a connection point that carries one, cuts that link (the server refunds it
            // and answers; reach is the server's call, same as for tile wires). A ground wire under the cursor still wins when it is closer.
            if (PoleLinkInputSystem.TryRequestCutAtCursor(player))
                return false;

            int tileX = Player.tileTargetX;
            int tileY = Player.tileTargetY;

            if (CustomWireSystem.HasWire(tileX, tileY, CustomWireType.Gold))
            {
                CustomWireNet.RequestEdit(false, tileX, tileY, CustomWireType.Gold);
                SoundEngine.PlaySound(SoundID.Dig, new Vector2(tileX, tileY) * 16f);

                return false;
            }
            if (CustomWireSystem.HasWire(tileX, tileY, CustomWireType.Tin))
            {
                CustomWireNet.RequestEdit(false, tileX, tileY, CustomWireType.Tin);
                SoundEngine.PlaySound(SoundID.Dig, new Vector2(tileX, tileY) * 16f);

                return false;
            }
            if (CustomWireSystem.HasWire(tileX, tileY, CustomWireType.Copper))
            {
                CustomWireNet.RequestEdit(false, tileX, tileY, CustomWireType.Copper);
                SoundEngine.PlaySound(SoundID.Dig, new Vector2(tileX, tileY) * 16f);

                return false;
            }

            return true;
        }
    }
}

