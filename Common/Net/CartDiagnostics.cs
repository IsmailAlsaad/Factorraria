using Factorraria.Common.Carts;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Temporary test code for patch 9.1 (remove in Phase 10): /fcarts lists the carts of the side it runs on (id, skin, module,
    // tile position, chain role) with the next-id counter; on a client, /fcarts fake spawns a local fake replica next to the player
    // that is fed 10 server-like states per second (a back and forth run of 480 px at 3 px per tick), /fcarts clear removes it.
    // The fake proves that ClientStep draws and eases a replica smoothly on a client without any packet. It is client only and
    // never reaches the server or any save.
    public class CartDiagnosticsCommand : ModCommand
    {
        public override string Command => "fcarts";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fcarts [fake|clear]";
        public override string Description => "Multiplayer test (9.1): list carts and ids; on a client, fake/clear a local replica to check interpolation.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (sub == "fake" || sub == "clear")
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    caller.Reply("fcarts " + sub + ": only on a multiplayer client (single-player and the server simulate real carts)", Color.OrangeRed);
                    return;
                }

                string result = sub == "fake" ? CartTestFeed.Start(Main.LocalPlayer.Center) : CartTestFeed.Stop();
                caller.Reply("fcarts " + sub + ": " + result, Color.LightGreen);
                FactorrariaNet.Log("[Carts] fcarts " + sub + ": " + result);
                return;
            }

            string side = Main.netMode == NetmodeID.Server ? "server" : Main.netMode == NetmodeID.MultiplayerClient ? "client" : "single-player";
            caller.Reply("fcarts [" + side + "]: " + CartSystem.Carts.Count + " cart(s), next id " + CartSystem.NextCartId, Color.LightGreen);
            FactorrariaNet.Log("[Carts] fcarts [" + side + "]: " + CartSystem.Carts.Count + " cart(s), next id " + CartSystem.NextCartId);
            caller.Reply(CartNet.Describe(), Color.LightGreen);
            FactorrariaNet.Log("[Carts] " + CartNet.Describe());
            caller.Reply(CartInputNet.Describe(), Color.LightGreen);
            FactorrariaNet.Log("[Carts] " + CartInputNet.Describe());

            for (int i = 0; i < CartSystem.Carts.Count; i++)
            {
                Cart cart = CartSystem.Carts[i];
                string role = cart.Chain == null ? "free" : cart.Chain.Motor == cart ? "chain motor" : "carriage";
                string line = "  id " + cart.Id + " " + cart.Skin + " " + cart.Module + " at tile (" + (int)(cart.Position.X / 16f) + ", " + (int)(cart.Position.Y / 16f) + ") " + role
                    + (cart.Active ? "" : " (inactive)");
                caller.Reply(line, Color.LightGreen);
                FactorrariaNet.Log("[Carts] " + line.Trim());
            }
        }
    }

    // Feeds the fake replica of /fcarts fake. Runs only on a client; forgets the cart when the world unloads.
    public class CartTestFeed : ModSystem
    {
        static Cart fake;
        static Vector2 origin;
        static int ticks;

        public static string Start(Vector2 playerCenter)
        {
            if (fake != null)
            {
                return "a fake replica is already running (use /fcarts clear first)";
            }

            fake = new Cart();
            fake.Skin = CartSkins.KeyOf(ItemID.Minecart);
            fake.SetModule(CartModule.Empty);
            fake.Id = -1;
            origin = playerCenter + new Vector2(0f, 16f);
            ticks = 0;
            CartSystem.Carts.Add(fake);
            return "fake replica added next to you; it runs right 480 px and back, fed 10 states per second";
        }

        public static string Stop()
        {
            if (fake == null)
            {
                return "no fake replica";
            }

            fake.Active = false;
            CartSystem.Carts.Remove(fake);
            fake = null;
            return "fake replica removed";
        }

        public override void PostUpdateEverything()
        {
            if (fake == null)
            {
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                fake = null;
                return;
            }

            ticks++;

            if (ticks % 6 != 0)
            {
                return;
            }

            int phase = ticks % 320;
            int dir = phase < 160 ? 1 : -1;
            float run = phase < 160 ? phase * 3f : (320 - phase) * 3f;

            fake.ApplyNetState(origin + new Vector2(run, 0f), new Vector2(3f * dir, 0f), 0f, dir, true);
        }

        public override void OnWorldUnload()
        {
            fake = null;
        }
    }
}