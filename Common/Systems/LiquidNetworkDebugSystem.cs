using Factorraria.Common.Liquids;
using Factorraria.Content.Configs;
using Factorraria.Common.Networks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{

    public class LiquidNetworkDebugSystem : ModSystem
    {
        public static bool EnableDebugView = true;

        // Same palette idea as PowerGridDebugSystem — cycles through so adjacent
        // networks are visually distinguishable even if there are more networks
        // than colors in the list.
        private static readonly Color[] NetworkColors = new Color[]
        {
        Color.Cyan,
        Color.Lime,
        Color.Orange,
        Color.Magenta,
        Color.Yellow,
        Color.DeepSkyBlue,
        Color.Violet
        };

        public override void PostDrawTiles()
        {
            // Reusing the same config flag PowerGridDebugSystem uses — one debug
            // toggle for both systems, since they're both "internal dev overlay" tools.
            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            EnableDebugView = config.EnableDebugs;

            if (!EnableDebugView)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;

            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            if (LiquidNetworkSystem.ActiveNetworks != null)
            {
                for (int i = 0; i < LiquidNetworkSystem.ActiveNetworks.Count; i++)
                {
                    LiquidNetwork network = LiquidNetworkSystem.ActiveNetworks[i];
                    Color networkColor = NetworkColors[i % NetworkColors.Length];

                    // 1. Draw every pipe tile belonging to this network, tinted its color —
                    //    this is the main "which pipes are actually one connected network" view.
                    DrawPipeTiles(spriteBatch, network, networkColor);

                    // 2. Draw a box around every machine this network is attached to.
                    foreach (var attachment in network.MachineAttachments)
                        DrawAttachmentOverlay(spriteBatch, attachment.Position, networkColor * 0.35f, networkColor);

                    // 3. Draw a box around every world-liquid tile this network is attached to —
                    //    a slightly different border color so it's visually distinct from machines.
                    foreach (var attachment in network.WorldLiquidAttachments)
                        DrawAttachmentOverlay(spriteBatch, attachment.Position, Color.SkyBlue * 0.35f, Color.SkyBlue);

                    // 4. One text readout per network, anchored at its first pipe tile —
                    //    "first" is arbitrary (HashSet has no order), just needs SOME anchor point.
                    if (network.PipeTiles.Count > 0)
                    {
                        Point anchor = default;
                        foreach (var p in network.PipeTiles) { anchor = p; break; }

                        string text = $"[Liquid Net #{i + 1}]\n" +
                                      $"Pipes: {network.PipeTiles.Count}\n" +
                                      $"Max Rate: {network.MaxFlowRate}\n" +
                                      $"Machines: {network.MachineAttachments.Count}\n" +
                                      $"World Sources: {network.WorldLiquidAttachments.Count}";

                        DrawWorldText(spriteBatch, anchor, text, networkColor);
                    }
                }
            }

            spriteBatch.End();
        }

        void DrawPipeTiles(SpriteBatch sb, LiquidNetwork network, Color color)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color fill = color * 0.35f;

            foreach (Point pos in network.PipeTiles)
            {
                Vector2 screenPos = new Vector2(pos.X, pos.Y) * 16f - Main.screenPosition;
                Rectangle rect = new Rectangle((int)screenPos.X, (int)screenPos.Y, 16, 16);

                sb.Draw(pixel, rect, fill);
                Utils.DrawRect(sb, rect, color);
            }
        }

        void DrawAttachmentOverlay(SpriteBatch sb, Point pos, Color fillColor, Color borderColor)
        {
            // NOTE: unlike PowerGridDebugSystem's DrawEntityOverlay, this doesn't look up
            // TileObjectData to size the box to the machine's full footprint — it just
            // draws a single 16x16 tile box at the exact attachment point. Good enough to
            // confirm "the network reaches this specific tile," which is what actually
            // matters for debugging mouth/attachment logic. Can be upgraded to a full
            // footprint box later if that's more useful once machines are bigger than 1x1.
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 screenPos = new Vector2(pos.X, pos.Y) * 16f - Main.screenPosition;
            Rectangle rect = new Rectangle((int)screenPos.X, (int)screenPos.Y, 16, 16);

            sb.Draw(pixel, rect, fillColor);
            Utils.DrawRect(sb, rect, borderColor);
        }

        void DrawWorldText(SpriteBatch sb, Point anchorTile, string text, Color color)
        {
            Vector2 screenPos = new Vector2(anchorTile.X, anchorTile.Y) * 16f - Main.screenPosition;
            Vector2 labelPos = screenPos - new Vector2(0, 60f); // a bit higher than power's -45, more lines of text here

            Utils.DrawBorderString(sb, text, labelPos, color, 0.8f);
        }
    }
}
