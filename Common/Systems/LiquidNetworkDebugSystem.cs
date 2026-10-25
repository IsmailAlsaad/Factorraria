using Factorraria.Common.Liquids;
using Factorraria.Content.Configs;
using Factorraria.Common.Machines;
using Factorraria.Common.Networks;
using Factorraria.Common.Networks.Flow;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
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

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // The liquid networks exist on the server only (roadmap 8.4, finding P6: this overlay stays single-player), so there is
                // nothing to draw here. Say so instead of drawing nothing; the server log has the [Pipes] lines.
                spriteBatch.Begin();
                Utils.DrawBorderString(spriteBatch, "Liquid network debug view: single-player only (the networks live on the server, see its [Pipes] log lines)", new Vector2(20f, 140f), Color.Yellow, 0.8f);
                spriteBatch.End();
                return;
            }

            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            List<FlowNetwork> networks = LiquidNetworkSystem.FlowNetworks;
            if (networks != null)
            {
                for (int i = 0; i < networks.Count; i++)
                {
                    FlowNetwork network = networks[i];
                    Color networkColor = NetworkColors[i % NetworkColors.Length];

                    // 1. Every pipe and motor tile tinted with its network's colour; a motor also shows its facing arrow and ON / OFF.
                    DrawNodes(spriteBatch, network, networkColor);

                    // 2. Every source and sink port at its attach tile, with the liquid it holds (machine tanks, world cells).
                    DrawPorts(spriteBatch, network);

                    // 3. At the end of every route: the rate it carries, how many tiles it climbs and how much that costs.
                    DrawRouteLabels(spriteBatch, network);

                    // 4. One readout per network, anchored at its first tile.
                    if (network.Positions.Length > 0)
                    {
                        int motorCount = 0, motorsOn = 0;
                        for (int n = 0; n < network.Nodes.Length; n++)
                        {
                            if (network.Motors[n] == null) continue;
                            motorCount++;
                            if (network.Nodes[n].MotorOn) motorsOn++;
                        }

                        float moved = 0f;
                        foreach (FlowTransfer transfer in network.LastTransfers)
                            moved += transfer.Amount;

                        string text = $"[Flow Net #{i + 1}]\n" +
                                      $"Pipes: {network.Nodes.Length - motorCount}  Pumps: {motorCount} ({motorsOn} on)\n" +
                                      $"Liquid: {(network.Solver.ActiveType == -1 ? "none" : LiquidTypeRegistry.Get(network.Solver.ActiveType).Name)}\n" +
                                      $"Cap: {(network.Cap >= float.MaxValue ? "none" : (network.Cap / 3600f).ToString("0.##") + "/tick")}  Moving: {moved:0.##}/tick\n" +
                                      $"Routes: {network.Routes.Count}  Sources: {network.Sources.Count}  Sinks: {network.Sinks.Count}";

                        DrawWorldText(spriteBatch, network.Positions[0], text, networkColor);
                    }
                }
            }

            spriteBatch.End();
        }

        // The overlay reads the FlowNetworks (liquid overhaul).
        void DrawNodes(SpriteBatch sb, FlowNetwork network, Color color)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            for (int n = 0; n < network.Positions.Length; n++)
            {
                Point pos = network.Positions[n];
                Vector2 worldPos = new Vector2(pos.X, pos.Y) * 16f;
                if (!BaseMachine.IsOnScreen(worldPos)) continue;

                Vector2 screenPos = worldPos - Main.screenPosition;
                Rectangle rect = new Rectangle((int)screenPos.X, (int)screenPos.Y, 16, 16);
                bool isMotor = network.Motors[n] != null;

                sb.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), color * (isMotor ? 0.6f : 0.35f));
                Utils.DrawRect(sb, rect, color);

                if (!isMotor) continue;

                string arrow = network.Nodes[n].Facing switch
                {
                    FlowDir.Up => "^",
                    FlowDir.Down => "v",
                    FlowDir.Left => "<",
                    _ => ">"
                };
                bool on = network.Nodes[n].MotorOn;
                Utils.DrawBorderString(sb, arrow, screenPos + new Vector2(4f, 0f), Color.White, 0.9f);
                Utils.DrawBorderString(sb, on ? "ON" : "OFF", screenPos + new Vector2(0f, -12f), on ? Color.LightGreen : Color.Red, 0.5f);
            }
        }

        // The tile a port is attached to: the machine tile or world cell on the other side of the pipe mouth.
        static Point AttachTile(FlowNetwork network, FlowPort port)
        {
            Point node = network.Positions[port.Node];
            return node + ((Direction)(int)port.Mouth).ToOffset();
        }

        static string TankText(LiquidStack tank)
        {
            return tank == null || tank.IsEmpty ? "empty" : $"{LiquidTypeRegistry.Get(tank.LiquidType).Name} {tank.Amount:0}/{tank.Capacity:0}";
        }

        static string WorldCellText(Point cell)
        {
            Tile tile = Main.tile[cell.X, cell.Y];
            if (tile.LiquidAmount <= 0) return "cell: dry";

            int? type = LiquidTypeRegistry.FromTileLiquidId((byte)tile.LiquidType);
            return $"cell: {(type == null ? "type " + tile.LiquidType : LiquidTypeRegistry.Get(type.Value).Name)} {tile.LiquidAmount}/255";
        }

        // One box per attach tile (green = machine output, orange = machine input, gold = both, sky blue = world cell) with a text
        // line per port: the tank it reads from or fills, or the world cell's liquid.
        void DrawPorts(SpriteBatch sb, FlowNetwork network)
        {
            var sourcesAt = new Dictionary<Point, List<int>>();
            var sinksAt = new Dictionary<Point, List<int>>();

            for (int a = 0; a < network.SourceAttach.Length; a++)
            {
                Point tile = AttachTile(network, network.SourceAttach[a]);
                if (!sourcesAt.TryGetValue(tile, out List<int> list)) sourcesAt[tile] = list = new List<int>();
                foreach (int s in network.SourcePortsOfAttach[a]) if (!list.Contains(s)) list.Add(s);
            }
            for (int a = 0; a < network.SinkAttach.Length; a++)
            {
                Point tile = AttachTile(network, network.SinkAttach[a]);
                if (!sinksAt.TryGetValue(tile, out List<int> list)) sinksAt[tile] = list = new List<int>();
                foreach (int k in network.SinkPortsOfAttach[a]) if (!list.Contains(k)) list.Add(k);
            }

            var tiles = new HashSet<Point>(sourcesAt.Keys);
            tiles.UnionWith(sinksAt.Keys);

            foreach (Point tile in tiles)
            {
                Vector2 worldPos = new Vector2(tile.X, tile.Y) * 16f;
                if (!BaseMachine.IsOnScreen(worldPos)) continue;

                bool world = false, machineSource = false, machineSink = false;
                var lines = new List<string>();

                if (sourcesAt.TryGetValue(tile, out List<int> sources))
                {
                    foreach (int s in sources)
                    {
                        FlowSource source = network.Sources[s];
                        if (source.IsWorld) { world = true; lines.Add(WorldCellText(source.WorldPos)); continue; }

                        machineSource = true;
                        LiquidStack[] tanks = source.Machine.OutputLiquids;
                        lines.Add("out#" + source.TankIndex + ": " + TankText(source.TankIndex < tanks.Length ? tanks[source.TankIndex] : null));
                    }
                }

                if (sinksAt.TryGetValue(tile, out List<int> sinks))
                {
                    foreach (int k in sinks)
                    {
                        FlowSink sink = network.Sinks[k];
                        if (sink.IsWorld) { world = true; continue; }   // a world cell is both source and sink: its text is above

                        machineSink = true;
                        LiquidStack[] tanks = sink.Machine.InputLiquids;
                        for (int t = 0; t < tanks.Length; t++)
                            lines.Add("in#" + t + ": " + TankText(tanks[t]));
                    }
                }

                Color border = world ? Color.SkyBlue : machineSource && machineSink ? Color.Gold : machineSource ? Color.LightGreen : Color.Orange;
                DrawAttachmentOverlay(sb, tile, border * 0.3f, border);

                if (lines.Count > 0)
                    Utils.DrawBorderString(sb, string.Join("\n", lines), worldPos - Main.screenPosition + new Vector2(0f, 16f), Color.White, 0.5f);
            }
        }

        // Next to the sink end of every route: "<- rate/tick, climbs N, lost X/tick". Lost = what the climb cost against the weakest pump.
        void DrawRouteLabels(SpriteBatch sb, FlowNetwork network)
        {
            var sinkTile = new Dictionary<int, Point>();
            for (int a = 0; a < network.SinkAttach.Length; a++)
                foreach (int k in network.SinkPortsOfAttach[a])
                    sinkTile.TryAdd(k, AttachTile(network, network.SinkAttach[a]));

            foreach (FlowRoute route in network.Routes)
            {
                if (!sinkTile.TryGetValue(route.Dst, out Point tile)) continue;

                Vector2 worldPos = new Vector2(tile.X, tile.Y) * 16f;
                if (!BaseMachine.IsOnScreen(worldPos)) continue;

                float weakest = float.MaxValue;
                foreach (int pump in route.Pumps)
                    weakest = System.Math.Min(weakest, network.Nodes[pump].Strength);
                float lost = System.Math.Max(0f, System.Math.Min(weakest, network.Cap) - route.RatePerMinute) / 3600f;

                string text = $"<- {route.RatePerMinute / 3600f:0.##}/tick, climbs {route.Climbs}, lost {lost:0.##}/tick";
                Utils.DrawBorderString(sb, text, worldPos - Main.screenPosition + new Vector2(0f, -22f), Color.LightGray, 0.5f);
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
