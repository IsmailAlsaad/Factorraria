using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.Extractinator
{
    /// <summary>
    /// Turns off the vanilla "right-click an extractable item onto the Extractinator" use. While the local player's cursor is
    /// over an Extractinator and the held item is extractable, that item's ExtractinatorMode is switched to "not extractable"
    /// for the duration of the player's update, so vanilla skips the extraction and the click opens the machine UI instead.
    /// </summary>
    public class ExtractinatorBlockPlayer : ModPlayer
    {
        int hiddenItemType = -1;
        int hiddenMode;

        public override void PreUpdate()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) return;

            int heldType = Player.HeldItem.type;
            if (heldType <= ItemID.None || ItemID.Sets.ExtractinatorMode[heldType] <= -1) return;

            int x = (int)(Main.MouseWorld.X / 16f);
            int y = (int)(Main.MouseWorld.Y / 16f);
            if (!WorldGen.InWorld(x, y, 2)) return;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || tile.TileType != TileID.Extractinator) return;

            hiddenItemType = heldType;
            hiddenMode = ItemID.Sets.ExtractinatorMode[heldType];
            ItemID.Sets.ExtractinatorMode[heldType] = -1;
        }

        public override void PostUpdate() => Restore();
        public override void OnEnterWorld() => Restore();

        void Restore()
        {
            if (hiddenItemType == -1) return;
            ItemID.Sets.ExtractinatorMode[hiddenItemType] = hiddenMode;
            hiddenItemType = -1;
        }
    }
}