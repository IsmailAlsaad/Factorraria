using Factorraria.Common.PowerGrid;
using Factorraria.Common.Systems;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Microsoft.Xna.Framework;
using System;
using System.Text;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // TEMPORARY test commands for power poles patch P3 (remove in Phase 10: delete this file). The click input does not exist until patch
    // P6, so these send the same PoleLinkRequest the input will send.
    //   /plink x1 y1 x2 y2 [copper|tin|gold] [slotA slotB]   link two poles. Each x y is ANY tile of that pole (use /plinks or /fsnap all to find them).
    //                                                         The clicked tile is the footprint tile of pole 2 at your height, so stand next to pole 2.
    //                                                         Without slots the server picks the connection points; with them it uses exactly those.
    //   /plink cut x y slot                                   cut the link on connection point <slot> of the pole at x y (stand next to that pole)
    // A Chat command runs on the client that typed it, which is what the test needs. The answer arrives as a chat line (a refusal) or as
    // the wire leaving your inventory (a made link).
    public class PoleLinkCommand : ModCommand
    {
        public override string Command => "plink";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/plink x1 y1 x2 y2 [copper|tin|gold] [slotA slotB]   or   /plink cut x y slot";
        public override string Description => "Power pole test (P3): links two poles, or cuts a link. Any tile of a pole names the pole.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length > 0 && args[0].Equals("cut", StringComparison.OrdinalIgnoreCase))
            {
                Cut(caller, args);
                return;
            }

            if (args.Length < 4 || !int.TryParse(args[0], out int x1) || !int.TryParse(args[1], out int y1) || !int.TryParse(args[2], out int x2) || !int.TryParse(args[3], out int y2))
            {
                caller.Reply("plink: usage " + Usage, Color.OrangeRed);
                return;
            }

            CustomWireType wire = CustomWireType.Copper;
            if (args.Length > 4)
            {
                switch (args[4].ToLowerInvariant())
                {
                    case "copper": wire = CustomWireType.Copper; break;
                    case "tin": wire = CustomWireType.Tin; break;
                    case "gold": wire = CustomWireType.Gold; break;
                    default:
                        caller.Reply("plink: the wire must be copper, tin or gold", Color.OrangeRed);
                        return;
                }
            }

            int slotA = PoleLinkNet.AutoSlot;
            int slotB = PoleLinkNet.AutoSlot;
            if (args.Length > 6)
            {
                if (!int.TryParse(args[5], out slotA) || !int.TryParse(args[6], out slotB) || slotA < 0 || slotB < 0 || slotA > 254 || slotB > 254)
                {
                    caller.Reply("plink: slotA and slotB must be numbers from 0 to 254", Color.OrangeRed);
                    return;
                }
            }

            PickClickTile(x2, y2, out int clickX, out int clickY);
            PoleLinkNet.RequestLink(x1, y1, x2, y2, wire, slotA, slotB, clickX, clickY);

            caller.Reply("plink: asked for a " + wire + " link from (" + x1 + ", " + y1 + ") to (" + x2 + ", " + y2 + "), clicking tile (" + clickX + ", " + clickY + ")"
                + (slotA == PoleLinkNet.AutoSlot ? ", server picks the points" : ", points " + slotA + " and " + slotB)
                + ". A refusal shows up as an orange chat line; /plinks shows the result.", Color.LightGreen);
        }

        static void Cut(CommandCaller caller, string[] args)
        {
            if (args.Length < 4 || !int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y) || !int.TryParse(args[3], out int slot) || slot < 0 || slot > 254)
            {
                caller.Reply("plink: usage /plink cut x y slot", Color.OrangeRed);
                return;
            }

            PickClickTile(x, y, out int clickX, out int clickY);
            PoleLinkNet.RequestCut(x, y, slot, clickX, clickY);
            caller.Reply("plink: asked to cut point " + slot + " of the pole at (" + x + ", " + y + "), clicking tile (" + clickX + ", " + clickY + ").", Color.LightGreen);
        }

        // The footprint tile of the pole at the player's height (so the click is in reach when the player stands next to the pole). When
        // this process does not know the pole, the typed tile is used as it is.
        static void PickClickTile(int poleTileX, int poleTileY, out int clickX, out int clickY)
        {
            clickX = poleTileX;
            clickY = poleTileY;

            if (!PoleLinkSystem.TryGetPoleAtTile(poleTileX, poleTileY, out PowerPoleTileEntityBase pole, out Point16 corner))
                return;

            int playerTileY = (int)(Main.LocalPlayer.Center.Y / 16f);
            clickX = corner.X;
            clickY = Math.Clamp(playerTileY, corner.Y, corner.Y + PoleLinkSystem.FootprintHeight(pole) - 1);
        }
    }

    // TEMPORARY (patch P3, remove in Phase 10: same file). /plinks lists the links THIS side knows, so a server console and a client can be
    // compared: in the server console it shows the authoritative graph, in a client's chat the replica, in single-player the real one.
    public class PoleLinksCommand : ModCommand
    {
        public override string Command => "plinks";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/plinks";
        public override string Description => "Power pole test (P3, P4): lists the pole links this side knows, the span and cost tunables, and whether the two poles of each link share a grid.";

        // Server and single-player only (a client builds no grids): do both poles sit in the same PowerNetwork right now?
        static bool SharesGrid(PowerPoleTileEntityBase a, PowerPoleTileEntityBase b)
        {
            foreach (PowerNetwork network in PowerGridSystem.ActivePowerNetworks)
            {
                if (network.electricConsumers.Contains(a))
                    return network.electricConsumers.Contains(b);
            }

            return false;
        }

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            PoleLinkSystem links = PoleLinkSystem.Instance;
            string side = Main.netMode == NetmodeID.Server ? "server" : Main.netMode == NetmodeID.MultiplayerClient ? "client replica" : "single-player";

            caller.Reply("plinks [" + side + "]: " + links.Links.Count + " link(s), version " + links.Version + "; span copper " + links.CopperSpan + " tin " + links.TinSpan + " gold " + links.GoldSpan
                + " tiles; tiles per item " + links.CopperTilesPerItem + " / " + links.TinTilesPerItem + " / " + links.GoldTilesPerItem, Color.LightGreen);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                PowerGridSystem grid = ModContent.GetInstance<PowerGridSystem>();
                caller.Reply("plinks: " + PowerGridSystem.ActivePowerNetworks.Count + " grid(s); the last rebuild joined " + grid.PoleBridgesLastBuild + " pole(s) through links and skipped "
                    + grid.PoleLinkEndsMissingLastBuild + " link end(s) with no pole entity here", Color.LightGreen);
            }

            int shown = 0;
            foreach (PoleLink link in links.Links)
            {
                if (shown++ >= 20)
                {
                    caller.Reply("plinks: ... " + (links.Links.Count - 20) + " more not shown", Color.LightGreen);
                    break;
                }

                var line = new StringBuilder();
                line.Append("(").Append(link.A.X).Append(", ").Append(link.A.Y).Append(")#").Append(link.SlotA);
                line.Append(" <-> (").Append(link.B.X).Append(", ").Append(link.B.Y).Append(")#").Append(link.SlotB);
                line.Append("  ").Append(link.Wire).Append(", cost ").Append(link.Cost);

                bool hasA = PoleLinkSystem.TryGetPole(link.A, out PowerPoleTileEntityBase poleA);
                bool hasB = PoleLinkSystem.TryGetPole(link.B, out PowerPoleTileEntityBase poleB);
                if (hasA && hasB)
                    line.Append(", span ").Append(PoleLinkSystem.SpanTiles(poleA, link.SlotA, poleB, link.SlotB).ToString("0.0")).Append(" tiles");
                if (hasA && hasB && Main.netMode != NetmodeID.MultiplayerClient)
                    line.Append(SharesGrid(poleA, poleB) ? ", same grid" : ", DIFFERENT grids");
                if (!hasA)
                    line.Append(", pole A entity missing here");
                if (!hasB)
                    line.Append(", pole B entity missing here");

                caller.Reply(line.ToString(), Color.LightGreen);
            }
        }
    }
}