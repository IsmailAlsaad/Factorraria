using Factorraria.Common.Liquids;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// What the modules DO. Chest carts pick conveyor items (vItems) up and spill them back on a bump; terrarium carts
    /// drain world liquid into their tank and pour it back on a bump. Called from CartSystem: Update every tick for every
    /// cart, OnBump from the Cart.Bumped event. Tuning numbers live in CartPhysics.
    /// </summary>
    public static class CartCargo
    {
        public const int WindowColumns = CartChestUIState.Columns;
        public const int WindowRows = CartChestUIState.Rows;

        private static readonly List<VirtualItem> nearby = new List<VirtualItem>();
        private static readonly Vector2[] corners = new Vector2[4];

        // Where the terrarium looks for liquid, as px offsets from the rail point (positive = down): the rail's own tile,
        // the cell the cart's feet are in, and the cell above that. Rows are de-duplicated, the first with liquid wins.
        private static readonly float[] DrainProbeOffsets = { 1f, -1f, -17f };

        public static void Update(Cart cart)
        {
            if (cart.Module == CartModule.Chest)
            {
                PickUpConveyorItems(cart);
            }
            else if (cart.Module == CartModule.Terrarium)
            {
                DrainWorldLiquid(cart);
            }
        }

        public static void OnBump(Cart cart)
        {
            if (cart.Module == CartModule.Chest)
            {
                SpillChest(cart);
            }
            else if (cart.Module == CartModule.Terrarium)
            {
                PourTank(cart);
            }
        }

        /// <summary>
        /// The 3x2 block of tiles the cart's cargo maps onto, one tile per chest slot (slot i = column i % 3, row i / 3, the
        /// same layout as the chest panel). It is the 3x2 block closest to the centre of the cart's rotated hitbox, so it
        /// follows the cart's position and tilt. (The hitbox is 51 x 35 px, a little bigger than 3x2 tiles = 48 x 32 px.)
        /// </summary>
        public static void GetWindow(Cart cart, out int left, out int top)
        {
            Vector2 center = CartGeometry.BoxCenter(cart);
            left = (int)Math.Floor(center.X / 16f - WindowColumns / 2f + 0.5f);
            top = (int)Math.Floor(center.Y / 16f - WindowRows / 2f + 0.5f);
        }

        #region Chest: conveyor pickup and bump spill

        /// <summary>Moves conveyor items that are inside (or within PickupPadding of) the rotated hitbox into the chest.</summary>
        private static void PickUpConveyorItems(Cart cart)
        {
            if (cart.ChestItems == null)
            {
                return;
            }

            CartGeometry.GetCorners(cart, corners);

            float minX = corners[0].X;
            float maxX = corners[0].X;
            float minY = corners[0].Y;
            float maxY = corners[0].Y;

            for (int i = 1; i < 4; i++)
            {
                minX = Math.Min(minX, corners[i].X);
                maxX = Math.Max(maxX, corners[i].X);
                minY = Math.Min(minY, corners[i].Y);
                maxY = Math.Max(maxY, corners[i].Y);
            }

            float pad = CartPhysics.PickupPadding;
            Rectangle area = new Rectangle((int)(minX - pad), (int)(minY - pad), (int)(maxX - minX + 2f * pad) + 1, (int)(maxY - minY + 2f * pad) + 1);

            nearby.Clear();
            VirtualItemSystem.GetVItemsInRect(area, nearby);

            for (int i = 0; i < nearby.Count; i++)
            {
                VirtualItem vItem = nearby[i];

                if (!vItem.active || vItem.pickupCooldown > 0 || vItem.cartGrabCooldown > 0 || vItem.stackSize <= 0)
                {
                    continue;
                }

                if (!CartGeometry.Contains(cart, vItem.worldPosition, pad))
                {
                    continue;
                }

                Item offered = new Item(vItem.itemType, vItem.stackSize);
                offered.stack = vItem.stackSize;

                if (!cart.TryAddToChest(offered))
                {
                    continue; // chest has no room for this one
                }

                vItem.stackSize = offered.IsAir ? 0 : offered.stack;

                if (vItem.stackSize <= 0)
                {
                    vItem.Remove();
                }
            }

            nearby.Clear();

            PickUpWorldItems(cart, area);
        }

        /// <summary>
        /// Moves ordinary dropped items (the ones lying in the world, not vItems) whose centre is inside, or within PickupPadding of,
        /// the rotated hitbox into the chest. Skips items still on their grab delay (just thrown or dropped), items a player is
        /// pulling in, instanced items, and hearts / mana stars (they act on whoever touches them, a chest would only swallow them).
        /// </summary>
        private static void PickUpWorldItems(Cart cart, Rectangle area)
        {
            float pad = CartPhysics.PickupPadding;

            foreach (var worldItem in Main.ActiveItems)
            {
                if (worldItem.IsAir || worldItem.grabDelayTime > 0 || worldItem.beingGrabbed || worldItem.instanced || ItemID.Sets.IsAPickup[worldItem.type])
                {
                    continue;
                }

                if (!area.Contains(worldItem.Center.ToPoint()) || !CartGeometry.Contains(cart, worldItem.Center, pad))
                {
                    continue;
                }

                // inner is the Item inside the world entity: stack changes and TurnToAir apply to the real dropped item
                // (once it is air the world item is gone).
                cart.TryAddToChest(worldItem.inner);
            }
        }

        /// <summary>
        /// Bump: every chest slot becomes one vItem on its own tile of the 3x2 window. A slot is only emptied when its tile is
        /// free (inside the world, not solid, no vItem there already, see VirtualItemSystem.IsTileOccupied); otherwise its
        /// items stay in the chest. Items with a prefix also stay: a vItem cannot carry a prefix, so spilling would lose it.
        /// </summary>
        private static void SpillChest(Cart cart)
        {
            if (cart.ChestItems == null)
            {
                return;
            }

            int left;
            int top;
            GetWindow(cart, out left, out top);

            for (int slot = 0; slot < cart.ChestItems.Length; slot++)
            {
                Item item = cart.ChestItems[slot];

                if (item == null || item.IsAir || item.prefix != 0)
                {
                    continue;
                }

                int x = left + slot % WindowColumns;
                int y = top + slot / WindowColumns;

                if (!IsTileFreeForVItem(x, y))
                {
                    continue;
                }

                VirtualItem spilled = VirtualItemSystem.SpawnVirtualItem(item.type, item.stack, x, y);
                if (spilled == null)
                {
                    continue;
                }

                spilled.cartGrabCooldown = CartPhysics.SpillGrabCooldown;
                item.TurnToAir();
            }
        }

        /// <summary>Same passability rule vItems use to move (solid blocks stop them, platforms do not) plus "no vItem there".</summary>
        private static bool IsTileFreeForVItem(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 2))
            {
                return false;
            }

            Tile tile = Main.tile[x, y];

            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
            {
                return false;
            }

            return !VirtualItemSystem.IsTileOccupied(x, y);
        }

        #endregion

        #region Terrarium: drain and pour

        /// <summary>
        /// Pulls up to TerrariumDrainPerTick units of world liquid from the cell the cart stands in (or the cell above it)
        /// into the tank. The tank holds one liquid at a time: while it has liquid, only the same world liquid is taken.
        /// </summary>
        private static void DrainWorldLiquid(Cart cart)
        {
            LiquidStack tank = cart.Tank;
            if (tank == null)
            {
                return;
            }

            float space = tank.Capacity - tank.Amount;
            if (space < 1f)
            {
                return;
            }

            int x = (int)(cart.Position.X / 16f);
            int lastRow = int.MinValue;

            for (int i = 0; i < DrainProbeOffsets.Length; i++)
            {
                int y = (int)Math.Floor((cart.Position.Y + DrainProbeOffsets[i]) / 16f);

                if (y == lastRow || !WorldGen.InWorld(x, y, 2))
                {
                    continue;
                }

                lastRow = y;

                Tile tile = Main.tile[x, y];
                if (tile.LiquidAmount == 0)
                {
                    continue;
                }

                int? worldType = LiquidTypeRegistry.FromTileLiquidId((byte)tile.LiquidType);
                if (worldType == null)
                {
                    continue; // a liquid the registry does not know
                }

                if (!tank.IsEmpty && LiquidTypeRegistry.ToTileLiquidId(tank.LiquidType) != (byte)tile.LiquidType)
                {
                    continue; // one liquid type at a time
                }

                int take = (int)Math.Min(Math.Min(CartPhysics.TerrariumDrainPerTick, (int)tile.LiquidAmount), space);
                if (take <= 0)
                {
                    continue;
                }

                if (tank.IsEmpty)
                {
                    tank.LiquidType = worldType.Value;
                    tank.Amount = 0f;
                }

                tank.Amount += take;

                // A big body of liquid (100+ connected cells, see WorldLiquidSources) is bottomless: the tank fills, the world stays as it is.
                if (!WorldLiquidSources.IsInfiniteSource(new Point(x, y)))
                {
                    tile.LiquidAmount -= (byte)take;

                    // Not tile.ClearTile(): the cart stands in the track's own tile and ClearTile would delete the rail.
                    WorldGen.SquareTileFrame(x, y);
                    SendWater(x, y);
                }

                return; // one cell per tick
            }
        }

        /// <summary>
        /// Bump: pours the tank back out as world liquid into the 3x2 window, bottom row first, never into a solid tile or a
        /// tile holding a different liquid. What does not fit stays in the tank.
        /// </summary>
        public static void PourTank(Cart cart)
        {
            LiquidStack tank = cart.Tank;
            if (tank == null || tank.IsEmpty)
            {
                return;
            }

            byte? tileLiquid = LiquidTypeRegistry.ToTileLiquidId(tank.LiquidType);
            if (tileLiquid == null)
            {
                return; // a liquid with no world form stays in the tank
            }

            int left;
            int top;
            GetWindow(cart, out left, out top);

            bool poured = false;

            for (int row = WindowRows - 1; row >= 0 && tank.Amount >= 1f; row--)
            {
                for (int column = 0; column < WindowColumns && tank.Amount >= 1f; column++)
                {
                    int x = left + column;
                    int y = top + row;

                    if (!WorldGen.InWorld(x, y, 2))
                    {
                        continue;
                    }

                    Tile tile = Main.tile[x, y];

                    if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    {
                        continue;
                    }

                    if (tile.LiquidAmount > 0 && (byte)tile.LiquidType != tileLiquid.Value)
                    {
                        continue;
                    }

                    int give = (int)Math.Min(tank.Amount, 255 - (int)tile.LiquidAmount);
                    if (give <= 0)
                    {
                        continue;
                    }

                    if (tile.LiquidAmount == 0)
                    {
                        tile.LiquidType = tileLiquid.Value;
                    }

                    tile.LiquidAmount += (byte)give;
                    tank.Amount -= give;
                    poured = true;

                    WorldGen.SquareTileFrame(x, y);
                    SendWater(x, y);
                }
            }

            if (tank.Amount < 1f)
            {
                tank.Amount = 0f;
                tank.LiquidType = -1;
            }

            if (poured)
            {
                SoundEngine.PlaySound(SoundID.SplashWeak, cart.Position);
            }
        }

        /// <summary>Same as BucketBase: a client tells the server about a liquid change. Single player needs nothing.</summary>
        private static void SendWater(int x, int y)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.sendWater(x, y);
            }
        }

        #endregion
    }
}