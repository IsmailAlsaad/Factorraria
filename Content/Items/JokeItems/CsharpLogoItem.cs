using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.JokeItems
{
    public class CsharpLogoItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 10;
            Item.height = 10;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            Rectangle frame = Main.itemAnimations[Item.type] != null
                ? Main.itemAnimations[Item.type].GetFrame(texture)
                : texture.Frame();

            Vector2 origin = frame.Size() / 2f;

            Vector2 drawPosition = Item.position + origin - Main.screenPosition;

            Vector2 customOffset = new Vector2(-63f, -85f);
            drawPosition += customOffset;

            float customScale = scale * 0.25f;

            spriteBatch.Draw(
                texture,
                drawPosition,
                frame,
                Item.GetAlpha(lightColor),
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            return false;
        }


        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            return base.PreDrawInInventory(spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }
    }
}
