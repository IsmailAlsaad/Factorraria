using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Patch 7b.2, TEMPORARY test code (remove in Phase 10). Measures the "ghost items" seen in the 7.4 / 7b.1 test (finding R3): world
    // items and vItems a client still shows that the server no longer has, and the other way round. It changes no behavior.
    //   /fghost     on a client: prints this client's numbers and asks the server to compare them with its own; the answer arrives as
    //               chat lines (and in the server log). On the server console: prints the server's totals.
    //   /farmsend   [on|off|status] switches the Robotic Arm's explicit tile broadcast (patch 7b.2) on the server, so a test can show
    //               what vanilla already sends by itself (verification item 16). Use it in the server console.
    // The server log also gets [Ghost] lines for every arm drop that became a vItem and every VirtualItemSystem.SyncWorldItem call.
    public static class GhostDiagnostics
    {
        public static int RouteDropConversions;
        public static int WorldItemSyncs;
        public static int WorldItemSyncsInactive;
        public static int ChecksHandled;
        public static int GhostWorldItemsFound;
        public static int GhostVItemsFound;

        const float RadiusTiles = 100f;
        const int MaxWorldItems = 80;
        const int MaxVItems = 120;
        const int MaxLines = 12;

        public static void OnRouteDrop(int index, int type, int stack, int vItemId, int tileX, int tileY)
        {
            RouteDropConversions++;
            FactorrariaNet.Log("[Ghost] RouteDrop: world item #" + index + " (type " + type + " x" + stack + ") became vItem " + vItemId + " at tile (" + tileX + ", " + tileY + ")");
        }

        public static void OnWorldItemSync(int index, string source)
        {
            if (Main.netMode != NetmodeID.Server || index < 0 || index >= Main.maxItems)
                return;

            Item item = Main.item[index];
            bool gone = !item.active || item.IsAir;
            WorldItemSyncs++;
            if (gone)
                WorldItemSyncsInactive++;

            FactorrariaNet.Log("[Ghost] SyncWorldItem (" + source + ") #" + index + ": type " + item.type + " stack " + item.stack + " active " + item.active
                + (gone ? " -> clients are told it is gone" : ""));
        }

        public static string DescribeLocal()
        {
            string counters = "routeDrops=" + RouteDropConversions + " worldSyncs=" + WorldItemSyncs + " (inactive " + WorldItemSyncsInactive + ") checks=" + ChecksHandled
                + " ghostItemsFound=" + GhostWorldItemsFound + " ghostVItemsFound=" + GhostVItemsFound;

            if (Main.dedServ)
            {
                int items = 0;
                for (int i = 0; i < Main.maxItems; i++)
                    if (Main.item[i].active && !Main.item[i].IsAir)
                        items++;

                return "ghost (server): world items active=" + items + " vItems=" + VirtualItemSystem.virtualItems.Count + " | " + counters;
            }

            List<int> nearItems = new List<int>();
            List<int> nearVItems = new List<int>();
            Vector2 center = Main.LocalPlayer.Center;
            CollectWorldItems(center, nearItems);
            CollectVItems(center, nearVItems);
            return "ghost (netMode " + Main.netMode + "): world items within " + RadiusTiles + " tiles=" + nearItems.Count + " vItems=" + nearVItems.Count + " | " + counters;
        }

        static void CollectWorldItems(Vector2 center, List<int> indices)
        {
            float radius = RadiusTiles * 16f;
            for (int i = 0; i < Main.maxItems; i++)
            {
                Item item = Main.item[i];
                if (item == null || !item.active || item.IsAir)
                    continue;

                if (Vector2.Distance(item.Center, center) <= radius)
                    indices.Add(i);
            }
        }

        static void CollectVItems(Vector2 center, List<int> ids)
        {
            float radius = RadiusTiles * 16f;
            List<VirtualItem> all = VirtualItemSystem.virtualItems;
            for (int i = 0; i < all.Count; i++)
            {
                VirtualItem item = all[i];
                if (item == null || !item.active || item.Id == 0)
                    continue;

                if (Vector2.Distance(item.worldPosition, center) <= radius)
                    ids.Add(item.Id);
            }
        }

        // ---------------------------------------------------------------- client: send what this client shows

        public static void SendCheck(Player player)
        {
            List<int> items = new List<int>();
            List<int> vItems = new List<int>();
            CollectWorldItems(player.Center, items);
            CollectVItems(player.Center, vItems);

            bool itemsCut = items.Count > MaxWorldItems;
            bool vItemsCut = vItems.Count > MaxVItems;
            if (itemsCut)
                items.RemoveRange(MaxWorldItems, items.Count - MaxWorldItems);
            if (vItemsCut)
                vItems.RemoveRange(MaxVItems, vItems.Count - MaxVItems);

            FactorrariaNet.SendRequest(FactorrariaPacket.GhostCheckRequest, writer =>
            {
                writer.Write(itemsCut);
                writer.Write((ushort)items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    Item item = Main.item[items[i]];
                    writer.Write((short)items[i]);
                    writer.Write((ushort)item.type);
                    writer.Write(item.stack);
                }

                writer.Write(vItemsCut);
                writer.Write((ushort)vItems.Count);
                for (int i = 0; i < vItems.Count; i++)
                    writer.Write(vItems[i]);
            });
        }

        // ---------------------------------------------------------------- server: compare with the real state

        public static void HandleCheck(BinaryReader reader, int sender)
        {
            if (sender < 0 || sender >= Main.maxPlayers || !Main.player[sender].active)
                return;

            Player player = Main.player[sender];
            bool itemsCut = reader.ReadBoolean();
            int itemCount = reader.ReadUInt16();
            if (itemCount > MaxWorldItems)
                return;

            HashSet<int> clientItems = new HashSet<int>();
            List<string> lines = new List<string>();
            int ghostItems = 0;
            int mismatches = 0;

            for (int n = 0; n < itemCount; n++)
            {
                int index = reader.ReadInt16();
                int type = reader.ReadUInt16();
                int stack = reader.ReadInt32();
                clientItems.Add(index);

                if (index < 0 || index >= Main.maxItems)
                    continue;

                Item real = Main.item[index];
                if (!real.active || real.IsAir)
                {
                    ghostItems++;
                    lines.Add("GHOST world item #" + index + " (client: type " + type + " x" + stack + "): the server has none (server slot type " + real.type + ", active " + real.active + ")");
                }
                else if (real.type != type || real.stack != stack)
                {
                    mismatches++;
                    lines.Add("DIFFERENT world item #" + index + ": client type " + type + " x" + stack + ", server type " + real.type + " x" + real.stack);
                }
            }

            bool vItemsCut = reader.ReadBoolean();
            int vItemCount = reader.ReadUInt16();
            if (vItemCount > MaxVItems)
                return;

            HashSet<int> clientVItems = new HashSet<int>();
            int ghostVItems = 0;
            for (int n = 0; n < vItemCount; n++)
            {
                int id = reader.ReadInt32();
                clientVItems.Add(id);

                if (!VirtualItemSystem.TryGetById(id, out VirtualItem real) || real == null || !real.active)
                {
                    ghostVItems++;
                    lines.Add("GHOST vItem id " + id + ": the server has no such vItem");
                }
            }

            int missingItems = 0;
            int missingVItems = 0;
            int serverItemsNear = 0;
            int serverVItemsNear = 0;

            List<int> nearItems = new List<int>();
            CollectWorldItems(player.Center, nearItems);
            serverItemsNear = nearItems.Count;
            if (!itemsCut)
            {
                foreach (int index in nearItems)
                {
                    if (clientItems.Contains(index))
                        continue;

                    missingItems++;
                    Item real = Main.item[index];
                    lines.Add("MISSING on client: world item #" + index + " type " + real.type + " x" + real.stack + " at tile (" + (int)(real.Center.X / 16f) + ", " + (int)(real.Center.Y / 16f) + ")");
                }
            }

            List<int> nearVItems = new List<int>();
            CollectVItems(player.Center, nearVItems);
            serverVItemsNear = nearVItems.Count;
            if (!vItemsCut)
            {
                foreach (int id in nearVItems)
                {
                    if (clientVItems.Contains(id))
                        continue;

                    missingVItems++;
                    lines.Add("MISSING on client: vItem id " + id);
                }
            }

            ChecksHandled++;
            GhostWorldItemsFound += ghostItems;
            GhostVItemsFound += ghostVItems;

            string summary = "[Ghost] check for " + player.name + ": client shows " + itemCount + " world items and " + vItemCount + " vItems, server has " + serverItemsNear + " and " + serverVItemsNear
                + " near that player | ghost items " + ghostItems + ", different items " + mismatches + ", ghost vItems " + ghostVItems + ", missing items " + missingItems + ", missing vItems " + missingVItems
                + (itemsCut || vItemsCut ? " (client list was cut, so 'missing' was not checked for the cut list)" : "");

            FactorrariaNet.Log(summary);
            Reply(sender, summary);

            for (int i = 0; i < lines.Count; i++)
            {
                FactorrariaNet.Log("[Ghost]   " + lines[i]);
                if (i < MaxLines)
                    Reply(sender, lines[i]);
            }

            if (lines.Count > MaxLines)
                Reply(sender, "... " + (lines.Count - MaxLines) + " more lines in the server log");
        }

        static void Reply(int toClient, string text)
        {
            ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(text), Color.LightGreen, toClient);
        }
    }

    public class GhostDiagnosticsSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.GhostCheckRequest, PacketDirection.ToServer, GhostDiagnostics.HandleCheck, 5);
        }
    }

    public class GhostCheckCommand : ModCommand
    {
        public override string Command => "fghost";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fghost";
        public override string Description => "Multiplayer test (7b.2): compares the world items and vItems a client shows with the server's.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string text = GhostDiagnostics.DescribeLocal();
            caller.Reply(text, Color.LightGreen);
            FactorrariaNet.Log("[fghost] " + text);

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                GhostDiagnostics.SendCheck(Main.LocalPlayer);
                caller.Reply("Check sent to the server; the answer arrives as chat lines.", Color.LightGreen);
            }
        }
    }

    public class ArmSendCommand : ModCommand
    {
        public override string Command => "farmsend";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/farmsend [on|off|fxon|fxoff|areareject|placereject|status]";
        public override string Description => "Multiplayer test (7b.2): switches the Robotic Arm's explicit tile broadcast (type it in the server console).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length > 0 && args[0] == "on")
                RoboticArmWorker.BroadcastTileSquares = true;
            else if (args.Length > 0 && args[0] == "off")
                RoboticArmWorker.BroadcastTileSquares = false;
            else if (args.Length > 0 && args[0] == "fxon")
                RoboticArmWorker.BroadcastTileEffects = true;
            else if (args.Length > 0 && args[0] == "fxoff")
                RoboticArmWorker.BroadcastTileEffects = false;
            else if (args.Length > 0 && args[0] == "areareject")
                RoboticArmAreaNet.RejectNext = true; // patch 7b.5 test: the server rejects the next area request (type it in the server console)
            else if (args.Length > 0 && args[0] == "placereject")
                MachinePlacement.RejectNextEntityRequest = true; // patch 7b.6 test: the server refuses the next machine entity request (type it in the server console)

            string text = "Robotic Arm tile broadcast is " + (RoboticArmWorker.BroadcastTileSquares ? "ON" : "OFF") + " on this side (netMode " + Main.netMode + "). " + " Dig and place effects are " + (RoboticArmWorker.BroadcastTileEffects ? "ON" : "OFF") + ". " + RoboticArmNet.Describe() + " (patch 7b.4 counters) | " + RoboticArmAreaNet.Describe() + (RoboticArmAreaNet.RejectNext ? " | next area request will be REJECTED (test)" : "") + (MachinePlacement.RejectNextEntityRequest ? " | next machine entity request will be REFUSED (test)" : "");
            caller.Reply(text, Color.LightGreen);
            FactorrariaNet.Log("[farmsend] " + text);
        }
    }
}