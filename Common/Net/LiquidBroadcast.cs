using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Patch 8.1 (finding P2). The liquid networks edit tile.LiquidAmount / LiquidType directly (LiquidWorldBanks, since the liquid overhaul;
    // before it LiquidNetwork.WithdrawFromWorldTile and DepositToWorldTile) and nothing told the clients. Authority: the server (or single-player) is the only side that ticks the networks
    // (LiquidNetworkSystem.PostUpdateWorld returns on a client), so only the server marks tiles here. Mark() queues a position once
    // (a tile touched twice before it is sent goes out once, with its final state); every tick up to LiquidBroadcastsPerTick queued
    // positions are sent and the rest wait for the next tick. Single-player and clients never mark anything, so nothing changes there.
    // Vanilla liquids (water, lava, honey, shimmer) use vanilla's own water message through NetMessage.sendWater, which only reaches
    // clients whose loaded sections contain the tile. A custom liquid (a tile liquid id above shimmer) goes through a 1x1 tile square
    // instead, because ModLiquidLib patches the tile block codec (not the water message) to carry its liquid type.
    public class LiquidBroadcastSystem : ModSystem
    {
        static readonly Queue<Point> queue = new Queue<Point>();
        static readonly HashSet<Point> queued = new HashSet<Point>();

        // TEMPORARY test counters (remove in Phase 10; shown by /fliquid).
        public static int Marked;
        public static int SentWater;
        public static int SentSquares;
        public static int PeakQueue;
        public static int TicksOverCap;
        static int lastLoggedSent;

        public static void Mark(int x, int y)
        {
            if (Main.netMode != NetmodeID.Server || !WorldGen.InWorld(x, y, 1))
                return;

            var position = new Point(x, y);
            if (!queued.Add(position))
                return;

            queue.Enqueue(position);
            Marked++;
            if (queue.Count > PeakQueue)
                PeakQueue = queue.Count;
        }

        public override void OnWorldLoad() => Reset();
        public override void OnWorldUnload() => Reset();

        static void Reset()
        {
            queue.Clear();
            queued.Clear();
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.Server || queue.Count == 0)
                return;

            int cap = Math.Max(1, NetworkConfig.Current.LiquidBroadcastsPerTick);
            int sent = 0;
            while (sent < cap && queue.Count > 0)
            {
                Point position = queue.Dequeue();
                queued.Remove(position);
                Send(position);
                sent++;
            }

            if (queue.Count > 0)
                TicksOverCap++;

            if (Main.GameUpdateCount % 300 == 0 && SentWater + SentSquares != lastLoggedSent)
            {
                lastLoggedSent = SentWater + SentSquares;
                FactorrariaNet.Log("[Liquid] " + Describe());
            }
        }

        static void Send(Point position)
        {
            Tile tile = Main.tile[position.X, position.Y];
            if (tile.LiquidAmount == 0 || tile.LiquidType <= LiquidID.Shimmer)
            {
                NetMessage.sendWater(position.X, position.Y);
                SentWater++;
            }
            else
            {
                NetMessage.SendTileSquare(-1, position.X, position.Y, 1, 1);
                SentSquares++;
            }
        }

        public static string Describe() => "marked " + Marked + ", water messages " + SentWater + ", tile squares (custom liquids) " + SentSquares
            + ", queued now " + queue.Count + ", peak queue " + PeakQueue + ", ticks over the cap " + TicksOverCap;
    }

    // Temporary test command for patch 8.1 (remove in Phase 10): prints the broadcast counters of the side it runs on (only the
    // server ever counts anything).
    public class LiquidBroadcastCommand : ModCommand
    {
        public override string Command => "fliquid";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fliquid";
        public override string Description => "Multiplayer test (8.1): world liquid broadcast counters.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string side = Main.netMode == NetmodeID.Server ? "server" : Main.netMode == NetmodeID.MultiplayerClient ? "client (counts nothing)" : "single-player (counts nothing)";
            string text = "fliquid [" + side + "]: " + LiquidBroadcastSystem.Describe();
            caller.Reply(text, Color.LightGreen);
            FactorrariaNet.Log("[Liquid] " + text);
        }
    }
}