using Factorraria.Common.Machines;
using Factorraria.Content.Configs;
using Factorraria.Content.Items.Mounts;
using Factorraria.Content.Projectiles.Mounts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.Mounts
{
    // EnableDebugs overlay of the Mech Suit (client-only drawing, nothing is synced or saved). For every suited player on screen:
    //   yellow circle  = search radius around the player (how far an arm looks for an anchor tile)
    //   red circle     = cutoff radius around the CLOSEST latched anchor (the player cannot leave it while that arm is the closest)
    //   orange circles = deadzone radius around every arm's anchor (no new arm anchors inside one)
    //   lines          = player to each arm anchor, lime when latched and gray while still flying
    // plus a small text block with the live values. All radii are read from the player's MechSuitPlayer, so changing them there (hot
    // reload) shows up here at once. It only draws while EnableDebugs is on in the Factorraria mod config.
    public class MechSuitDebugSystem : ModSystem
    {
        const int CircleSegments = 48;

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            if (config == null || !config.EnableDebugs)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool begun = false;

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (!player.active || player.dead)
                    continue;

                MechSuitPlayer suit = player.GetModPlayer<MechSuitPlayer>();
                if (!suit.IsActive || !BaseMachine.IsOnScreen(player.Center, 900f))
                    continue;

                if (!begun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }

                DrawSuit(spriteBatch, player, suit);
            }

            if (begun)
                spriteBatch.End();
        }

        static void DrawSuit(SpriteBatch spriteBatch, Player player, MechSuitPlayer suit)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            DrawCircle(spriteBatch, pixel, player.Center, suit.SearchRadiusPixels, Color.Yellow * 0.6f);

            int armType = ModContent.ProjectileType<MechSuitArmProjectile>();
            int arms = 0;
            int latched = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.type != armType || projectile.owner != player.whoAmI)
                    continue;
                if (!(projectile.ModProjectile is MechSuitArmProjectile arm))
                    continue;

                arms++;
                Vector2 anchor = new Vector2(arm.AnchorTile.X * 16 + 8, arm.AnchorTile.Y * 16 + 8);
                bool isLatched = arm.Latched;
                if (isLatched)
                    latched++;

                DrawLine(spriteBatch, pixel, player.Center, anchor, isLatched ? Color.Lime * 0.8f : Color.Gray * 0.8f, 1f);
                DrawCircle(spriteBatch, pixel, anchor, suit.DeadzoneRadiusPixels, Color.Orange * 0.7f);
                spriteBatch.Draw(pixel, anchor - Main.screenPosition, new Rectangle(0, 0, 1, 1), isLatched ? Color.Lime : Color.Gray, 0f, new Vector2(0.5f, 0.5f), 6f, SpriteEffects.None, 0f);
            }

            bool movementOn = suit.TryGetClosestLatchedAnchor(out Vector2 closest);
            float closestTiles = 0f;
            if (movementOn)
            {
                DrawCircle(spriteBatch, pixel, closest, suit.CutoffRadiusPixels, Color.Red * 0.7f);
                closestTiles = Vector2.Distance(player.Center, closest) / 16f;
            }

            Vector2 textPos = player.Center - Main.screenPosition + new Vector2(-70f, -110f);
            Utils.DrawBorderString(spriteBatch, "MechSuit debug: " + (movementOn ? "mount movement ON" : "mount movement OFF (regular physics)"), textPos, movementOn ? Color.Lime : Color.OrangeRed, 0.8f);
            Utils.DrawBorderString(spriteBatch, "arms " + arms + "/" + MechSuitPlayer.MaxArms + "  latched " + latched
                + (movementOn ? "  closest " + closestTiles.ToString("0.0") + " tiles" : ""), textPos + new Vector2(0f, 16f), Color.White, 0.8f);
            Utils.DrawBorderString(spriteBatch, "search " + suit.SearchRadiusTiles.ToString("0.#") + "  cutoff " + suit.CutoffRadiusTiles.ToString("0.#")
                + "  deadzone " + suit.DeadzoneRadiusTiles.ToString("0.#") + " (tiles)  speed " + suit.MoveSpeed.ToString("0.#"), textPos + new Vector2(0f, 32f), Color.LightGray, 0.8f);
        }

        static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, Color color, float thickness)
        {
            Vector2 delta = to - from;
            float length = delta.Length();
            if (length < 0.5f)
                return;

            spriteBatch.Draw(pixel, from - Main.screenPosition, new Rectangle(0, 0, 1, 1), color, delta.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        static void DrawCircle(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color)
        {
            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= CircleSegments; i++)
            {
                float angle = MathHelper.TwoPi * i / CircleSegments;
                Vector2 next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                DrawLine(spriteBatch, pixel, previous, next, color, 1f);
                previous = next;
            }
        }
    }
}