using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Implemented by the Harvester Box row entity: draws one faded idle gear on a tile (world tile coordinates).
    public interface IHarvesterGearPreview
    {
        void DrawGearPreview(SpriteBatch spriteBatch, int tileX, int tileY, Color color);
    }

    // Vanilla's placement preview only draws the tile, so the gear is added on top of it (client only, nothing is synced): faded like the tile,
    // red when the box cannot go there (same rule as the machine hologram in MachineOverhangSystem).
    public class HarvesterBoxPlacementPreview : ModSystem
    {
        IHarvesterGearPreview previewer;
        bool searched;

        public override void Load()
        {
            On_TileObject.DrawPreview += DrawGearOnPreview;
        }

        public override void Unload()
        {
            previewer = null;
            searched = false;
        }

        void DrawGearOnPreview(On_TileObject.orig_DrawPreview orig, SpriteBatch sb, TileObjectPreviewData op, Vector2 position)
        {
            orig(sb, op, position);

            if (Main.dedServ || Main.gameMenu || !HarvesterBoxRows.IsBoxTile(op.Type))
                return;

            Player player = Main.LocalPlayer;
            Item held = player.HeldItem;
            if (held.IsAir || held.createTile != op.Type)
                return;

            if (!searched)
            {
                searched = true;
                foreach (ModTileEntity entity in ModContent.GetContent<ModTileEntity>())
                {
                    if (entity is IHarvesterGearPreview found)
                    {
                        previewer = found;
                        break;
                    }
                }
            }

            if (previewer == null)
                return;

            bool valid = player.IsTargetTileInItemRange(held)
                && TileObject.CanPlace(Player.tileTargetX, Player.tileTargetY, held.createTile, held.placeStyle, player.direction, out _, true);

            previewer.DrawGearPreview(sb, Player.tileTargetX, Player.tileTargetY, (valid ? Color.White : Color.Red) * 0.6f);
        }
    }
}
