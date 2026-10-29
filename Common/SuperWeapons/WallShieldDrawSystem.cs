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

        // What DrawBody used last (read by /fshield status). Debug only, removed once the body animation is settled.
        public bool DebugSpanOk;
        public float DebugTop;
        public float DebugBottom;
        public int DebugFrameValue;
        public int DebugFrameY;
        public int DebugRows;

        Asset<Texture2D> body;
        Asset<Texture2D> mouth;
        Asset<Texture2D> eye;
        bool drawDisabled;
        bool ownBatchInBehindPass;  // learned at runtime: false = vanilla leaves a SpriteBatch open in the behind-tiles pass (the normal case)

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

        /// <summary>What /fshield status prints about the body drawing: the raw vanilla values and what DrawBody did with them.</summary>
        public string BodyStatus()
        {
            string tex = body != null ? body.Value.Width + "x" + body.Value.Height : "missing";
            return "wofDrawAreaTop " + Main.wofDrawAreaTop + ", wofDrawAreaBottom " + Main.wofDrawAreaBottom + ", wofDrawFrameIndex " + Main.wofDrawFrameIndex
                + ", maxTilesY " + Main.maxTilesY + " | span " + (DebugSpanOk ? "from vanilla" : "FALLBACK (1200 px)") + " top " + DebugTop.ToString("0") + " bottom " + DebugBottom.ToString("0")
                + " (" + ((DebugBottom - DebugTop) / 16f).ToString("0") + " tiles) | frameValue " + DebugFrameValue + " -> frameY " + DebugFrameY + ", rows drawn " + DebugRows + ", texture " + tex;
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

            // Vanilla leaves a SpriteBatch open after the behind-tiles DrawNPCs (the wall NPCs are behindTiles) and closes it after the front pass.
            // Never call Begin into an open batch: even a caught InvalidOperationException is logged by tModLoader as a "Silently Caught Exception".
            SpriteBatch spriteBatch = Main.spriteBatch;
            bool needOwnBatch = !behindTiles || ownBatchInBehindPass;
            bool began = false;
            try
            {
                if (needOwnBatch)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    began = true;
                }

                DrawAll(spriteBatch, behindTiles);
            }
            catch (InvalidOperationException e)
            {
                if (behindTiles && !ownBatchInBehindPass)
                {
                    // no batch was open after all: use our own from the next frame on
                    ownBatchInBehindPass = true;
                    Mod.Logger.Info("WallShieldDrawSystem: no SpriteBatch is open after the behind-tiles pass, using its own. " + e.Message);
                }
                else
                {
                    drawDisabled = true;
                    Mod.Logger.Warn("WallShieldDrawSystem: drawing the shield failed and was switched off: " + e.Message);
                }
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

        // Replacement for the vanilla wall body, drawn the way Main.DrawWoF draws it: ONE column as wide as the texture at the wall's X
        // (80 px further left when the wall faces right), rows of 140 px anchored to the top of the wall, the animation frame picked by
        // wofDrawFrameIndex (the sheet is 3 frames of 140 px stacked), flipped with spriteDirection, lit per 16 px slice.
        void DrawBody(SpriteBatch spriteBatch, NPC mouthNpc)
        {
            DebugSpanOk = TryGetBodySpan(mouthNpc.Center, out float top, out float bottom);
            DebugTop = top;
            DebugBottom = bottom;

            float zoom = Main.GameViewMatrix.Zoom.X;
            if (zoom < 0.01f)
                zoom = 1f;
            float visibleTop = Main.screenPosition.Y + Main.screenHeight * 0.5f - Main.screenHeight / zoom * 0.5f - 32f;
            float visibleBottom = Main.screenPosition.Y + Main.screenHeight * 0.5f + Main.screenHeight / zoom * 0.5f + 32f;

            Texture2D tile = body != null ? body.Value : null;
            int width = tile != null ? tile.Width : 190;
            float x = mouthNpc.position.X;
            if (mouthNpc.direction > 0)
                x -= 80f;

            if (tile == null)
            {
                DrawBlock(spriteBatch, (int)x, (int)Math.Max(top, visibleTop), (int)x + width, (int)Math.Min(bottom, visibleBottom));
                return;
            }

            int rowHeight = 140;
            int frameValue = Main.wofDrawFrameIndex;
            int frameY = ((frameValue >= rowHeight ? frameValue / rowHeight : frameValue) % 3) * rowHeight;  // accepts a frame index (0 to 2) or a pixel offset (0 / 140 / 280)
            if (frameY + rowHeight > tile.Height)
                frameY = 0;
            DebugFrameValue = frameValue;
            DebugFrameY = frameY;
            DebugRows = 0;

            SpriteEffects effects = mouthNpc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            int lightX = (int)((x + width * 0.5f) / 16f);
            float firstRow = top + (float)Math.Floor((Math.Max(top, visibleTop) - top) / rowHeight) * rowHeight;
            for (float rowY = firstRow; rowY < Math.Min(bottom, visibleBottom); rowY += rowHeight)
            {
                DebugRows++;
                for (int dy = 0; dy < rowHeight; dy += 16)
                {
                    float drawY = rowY + dy;
                    if (drawY >= bottom || drawY >= visibleBottom)
                        break;
                    if (drawY + 16f < visibleTop)
                        continue;

                    Color light = Lighting.GetColor(lightX, (int)(drawY / 16f));
                    spriteBatch.Draw(tile, new Vector2(x, drawY) - Main.screenPosition, new Rectangle(0, frameY + dy, width, 16), light, 0f, Vector2.Zero, 1f, effects, 0f);
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