using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.SuperWeapons
{
    // Stone overlay of the Wall of Flesh shield (super weapons patch S1.2). CLIENT ONLY: nothing here is saved or sent; every client reads the
    // synced WallShieldGlobalNPC.ShieldUp of the wall NPCs and draws its own overlay.
    //
    // Why a detour and not GlobalNPC.PostDraw (V3): Main.DrawNPCs draws NPC indices from 199 down to 0, so a per-NPC hook cannot control the
    // order between the mouth and the eyes. The detour draws after the vanilla pass, in one fixed order: body strip, mouth, then eyes.
    //
    // Art (Ismail), all optional while testing. A missing PNG makes that part draw as a translucent grey block, so alignment can be checked first:
    //   Content/Tiles/SuperWeapons/WallShield_Body.png   replacement for the vanilla wall body texture, 190x420, repeated over the body strip
    //   Content/Tiles/SuperWeapons/WallShield_Mouth.png   same size and frame layout as vanilla NPC_113.png, drawn with the NPC's own frame
    //   Content/Tiles/SuperWeapons/WallShield_Eye.png   same size and frame layout as vanilla NPC_114.png, drawn with the NPC's own frame
    // /fshield status prints the exact sheet and frame sizes of both.
    public class WallShieldDrawSystem : ModSystem
    {
        // Tunables (instance fields, so hot reload can change them; /fshield flip and /fshield offset change them in game).
        public bool FlipBody;               // false: the body trails behind the mouth (opposite its direction); true: the other side
        public float BodyStartOffsetPx;     // moves the body strip's front edge away from the mouth's centre toward the body (negative = toward the mouth)
        public float FallbackAlpha = 0.65f; // opacity of the grey block drawn for a missing PNG
        public Color FallbackColor = new Color(96, 98, 106);

        Asset<Texture2D> body;
        Asset<Texture2D> mouth;
        Asset<Texture2D> eye;
        bool drawDisabled;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            ModContent.RequestIfExists<Texture2D>("Factorraria/Content/Tiles/SuperWeapons/WallShield_Body", out body);
            ModContent.RequestIfExists<Texture2D>("Factorraria/Content/Tiles/SuperWeapons/WallShield_Mouth", out mouth);
            ModContent.RequestIfExists<Texture2D>("Factorraria/Content/Tiles/SuperWeapons/WallShield_Eye", out eye);
            On_Main.DrawNPCs += DrawShieldsAfterNPCs;
        }

        public override void Unload()
        {
            if (Main.dedServ)
                return;

            On_Main.DrawNPCs -= DrawShieldsAfterNPCs;
        }

        /// <summary>What /fshield status prints about the art.</summary>
        public string ArtStatus()
        {
            return "body " + (body != null ? "found" : "missing") + ", mouth " + (mouth != null ? "found" : "missing") + ", eye " + (eye != null ? "found" : "missing");
        }

        /// <summary>
        /// The vertical span of the wall body in world pixels. Uses Main.wofDrawAreaTop / Bottom (the tiles-or-pixels unit is checked at runtime) and
        /// falls back to 600 px above and below the mouth. Returns false when the fallback was used.
        /// </summary>
        public bool TryGetBodySpan(Vector2 mouthCenter, out float top, out float bottom)
        {
            float t = Main.wofDrawAreaTop;
            float b = Main.wofDrawAreaBottom;
            if (b > t && b < Main.maxTilesY)
            {
                t *= 16f;   // tile units, not pixels
                b *= 16f;
            }

            if (b > t && b - t < 8000f && Math.Abs((t + b) * 0.5f - mouthCenter.Y) < 4000f)
            {
                top = t;
                bottom = b;
                return true;
            }

            top = mouthCenter.Y - 600f;
            bottom = mouthCenter.Y + 600f;
            return false;
        }

        void DrawShieldsAfterNPCs(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles)
        {
            orig(self, behindTiles);

            if (Main.dedServ || drawDisabled)
                return;

            bool any = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.behindTiles == behindTiles && WallShieldGlobalNPC.IsWallType(npc.type) && WallShieldGlobalNPC.IsShielded(npc))
                {
                    any = true;
                    break;
                }
            }

            if (!any)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool began = false;
            try
            {
                try
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    began = true;
                }
                catch (InvalidOperationException)
                {
                    // a batch is already open here: draw into it
                }

                DrawAll(spriteBatch, behindTiles);
            }
            catch (Exception e)
            {
                drawDisabled = true;
                Mod.Logger.Warn("WallShieldDrawSystem: drawing the shield failed and was switched off: " + e.Message);
            }
            finally
            {
                if (began)
                    spriteBatch.End();
            }
        }

        void DrawAll(SpriteBatch spriteBatch, bool behindTiles)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.behindTiles != behindTiles || npc.type != Terraria.ID.NPCID.WallofFlesh || !WallShieldGlobalNPC.IsShielded(npc))
                    continue;

                DrawBody(spriteBatch, npc);
                DrawSheetOverlay(spriteBatch, npc, mouth);
            }

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.behindTiles != behindTiles || npc.type != Terraria.ID.NPCID.WallofFleshEye || !WallShieldGlobalNPC.IsShielded(npc))
                    continue;

                DrawSheetOverlay(spriteBatch, npc, eye);
            }
        }

        // The strip from the mouth to the edge of the screen, between the wall's top and bottom.
        void DrawBody(SpriteBatch spriteBatch, NPC mouthNpc)
        {
            TryGetBodySpan(mouthNpc.Center, out float top, out float bottom);

            float zoom = Main.GameViewMatrix.Zoom.X;
            if (zoom < 0.01f)
                zoom = 1f;
            float halfWidth = Main.screenWidth / zoom * 0.5f + 64f;
            float halfHeight = Main.screenHeight / zoom * 0.5f + 64f;
            float centerX = Main.screenPosition.X + Main.screenWidth * 0.5f;
            float centerY = Main.screenPosition.Y + Main.screenHeight * 0.5f;

            int side = FlipBody ? mouthNpc.direction : -mouthNpc.direction;
            if (side == 0)
                side = -1;

            float edge = mouthNpc.Center.X + side * BodyStartOffsetPx;
            float left;
            float right;
            if (side < 0)
            {
                left = centerX - halfWidth;
                right = Math.Min(edge, centerX + halfWidth);
            }
            else
            {
                left = Math.Max(edge, centerX - halfWidth);
                right = centerX + halfWidth;
            }

            top = Math.Max(top, centerY - halfHeight);
            bottom = Math.Min(bottom, centerY + halfHeight);
            if (right <= left || bottom <= top)
                return;

            Texture2D tile = body != null ? body.Value : null;
            if (tile == null)
            {
                DrawBlock(spriteBatch, (int)left, (int)top, (int)right, (int)bottom);
                return;
            }

            // tiled, anchored to world coordinates so the pattern does not slide while the wall moves
            int tileWidth = tile.Width;
            int tileHeight = tile.Height;
            int l = (int)left;
            int t = (int)top;
            int r = (int)right;
            int b = (int)bottom;
            for (int y = (int)Math.Floor(t / (double)tileHeight) * tileHeight; y < b; y += tileHeight)
            {
                for (int x = (int)Math.Floor(l / (double)tileWidth) * tileWidth; x < r; x += tileWidth)
                {
                    int destLeft = Math.Max(x, l);
                    int destTop = Math.Max(y, t);
                    int destRight = Math.Min(x + tileWidth, r);
                    int destBottom = Math.Min(y + tileHeight, b);
                    if (destRight <= destLeft || destBottom <= destTop)
                        continue;

                    Rectangle source = new Rectangle(destLeft - x, destTop - y, destRight - destLeft, destBottom - destTop);
                    spriteBatch.Draw(tile, new Vector2(destLeft, destTop) - Main.screenPosition, source, Color.White);
                }
            }
        }

        // The mouth and the eyes: the overlay sheet has the layout of the vanilla sheet, so the NPC's current frame picks the same part.
        void DrawSheetOverlay(SpriteBatch spriteBatch, NPC npc, Asset<Texture2D> sheet)
        {
            if (sheet == null)
            {
                Rectangle hitbox = npc.Hitbox;
                DrawBlock(spriteBatch, hitbox.Left, hitbox.Top, hitbox.Right, hitbox.Bottom);
                return;
            }

            Texture2D texture = sheet.Value;
            Rectangle frame = npc.frame;
            if (frame.Width <= 0 || frame.Height <= 0 || frame.Right > texture.Width || frame.Bottom > texture.Height)
                frame = new Rectangle(0, 0, texture.Width, texture.Height);

            Vector2 position = npc.Center - Main.screenPosition + new Vector2(0f, npc.gfxOffY);
            Vector2 origin = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);
            SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color light = Lighting.GetColor((int)(npc.Center.X / 16f), (int)(npc.Center.Y / 16f));
            spriteBatch.Draw(texture, position, frame, light, npc.rotation, origin, npc.scale, effects, 0f);
        }

        // A translucent grey block over a world rectangle (the stand-in for missing art). MagicPixel is drawn with a 1x1 source, see PROJECT_CONTEXT.
        void DrawBlock(SpriteBatch spriteBatch, int left, int top, int right, int bottom)
        {
            Rectangle destination = new Rectangle((int)(left - Main.screenPosition.X), (int)(top - Main.screenPosition.Y), right - left, bottom - top);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, destination, new Rectangle(0, 0, 1, 1), FallbackColor * FallbackAlpha);
        }
    }
}