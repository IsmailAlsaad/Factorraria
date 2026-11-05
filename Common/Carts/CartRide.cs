using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Carts
{
    /// <summary>Where and how fast a rider starts, as worked out by the server (or single-player) when it takes the cart.</summary>
    public struct CartRideStart
    {
        public int SkinItem;
        public Vector2 Position;   // the cart's rail point
        public Vector2 Velocity;   // px per tick, world axes
        public int Facing;
    }

    /// <summary>
    /// Riding a plain cart. The cart becomes the player's vanilla mount (the skin item's mountType), so vanilla's minecart physics carry the
    /// rider. Take runs where the carts live (server / single-player), Apply on the owner. The mount ending for any reason is noticed by
    /// CartRiderPlayer, which gives the cart back through ReturnCart / PlaceOrDrop. The cart-equip slot is never read or changed.
    /// </summary>
    public static class CartRide
    {
        /// <summary>Pixels the rider is lifted above the rail point when it starts. Tune if the rider sinks into or floats over the track.</summary>
        public static float MountRailLift { get { return 0f; } }

        public static int MountTypeOf(int skinItem)
        {
            if (skinItem <= 0 || skinItem >= ItemLoader.ItemCount)
            {
                return -1;
            }

            return ContentSamples.ItemsByType[skinItem].mountType;
        }

        /// <summary>A cart that can be ridden: no module (so no chest, terrarium or motor panel) and not a motor / mechanical skin.</summary>
        public static bool CanMount(Cart cart)
        {
            if (cart == null || !cart.Active || cart.Module != CartModule.Empty || cart.HasPanel || cart.AlwaysRuns || Cart.IsMotorSkin(cart.SkinType))
            {
                return false;
            }

            int mountType = MountTypeOf(cart.SkinType);
            return mountType >= 0 && mountType < MountID.Sets.Cart.Length && MountID.Sets.Cart[mountType];
        }

        public static bool CanPlayerRide(Player player)
        {
            return player != null && player.active && !player.dead && !player.mount.Active;
        }

        /// <summary>
        /// Server / single-player: takes the cart off the world. The passenger NPC (if any) gets out with the cart's velocity, the cart
        /// leaves its chain and is removed. Fills start for the rider.
        /// </summary>
        public static bool Take(Cart cart, out CartRideStart start)
        {
            start = default(CartRideStart);

            if (!CanMount(cart))
            {
                return false;
            }

            Cart body = cart.Body;
            Vector2 velocity;

            if (body.OnTrack)
            {
                Vector2 right = CartGeometry.Right(cart);
                velocity = System.Math.Abs(right.X) > 0.1f ? right * (body.Speed / right.X) : new Vector2(body.Speed, 0f);   // Speed is the X part, so a slope adds its Y part
            }
            else
            {
                velocity = body.Velocity;
            }

            start.SkinItem = cart.SkinType;
            start.Position = cart.Position;
            start.Velocity = velocity;
            start.Facing = cart.Facing < 0 ? -1 : 1;

            CartPassengers.ReleaseWithVelocity(cart, velocity);
            cart.PendingPassenger = null;

            CartChestUISystem.NotifyCartRemoved(cart);

            if (cart.Chain != null)
            {
                cart.Chain.Remove(cart);
            }

            cart.Active = false;
            return true;
        }

        /// <summary>Single-player / host: take the cart and get on it.</summary>
        public static void MountHere(Player player, Cart cart)
        {
            CartRideStart start;
            if (Take(cart, out start))
            {
                Apply(player, start);
            }
        }

        /// <summary>Owner: mounts the skin's cart mount and starts it on the cart's rail point with the cart's velocity.</summary>
        public static void Apply(Player player, CartRideStart start)
        {
            int mountType = MountTypeOf(start.SkinItem);

            // Registered first: if the mount is refused, the next PostUpdate sees "not mounted" and gives the cart back.
            player.GetModPlayer<CartRiderPlayer>().Begin(start.SkinItem, mountType);

            CartMountBlocker.AllowCartMount = true;
            try
            {
                player.mount.SetMount(mountType, player, start.Facing < 0);
            }
            finally
            {
                CartMountBlocker.AllowCartMount = false;
            }

            player.direction = start.Facing < 0 ? -1 : 1;
            player.Bottom = start.Position - new Vector2(0f, MountRailLift);
            player.velocity = start.Velocity;
            player.oldPosition = player.position;
            player.fallStart = (int)(player.position.Y / 16f);
            player.fallStart2 = player.fallStart;
            player.RemoveAllGrapplingHooks();
        }

        /// <summary>Owner: the ride is over. Single-player puts the cart back (or hands the item over) at once, a client asks the server.</summary>
        public static void ReturnCart(Player player, int skinItem)
        {
            int tileX;
            int tileY;
            bool hasSpot = FindReturnSpot(player, out tileX, out tileY);
            int direction = player.direction >= 0 ? 1 : -1;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                CartInputNet.RequestDismount(skinItem, hasSpot, tileX, tileY, direction);
            }
            else
            {
                PlaceOrDrop(player, skinItem, hasSpot, tileX, tileY, direction);
            }
        }

        /// <summary>A track tile at or just below the player's feet where a cart can stand (range and overlap rules of a normal placement).</summary>
        public static bool FindReturnSpot(Player player, out int tileX, out int tileY)
        {
            int x = (int)(player.Center.X / 16f);
            int y = (int)((player.Bottom.Y - 1f) / 16f);
            int[] offsets = { 0, 1, 2, -1 };

            for (int i = 0; i < offsets.Length; i++)
            {
                if (CartSystem.CheckPlacement(player, x, y + offsets[i], out _, out _))
                {
                    tileX = x;
                    tileY = y + offsets[i];
                    return true;
                }
            }

            tileX = x;
            tileY = y;
            return false;
        }

        /// <summary>Server / single-player: the cart goes back on the track at the spot (re-checked here), or the skin item drops at the player.</summary>
        public static void PlaceOrDrop(Player player, int skinItem, bool hasSpot, int tileX, int tileY, int direction)
        {
            Vector2 rail;

            if (hasSpot && WorldGen.InWorld(tileX, tileY, 10) && CartSystem.CheckPlacement(player, tileX, tileY, out rail, out _))
            {
                Cart cart = CartSystem.SpawnCart(rail, skinItem);
                cart.Facing = direction < 0 ? -1 : 1;
                return;
            }

            player.QuickSpawnItem(player.GetSource_Misc("FactorrariaCartDismount"), new Item(skinItem), 1);
        }
    }

    /// <summary>Remembers which cart the player rides and notices when the ride ends (dismount key, death, anything), on the owner only.</summary>
    public class CartRiderPlayer : ModPlayer
    {
        public int RidingSkin;
        private int ridingMountType;
        private int savedSkin;   // loaded from the player file: a ride that was still going when the game was left

        public void Begin(int skinItem, int mountType)
        {
            RidingSkin = skinItem;
            ridingMountType = mountType;
        }

        public override void PostUpdate()
        {
            if (!FactorrariaNet.IsOwner(Player) || RidingSkin <= 0)
            {
                return;
            }

            if (Player.mount.Active && Player.mount.Type == ridingMountType)
            {
                return;
            }

            int skin = RidingSkin;
            RidingSkin = 0;
            CartRide.ReturnCart(Player, skin);
        }

        public override void PlayerDisconnect()
        {
            if (Main.netMode == NetmodeID.Server)
            {
                CartInputNet.ServerRiderLeft(Player);
            }
        }

        // Single-player only: quitting while riding must not lose the cart. (A multiplayer server drops it in PlayerDisconnect.)
        public override void SaveData(TagCompound tag)
        {
            if (RidingSkin > 0 && Main.netMode == NetmodeID.SinglePlayer)
            {
                tag["ridingItem"] = ItemIO.Save(new Item(RidingSkin));
            }
        }

        public override void LoadData(TagCompound tag)
        {
            savedSkin = 0;

            if (tag.ContainsKey("ridingItem"))
            {
                Item item = ItemIO.Load(tag.GetCompound("ridingItem"));
                savedSkin = item != null && !item.IsAir ? item.type : 0;
            }
        }

        public override void OnEnterWorld()
        {
            if (savedSkin <= 0)
            {
                return;
            }

            Item item = new Item(savedSkin);
            savedSkin = 0;

            Item leftover = Player.GetItem(Player.whoAmI, item, GetItemSettings.InventoryEntityToPlayerInventorySettings);
            if (leftover != null && !leftover.IsAir)
            {
                Player.QuickSpawnItem(Player.GetSource_Misc("FactorrariaCartRideSaved"), leftover, leftover.stack);
            }
        }
    }
}