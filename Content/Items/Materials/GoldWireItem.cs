using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Factorraria.Common;
using Factorraria.Common.Net;
using Factorraria.Common.Systems;

namespace Factorraria.Content.Items.Materials
{
    internal class GoldWireItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Blue;

            // Same use behaviour as CopperWireItem / TinWireItem: places gold custom wire on the targeted tile.
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

            if (CustomWireSystem.HasWire(tileX, tileY, CustomWireType.Gold))
            {
                return false;
            }

            CustomWireNet.RequestEdit(true, tileX, tileY, CustomWireType.Gold);

            SoundEngine.PlaySound(SoundID.Dig, new Vector2(tileX * 16, tileY * 16));

            return true;
        }
    }
}
