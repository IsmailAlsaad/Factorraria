using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Conveyors
{
    // Hides the sprite vanilla draws next to the cursor (Player.cursorItemIcon*: the held pickaxe/tool/item icon and its text)
    // while the cursor is over a priority conveyor tile. Client-only and purely visual: nothing is synced and no tile logic changes.
    // The layer sits right before "Vanilla: Interact Item Icon", after Player.Update has already set the flags, so it overrides whoever set them.
    public class PriorityConveyorCursorSystem : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.dedServ) return;

            int iconLayer = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Interact Item Icon"));
            if (iconLayer == -1) return;

            layers.Insert(iconLayer, new LegacyGameInterfaceLayer("Factorraria: Hide Cursor Icon On Priority Conveyor", delegate
            {
                HideIconIfOverConveyor();
                return true;
            }, InterfaceScaleType.UI));
        }

        private void HideIconIfOverConveyor()
        {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.mouseInterface) return;

            // tileTargetX/Y is the tile vanilla interacts with (follows Smart Cursor too), already zoom-corrected.
            Tile tile = Framing.GetTileSafely(Player.tileTargetX, Player.tileTargetY);
            if (!tile.HasTile) return;

            if (tile.TileType == ModContent.TileType<ClockwisePriorityConveyorTile>()
                || tile.TileType == ModContent.TileType<CounterClockwisePriorityConveyorTile>())
            {
                player.cursorItemIconEnabled = false;
                player.cursorItemIconID = 0;
                player.cursorItemIconText = string.Empty;
            }
        }
    }
}