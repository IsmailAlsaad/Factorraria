using Factorraria.Common.Carts;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    public class CartInputNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.CartActionRequest, PacketDirection.ToServer, CartInputNet.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.CartActionResult, PacketDirection.ToClient, CartInputNet.HandleResult);
        }

        public override void OnWorldLoad() => CartInputNet.Reset();

        public override void OnWorldUnload() => CartInputNet.Reset();

        // PostUpdateWorld does not run on a multiplayer client (patch 7.3b); PostUpdateEverything runs on every side.
        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            CartInputNet.ClientTick();
        }
    }

    public enum CartAction : byte
    {
        Place = 0,
        Install = 1,
        Shove = 2,
        PickUp = 3,
    }

    // Cart input as requests (roadmap patch 9.3, finding C3).
    //
    // Authority: the server owns the carts. Click detection and the placement hologram stay on the owning client (they need the mouse
    // and the held item) and read the replicas that patch 9.2 puts in CartSystem.Carts. Single-player never comes here: the old direct
    // code in CartSystem runs unchanged. On a multiplayer client every action is a CartActionRequest with a kind byte:
    //  - Place (item type, tile x, tile y, direction): the server checks the player, that the held item (cursor item first, like the
    //    client) has that type and is a cart item, re-runs CartSystem.CheckPlacement (track under the tile, range, no cart in the way),
    //    spawns the cart with SpawnCart and answers CartActionResult(accepted). The CLIENT then consumes one item of that type (the
    //    server must not edit its copy of the inventory: vanilla ignores equipment syncs about yourself, so the client is the one that
    //    changes its inventory and syncs the slot). A refusal leaves the item untouched.
    //  - Install (cart id, item type): same held-item check, the cart must exist, be in reach and take a module
    //    (CartSystem.GetInstallModule); the server calls SetModule and answers; the client consumes one item on "accepted".
    //  - Shove (cart id): the cart must exist and be in reach; the push direction is away from the server's copy of the player.
    //  - PickUp (cart id): the cart must exist and be in reach; the server runs CartSystem.PickUp, which spawns the skin item, module
    //    item and contents at the player with Item.NewItem (auto-synced). The removal reaches every client through CartDelta.
    // The client lets one place/install request be on its way at a time (a second click while waiting does nothing), and the server
    // allows one place or install per player per 10 ticks, so a click burst can not turn one item into several carts.
    //
    // NOT in 9.3 (by design): opening the panel, fuel add and shift-insert. A replica's chest and fuel are empty on a client until 9.5, so
    // a right click on a chest, terrarium or motor cart only shows a notice. Shift + right click (pick up) works.
    // Server-side reach uses CartSystem's interact range plus ReachSlackTiles for the lag of the server's copy of the player.
    public static class CartInputNet
    {
        sealed class Result
        {
            public CartAction Action;
            public bool Accepted;
            public int ItemType;
        }

        static readonly ConcurrentQueue<Result> results = new();
        static uint pendingUntil;
        static readonly uint[] lastPlaceTick = new uint[Main.maxPlayers + 1];

        // Temporary test counters (remove in Phase 10).
        public static int ServerPlaced;
        public static int ServerInstalled;
        public static int ServerShoved;
        public static int ServerPickedUp;
        public static int ServerRefused;
        public static int ClientSent;
        public static int ClientAccepted;
        public static int ClientRefused;
        public static int ClientConsumed;
        public static int ClientConsumeMissed;

        public static void Reset()
        {
            while (results.TryDequeue(out _)) { }
            pendingUntil = 0;
            System.Array.Clear(lastPlaceTick, 0, lastPlaceTick.Length);
            ServerPlaced = ServerInstalled = ServerShoved = ServerPickedUp = ServerRefused = 0;
            ClientSent = ClientAccepted = ClientRefused = ClientConsumed = ClientConsumeMissed = 0;
        }

        public static string Describe()
        {
            return "cartInput: server placed=" + ServerPlaced + " installed=" + ServerInstalled + " shoved=" + ServerShoved + " pickedUp=" + ServerPickedUp
                + " refused=" + ServerRefused + "; client sent=" + ClientSent + " accepted=" + ClientAccepted + " refused=" + ClientRefused
                + " consumed=" + ClientConsumed + " (not found " + ClientConsumeMissed + ")";
        }

        // ---------------------------------------------------------------- client: requests

        /// <summary>
        /// Multiplayer client: asks the server to place the held cart item on the tile under the cursor. Returns true when the click was used
        /// (a request was sent, or one is still on its way), false when the spot is not valid even for the client (so nothing is sent).
        /// </summary>
        public static bool RequestPlace(Player player, Item held)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || held == null || held.IsAir)
                return false;

            if (Main.GameUpdateCount < pendingUntil)
                return true;

            if (!CartSystem.CheckPlacement(player, Player.tileTargetX, Player.tileTargetY, out _, out _))
                return false;

            pendingUntil = Main.GameUpdateCount + 30;

            int itemType = held.type;
            int tileX = Player.tileTargetX;
            int tileY = Player.tileTargetY;
            sbyte direction = (sbyte)(player.direction >= 0 ? 1 : -1);

            ClientSent++;
            FactorrariaNet.SendRequest(FactorrariaPacket.CartActionRequest, w =>
            {
                w.Write((byte)CartAction.Place);
                w.Write(itemType);
                w.Write(tileX);
                w.Write(tileY);
                w.Write(direction);
            });

            return true;
        }

        /// <summary>Multiplayer client: a left click on a cart. Installs the held module when it fits, otherwise shoves.</summary>
        public static void RequestInstallOrShove(Item held, Cart target)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || target == null || target.Id <= 0)
                return;

            int cartId = target.Id;

            if (CartSystem.GetInstallModule(held, target, out _))
            {
                if (Main.GameUpdateCount < pendingUntil)
                    return;

                pendingUntil = Main.GameUpdateCount + 30;
                int itemType = held.type;

                ClientSent++;
                FactorrariaNet.SendRequest(FactorrariaPacket.CartActionRequest, w =>
                {
                    w.Write((byte)CartAction.Install);
                    w.Write(cartId);
                    w.Write(itemType);
                });
                return;
            }

            ClientSent++;
            FactorrariaNet.SendRequest(FactorrariaPacket.CartActionRequest, w =>
            {
                w.Write((byte)CartAction.Shove);
                w.Write(cartId);
            });
        }

        /// <summary>Multiplayer client: a right click (or Shift + right click) that picks the cart up.</summary>
        public static void RequestPickUp(Cart target)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || target == null || target.Id <= 0)
                return;

            int cartId = target.Id;

            ClientSent++;
            FactorrariaNet.SendRequest(FactorrariaPacket.CartActionRequest, w =>
            {
                w.Write((byte)CartAction.PickUp);
                w.Write(cartId);
            });
        }

        // ---------------------------------------------------------------- server: requests

        public static void HandleRequest(BinaryReader reader, int sender)
        {
            CartAction action = (CartAction)reader.ReadByte();

            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers)
                return;   // single-player and the host never send these: the old direct code ran

            Player player = Main.player[sender];
            if (!player.active || player.dead)
                return;

            switch (action)
            {
                case CartAction.Place:
                    ServerPlace(player, sender, reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadSByte());
                    break;

                case CartAction.Install:
                    ServerInstall(player, sender, reader.ReadInt32(), reader.ReadInt32());
                    break;

                case CartAction.Shove:
                    ServerShove(player, reader.ReadInt32());
                    break;

                case CartAction.PickUp:
                    ServerPickUp(player, reader.ReadInt32());
                    break;
            }
        }

        // The item the player is "holding" for cart actions, from the server's copy: the cursor item (slot 58) first, like the client.
        static Item HeldOnServer(Player player)
        {
            Item mouse = player.inventory[58];
            return mouse != null && !mouse.IsAir ? mouse : player.HeldItem;
        }

        static float SlackPixels => FactorrariaNet.ReachSlackTiles * 16f;

        static void Refuse(Player player, int sender, CartAction action, int itemType, string reason)
        {
            ServerRefused++;
            FactorrariaNet.Log("[Carts] refused " + action + " from " + player.name + ": " + reason);
            SendResult(sender, action, false, itemType);
        }

        static void SendResult(int sender, CartAction action, bool accepted, int itemType)
        {
            FactorrariaNet.SendToClient(sender, FactorrariaPacket.CartActionResult, w =>
            {
                w.Write((byte)action);
                w.Write(accepted);
                w.Write(itemType);
            });
        }

        static bool CooldownPassed(int sender)
        {
            if (lastPlaceTick[sender] != 0 && Main.GameUpdateCount - lastPlaceTick[sender] < 10)
                return false;

            lastPlaceTick[sender] = Main.GameUpdateCount;
            return true;
        }

        static void ServerPlace(Player player, int sender, int itemType, int tileX, int tileY, sbyte direction)
        {
            if (!WorldGen.InWorld(tileX, tileY, 10))
            {
                Refuse(player, sender, CartAction.Place, itemType, "tile out of the world");
                return;
            }

            Item held = HeldOnServer(player);
            if (held == null || held.IsAir || held.type != itemType || !CartSkinTable.IsPlaceable(held))
            {
                Refuse(player, sender, CartAction.Place, itemType, "the held item is not that cart item");
                return;
            }

            if (!CooldownPassed(sender))
            {
                Refuse(player, sender, CartAction.Place, itemType, "too fast (10 tick cooldown)");
                return;
            }

            if (!CartSystem.CheckPlacement(player, tileX, tileY, out Vector2 railPoint, out bool onTrack))
            {
                Refuse(player, sender, CartAction.Place, itemType, onTrack ? "out of range or a cart is in the way" : "no track there (or track data not loaded on the server)");
                return;
            }

            Cart cart = CartSystem.SpawnCart(railPoint, CartSkinTable.SkinTypeFor(held));
            cart.Facing = direction < 0 ? -1 : 1;

            ServerPlaced++;
            FactorrariaNet.Log("[Carts] " + player.name + " placed cart " + cart.Id + " at tile (" + tileX + ", " + tileY + ")");
            SendResult(sender, CartAction.Place, true, itemType);
        }

        static void ServerInstall(Player player, int sender, int cartId, int itemType)
        {
            Cart cart = CartSystem.FindById(cartId);
            if (cart == null || !CartSystem.IsInInteractRange(player, cart, SlackPixels))
            {
                Refuse(player, sender, CartAction.Install, itemType, cart == null ? "no such cart" : "out of reach");
                return;
            }

            Item held = HeldOnServer(player);
            if (held == null || held.IsAir || held.type != itemType)
            {
                Refuse(player, sender, CartAction.Install, itemType, "the held item is not that item");
                return;
            }

            if (!CooldownPassed(sender))
            {
                Refuse(player, sender, CartAction.Install, itemType, "too fast (10 tick cooldown)");
                return;
            }

            if (!CartSystem.GetInstallModule(held, cart, out CartModule module))
            {
                Refuse(player, sender, CartAction.Install, itemType, "the cart does not take that module");
                return;
            }

            cart.SetModule(module, held.type);

            ServerInstalled++;
            FactorrariaNet.Log("[Carts] " + player.name + " installed " + module + " in cart " + cart.Id);
            SendResult(sender, CartAction.Install, true, itemType);
        }

        static void ServerShove(Player player, int cartId)
        {
            Cart cart = CartSystem.FindById(cartId);
            if (cart == null || !CartSystem.IsInInteractRange(player, cart, SlackPixels))
            {
                ServerRefused++;
                return;
            }

            float away = cart.Position.X >= player.Center.X ? 1f : -1f;
            cart.Shove(away * CartPhysics.ShoveSpeed);
            ServerShoved++;
        }

        static void ServerPickUp(Player player, int cartId)
        {
            Cart cart = CartSystem.FindById(cartId);
            if (cart == null || !CartSystem.IsInInteractRange(player, cart, SlackPixels))
            {
                ServerRefused++;
                FactorrariaNet.Log("[Carts] refused PickUp from " + player.name + ": " + (cart == null ? "no such cart" : "out of reach"));
                return;
            }

            int id = cart.Id;
            CartSystem.PickUp(player, cart);

            ServerPickedUp++;
            FactorrariaNet.Log("[Carts] " + player.name + " picked up cart " + id);
        }

        // ---------------------------------------------------------------- client: the answer

        // May run on the network thread: it only queues.
        public static void HandleResult(BinaryReader reader, int sender)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            Result result = new Result();
            result.Action = (CartAction)reader.ReadByte();
            result.Accepted = reader.ReadBoolean();
            result.ItemType = reader.ReadInt32();
            results.Enqueue(result);
        }

        /// <summary>Main thread, every tick on a client: handles the answers (an accepted place or install consumes one item).</summary>
        public static void ClientTick()
        {
            while (results.TryDequeue(out Result result))
            {
                if (result.Action == CartAction.Place || result.Action == CartAction.Install)
                    pendingUntil = 0;

                if (!result.Accepted)
                {
                    ClientRefused++;
                    continue;
                }

                ClientAccepted++;

                if (result.Action == CartAction.Place || result.Action == CartAction.Install)
                {
                    ConsumeOne(Main.LocalPlayer, result.ItemType);
                    SoundEngine.PlaySound(SoundID.Dig, Main.LocalPlayer.Center);
                }
            }
        }

        // Takes one item of this type: the cursor item if it matches, else the first matching inventory stack. Then sends the slot to
        // the server (SyncEquipment, slot 58 for the cursor item).
        static void ConsumeOne(Player player, int itemType)
        {
            Item target = null;
            Item held = CartSkinTable.GetHeldItem(player);

            if (held != null && !held.IsAir && held.type == itemType)
            {
                target = held;
            }
            else
            {
                for (int i = 0; i < 58; i++)
                {
                    Item candidate = player.inventory[i];
                    if (candidate != null && !candidate.IsAir && candidate.type == itemType)
                    {
                        target = candidate;
                        break;
                    }
                }
            }

            if (target == null)
            {
                ClientConsumeMissed++;
                return;
            }

            target.stack--;
            if (target.stack <= 0)
                target.TurnToAir();

            int slot = -1;
            for (int i = 0; i < player.inventory.Length; i++)
            {
                if (ReferenceEquals(player.inventory[i], target))
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0 && ReferenceEquals(target, Main.mouseItem))
                slot = 58;

            if (slot >= 0)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot, target.prefix);

            ClientConsumed++;
        }
    }
}