using Factorraria.Common.Carts;
using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using ModLiquidExampleMod.Content.Items;
using System.Collections.Concurrent;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Pour a liquid bucket into a machine tank or a liquid-fuel motor cart (side feature, designed multiplayer-first).
    //
    // Rule: a left click with any liquid bucket (vanilla water / lava / honey, or a BucketBase bucket such as Fuel and Oil) on a machine
    // that has input tanks, or on a motor cart that runs on a liquid, pours one bucket (BucketUnits, the same 255 as one full world tile)
    // into it when a tank accepts that liquid and has room for the whole bucket. Otherwise the click does what it always did (pours
    // into the world).
    //
    // Authority: the server (or single-player) owns every tank. The click is detected on the owning client (it needs the cursor and the
    // held item) and checked against the replicas for a quick yes or no, so a refused pour falls through to the normal world pour with no
    // round trip. In single-player the pour is applied at once. On a multiplayer client nothing changes locally: BucketPourRequest goes
    // to the server (kind byte: 0 machine at tile x, y; 1 cart with this id; plus the bucket item type). The server re-checks the
    // player, reach, that the held item (cursor item first, like the cart requests) is that bucket, a 10 tick cooldown per player and
    // the tank rules, writes the tank and answers BucketPourResult(accepted). The CLIENT swaps the bucket for an empty one when the
    // answer says accepted (the server never edits its copy of the inventory). The new tank level reaches every client through the
    // existing machine light sync and CartDelta. One request is in flight at a time per client (30 tick timeout).
    public static class BucketPour
    {
        // One bucket = one full world tile of liquid.
        public static float BucketUnits => 255f;

        static readonly ConcurrentQueue<(bool accepted, int itemType)> results = new();
        static uint nextPourTick;
        static readonly uint[] lastPourTick = new uint[Main.maxPlayers + 1];

        public static void Reset()
        {
            while (results.TryDequeue(out _)) { }
            nextPourTick = 0;
            System.Array.Clear(lastPourTick, 0, lastPourTick.Length);
        }

        // ---------------------------------------------------------------- rules (shared by client prediction and server)

        /// <summary>Which registry liquid does this bucket hold? False for anything that is not a full liquid bucket.</summary>
        public static bool TryGetBucketLiquid(Item item, out int liquidType)
        {
            liquidType = -1;
            if (item == null || item.IsAir)
                return false;

            if (item.type == ItemID.WaterBucket)
            {
                liquidType = LiquidTypeRegistry.Water;
                return true;
            }

            if (item.type == ItemID.LavaBucket)
            {
                liquidType = LiquidTypeRegistry.Lava;
                return true;
            }

            if (item.type == ItemID.HoneyBucket)
            {
                liquidType = LiquidTypeRegistry.Honey;
                return true;
            }

            if (item.ModItem is BucketBase bucket && bucket.BucketLiquidType >= 0)
            {
                int? registry = LiquidTypeRegistry.FromTileLiquidId((byte)bucket.BucketLiquidType);
                if (registry != null)
                {
                    liquidType = registry.Value;
                    return true;
                }
            }

            return false;
        }

        static bool HasRoomForBucket(LiquidStack tank) => tank.Capacity - tank.Amount >= BucketUnits - 0.01f;

        /// <summary>
        /// The input tank of this machine a bucket of this liquid would go into: a tank that already holds it and has room, else the first
        /// empty tank that accepts it (the same order the pipes use). Water is resolved to the machine's biome water like a pipe does.
        /// </summary>
        public static bool TryFindMachineTank(BaseMachine machine, int bucketLiquid, out LiquidStack tank, out int resolved)
        {
            tank = null;
            resolved = bucketLiquid;

            if (machine == null)
                return false;

            LiquidStack[] tanks = machine.InputLiquids;
            if (tanks == null || tanks.Length == 0)
                return false;

            resolved = machine.ResolveIncomingLiquid(bucketLiquid);

            LiquidStack firstEmpty = null;
            for (int i = 0; i < tanks.Length; i++)
            {
                LiquidStack slot = tanks[i];
                if (!slot.Accepts(resolved) || !HasRoomForBucket(slot))
                    continue;

                if (slot.IsEmpty)
                {
                    firstEmpty ??= slot;
                    continue;
                }

                if (slot.LiquidType == resolved)
                {
                    tank = slot;
                    return true;
                }
            }

            tank = firstEmpty;
            return tank != null;
        }

        /// <summary>Only a motor cart that runs on a liquid takes a bucket, and only its own fuel liquid, into an empty tank or one that holds that liquid.</summary>
        public static bool CanPourCart(Cart cart, int bucketLiquid)
        {
            if (cart == null || !cart.Active || cart.Module != CartModule.Motor || !cart.UsesLiquid || cart.Tank == null)
                return false;

            if (!cart.IsFuelLiquid(bucketLiquid) || !HasRoomForBucket(cart.Tank))
                return false;

            return cart.Tank.IsEmpty || cart.Tank.LiquidType == bucketLiquid;
        }

        static void Deposit(LiquidStack tank, int liquidType)
        {
            if (tank.IsEmpty)
            {
                tank.LiquidType = liquidType;
                tank.Amount = 0f;
            }

            tank.Amount += BucketUnits;
        }

        // ---------------------------------------------------------------- client: the click

        /// <summary>
        /// Called from the bucket's CanUseItem on the owning client. Returns true when the click belongs to a machine or cart that can take
        /// this bucket (the normal world pour must not run), false to let the bucket behave as before.
        /// </summary>
        public static bool TryPourAtCursor(Player player, Item bucket)
        {
            if (Main.dedServ || Main.gameMenu || player == null || player.whoAmI != Main.myPlayer || player.dead || player.noBuilding)
                return false;

            if (!TryGetBucketLiquid(bucket, out int liquid))
                return false;

            byte kind;
            int a;
            int b;
            LiquidStack tank = null;
            int resolved = liquid;
            Cart cart = null;

            cart = CartSystem.FindCartAtCursor(player);
            if (cart != null && CanPourCart(cart, liquid))
            {
                kind = 1;
                a = cart.Id;
                b = 0;
                tank = cart.Tank;
            }
            else
            {
                cart = null;
                int tx = Player.tileTargetX;
                int ty = Player.tileTargetY;

                if (!WorldGen.InWorld(tx, ty, 10) || !FactorrariaNet.InReach(player, tx, ty))
                    return false;

                if (!TileEntityHelper.TryGetEntityFromTile(tx, ty, out TileEntity entity, out Point16 corner) || entity is not BaseMachine machine)
                    return false;

                if (!TryFindMachineTank(machine, liquid, out tank, out resolved))
                    return false;

                kind = 0;
                a = tx;
                b = ty;
            }

            // The click is ours from here on. Wait out the cooldown / the answer without falling through to the world pour.
            if (Main.GameUpdateCount < nextPourTick)
                return true;

            int itemType = bucket.type;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                nextPourTick = Main.GameUpdateCount + 30;
                FactorrariaNet.SendRequest(FactorrariaPacket.BucketPourRequest, w =>
                {
                    w.Write(kind);
                    w.Write(a);
                    w.Write(b);
                    w.Write(itemType);
                });
                return true;
            }

            // Single-player: the real tank is right here.
            nextPourTick = Main.GameUpdateCount + 15;
            Deposit(tank, cart != null ? liquid : resolved);
            ConsumeBucket(player, itemType);
            SoundEngine.PlaySound(SoundID.SplashWeak, player.Center);
            return true;
        }

        // ---------------------------------------------------------------- server: the request

        public static void HandleRequest(BinaryReader reader, int sender)
        {
            byte kind = reader.ReadByte();
            int a = reader.ReadInt32();
            int b = reader.ReadInt32();
            int itemType = reader.ReadInt32();

            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers)
                return;   // single-player and the host never send this: the click already changed the real tank

            Player player = Main.player[sender];
            if (player == null || !player.active || player.dead)
                return;

            if (!ServerTryPour(player, sender, kind, a, b, itemType, out string reason))
            {
                FactorrariaNet.Log("[BucketPour] refused " + player.name + ": " + reason);
                SendResult(sender, false, itemType);
                return;
            }

            SendResult(sender, true, itemType);
        }

        static Item HeldOnServer(Player player)
        {
            Item mouse = player.inventory[58];
            return mouse != null && !mouse.IsAir ? mouse : player.HeldItem;
        }

        static bool ServerTryPour(Player player, int sender, byte kind, int a, int b, int itemType, out string reason)
        {
            reason = null;

            Item held = HeldOnServer(player);
            if (held == null || held.IsAir || held.type != itemType || !TryGetBucketLiquid(held, out int liquid))
            {
                reason = "the held item is not that bucket";
                return false;
            }

            if (lastPourTick[sender] != 0 && Main.GameUpdateCount - lastPourTick[sender] < 10)
            {
                reason = "too fast (10 tick cooldown)";
                return false;
            }

            if (kind == 0)
            {
                RequestCheck check = MachineRequest.Validate(sender, a, b, 0f, out BaseMachine machine, out Point16 corner);
                if (check != RequestCheck.Ok)
                {
                    reason = machine == null ? "no machine there" : "out of reach";
                    return false;
                }

                if (!TryFindMachineTank(machine, liquid, out LiquidStack tank, out int resolved))
                {
                    reason = "no tank at (" + corner.X + ", " + corner.Y + ") takes that liquid with room for a bucket";
                    return false;
                }

                Deposit(tank, resolved);
                lastPourTick[sender] = Main.GameUpdateCount;
                FactorrariaNet.Log("[BucketPour] " + player.name + " poured into the machine at (" + corner.X + ", " + corner.Y + ")");
                return true;
            }

            if (kind == 1)
            {
                Cart cart = CartSystem.FindById(a);
                if (cart == null || !CartSystem.IsInInteractRange(player, cart, FactorrariaNet.ReachSlackTiles * 16f))
                {
                    reason = cart == null ? "no such cart" : "cart out of reach";
                    return false;
                }

                if (!CanPourCart(cart, liquid))
                {
                    reason = "that cart cannot take that liquid right now";
                    return false;
                }

                Deposit(cart.Tank, liquid);
                lastPourTick[sender] = Main.GameUpdateCount;
                FactorrariaNet.Log("[BucketPour] " + player.name + " poured into cart " + cart.Id);
                return true;
            }

            reason = "unknown kind " + kind;
            return false;
        }

        static void SendResult(int sender, bool accepted, int itemType)
        {
            FactorrariaNet.SendToClient(sender, FactorrariaPacket.BucketPourResult, w =>
            {
                w.Write(accepted);
                w.Write(itemType);
            });
        }

        // ---------------------------------------------------------------- client: the answer

        // May run on the network thread: it only queues.
        public static void HandleResult(BinaryReader reader, int sender)
        {
            bool accepted = reader.ReadBoolean();
            int itemType = reader.ReadInt32();

            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            results.Enqueue((accepted, itemType));
        }

        /// <summary>Main thread, every tick on a client: an accepted pour empties the bucket.</summary>
        public static void ClientTick()
        {
            while (results.TryDequeue(out var result))
            {
                nextPourTick = 0;

                if (!result.accepted)
                    continue;

                ConsumeBucket(Main.LocalPlayer, result.itemType);
                SoundEngine.PlaySound(SoundID.SplashWeak, Main.LocalPlayer.Center);
            }
        }

        // One full bucket of this type becomes an empty bucket (the selected slot first), then every inventory slot that changed is sent.
        static void ConsumeBucket(Player player, int itemType)
        {
            int slot = -1;
            Item selected = player.inventory[player.selectedItem];
            if (selected != null && !selected.IsAir && selected.type == itemType)
            {
                slot = player.selectedItem;
            }
            else
            {
                for (int i = 0; i < 58; i++)
                {
                    Item candidate = player.inventory[i];
                    if (candidate != null && !candidate.IsAir && candidate.type == itemType)
                    {
                        slot = i;
                        break;
                    }
                }
            }

            if (slot < 0)
                return;

            int count = player.inventory.Length;
            int[] types = new int[count];
            int[] stacks = new int[count];
            for (int i = 0; i < count; i++)
            {
                types[i] = player.inventory[i].type;
                stacks[i] = player.inventory[i].stack;
            }

            player.inventory[slot].stack--;
            player.PutItemInInventoryFromItemUsage(ItemID.EmptyBucket, slot);

            for (int i = 0; i < count; i++)
            {
                Item now = player.inventory[i];
                if (now.type != types[i] || now.stack != stacks[i])
                    FactorrariaNet.SyncInventoryItem(player, now);
            }
        }
    }

    // The bucket's left click. Returning false from CanUseItem stops the use (and with it the world pour in BucketBase.HoldItem and
    // vanilla's own water / lava / honey bucket code) only when the cursor is on a machine or cart that takes the bucket.
    public class BucketPourGlobalItem : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.WaterBucket || entity.type == ItemID.LavaBucket || entity.type == ItemID.HoneyBucket || entity.ModItem is BucketBase;
        }

        public override bool CanUseItem(Item item, Player player)
        {
            if (BucketPour.TryPourAtCursor(player, item))
                return false;

            return true;
        }
    }

    public class BucketPourSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.BucketPourRequest, PacketDirection.ToServer, BucketPour.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.BucketPourResult, PacketDirection.ToClient, BucketPour.HandleResult);
        }

        public override void OnWorldLoad() => BucketPour.Reset();

        public override void OnWorldUnload() => BucketPour.Reset();

        // PostUpdateWorld does not run on a multiplayer client (patch 7.3b); PostUpdateEverything runs on every side.
        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                BucketPour.ClientTick();
        }
    }
}