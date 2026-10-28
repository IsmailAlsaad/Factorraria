using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
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
    }
}