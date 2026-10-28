using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // Cosmic patch C6b. Client only (a dedicated server never draws tiles). An ionized cloud block is drawn in the color of the particle
    // that ionized it: DrawEffects runs after vanilla has worked out the tile's final draw color, so the tint is multiplied into that.
    // Authority: none, this only changes how a tile looks. The tint comes from CosmicParticleSystem, which is fed by the server's events.
    public class CosmicIonizedTileDraw : GlobalTile
    {
        public override void DrawEffects(int i, int j, int type, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
        {
            if (Main.dedServ || !TileID.Sets.Clouds[type])
                return;

            if (!CosmicParticleSystem.Instance.TryGetTileTint(i, j, out Color tint))
                return;

            Color c = drawData.finalColor;
            drawData.finalColor = new Color(c.R * tint.R / 255, c.G * tint.G / 255, c.B * tint.B / 255, (int)c.A);
        }

        // C6c: vanilla draws a full block with smooth per-corner lighting and ignores finalColor, so DrawEffects above only changes slopes and
        // half blocks. A full block is therefore drawn again here, on top of itself, in the tint times the block's light.
        public override void PostDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (Main.dedServ || !TileID.Sets.Clouds[type])
                return;

            Tile tile = Main.tile[i, j];
            if (!tile.HasTile || tile.IsHalfBlock || tile.Slope != SlopeType.Solid || tile.IsActuated)
                return;

            if (!CosmicParticleSystem.Instance.TryGetTileTint(i, j, out Color tint))
                return;

            Color light = Lighting.GetColor(i, j);
            Color color = new Color(light.R * tint.R / 255, light.G * tint.G / 255, light.B * tint.B / 255, 255);
            Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition + zero;

            spriteBatch.Draw(TextureAssets.Tile[type].Value, position, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }
}