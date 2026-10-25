using Factorraria.Common.Net;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace Factorraria.Common.Systems
{
    /// <summary>What a chest is allowed to do with the conveyors under it. Cycled by the button in the chest UI.</summary>
    public enum ChestConveyorMode
    {
        InputOutput, // each side does whatever its floor tile says
        Input,       // only the input sides work
        Output       // only the output sides work
    }

    /// <summary>What one bottom-row tile of the chest does, decided by the conveyor under it.</summary>
    public enum ChestSideRole
    {
        None,   // plain floor
        Input,  // items on this tile are absorbed into the chest
        Output  // the chest spawns items on this tile
    }

    public class ChestConveyorEntry
    {
        public Point TopLeft;
        public int ChestIndex = -1;
        public ChestConveyorMode Mode = ChestConveyorMode.InputOutput;
        public ChestSideRole Left = ChestSideRole.None;
        public ChestSideRole Right = ChestSideRole.None;
    }

    /// <summary>
    /// Chests (2x2) standing on conveyors. The tile under each bottom-row chest tile decides what that side does:
    ///   Left  floor: clockwise = INPUT,  counter-clockwise = OUTPUT
    ///   Right floor: clockwise = OUTPUT, counter-clockwise = INPUT
    /// Output: while the chest's bottom tile on that side has no vItem on it, the first non-empty slot (or the first slot
    ///         matching the priority-conveyor filter) is spawned there as a whole stack. No delay: conveyor speed is the throttle.
    /// Input:  a vItem standing on that tile is moved into the chest (stack into matching slots first, then empty ones).
    ///         A priority-conveyor filter on the tile restricts what the chest accepts. A full chest lets the item pass.
    ///
    /// Only chests in the REGISTRY run any logic. A chest enters it when the player places the chest (with a conveyor under it)
    /// or places a conveyor under an existing chest (tile swap). Nothing ever scans the world or Main.chest. See ChestConveyorGlobalTile.
    /// The registry (with each chest's mode) is saved with the world.
    /// </summary>
    public class ChestConveyorSystem : ModSystem
    {
        // Tweakable (static fields, NOT const, so hot reload can change them).
        public static bool Enabled = true;

        private struct Probe
        {
            public Point Tile;
            public bool AllowAdd;
            public int Age;   // ticks this probe has been waiting for its chest to exist
        }

        // Patch 3.4c: a client places the chest tile first and the server creates the Chest object a moment later (vanilla chest
        // message), so a probe that finds no chest is retried for this many ticks before it is dropped.
        public static int ProbeRetryTicks = 180;
        private static readonly List<Probe> retry = new List<Probe>();

        private static readonly Dictionary<Point, ChestConveyorEntry> entries = new Dictionary<Point, ChestConveyorEntry>();
        private static readonly List<Probe> pending = new List<Probe>();
        private static readonly List<Point> toRemove = new List<Point>();

        #region Public API (hooks and UI)

        public static bool TryGetEntry(int chestX, int chestY, out ChestConveyorEntry entry)
        {
            return entries.TryGetValue(new Point(chestX, chestY), out entry);
        }

        /// <summary>True when a registered chest stands directly above this floor tile (cheap: two dictionary lookups).</summary>
        public static bool HasListedChestAbove(int floorX, int floorY)
        {
            if (entries.Count == 0)
            {
                return false;
            }

            return entries.ContainsKey(new Point(floorX, floorY - 2)) || entries.ContainsKey(new Point(floorX - 1, floorY - 2));
        }

        /// <summary>
        /// Asks the system to look at the chest containing this tile on the next tick (the chest entry may not exist yet while a
        /// placement hook runs). allowAdd = may add it to the registry; otherwise it can only update or remove an existing entry.
        /// </summary>
        public static void QueueProbe(int tileX, int tileY, bool allowAdd)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                return;
            }

            pending.Add(new Probe { Tile = new Point(tileX, tileY), AllowAdd = allowAdd });
        }

        /// <summary>All registered chests (the join provider writes them).</summary>
        public static IEnumerable<ChestConveyorEntry> Entries => entries.Values;

        /// <summary>
        /// The mode button was clicked. The server owns the registry (patch 3.4a), so this is a request: single-player runs the
        /// server code at once, a multiplayer client asks the server, which changes the mode and tells everyone.
        /// </summary>
        public static void CycleMode(ChestConveyorEntry entry)
        {
            ChestConveyorNet.RequestCycle(entry.TopLeft);
        }

        /// <summary>Server / single-player: really cycles the mode of the registered chest at this position. False if there is none.</summary>
        public static bool TryCycleModeAt(Point chest, out ChestConveyorMode newMode)
        {
            newMode = ChestConveyorMode.InputOutput;

            if (!entries.TryGetValue(chest, out ChestConveyorEntry entry))
            {
                return false;
            }

            entry.Mode = (ChestConveyorMode)(((int)entry.Mode + 1) % 3);
            newMode = entry.Mode;
            return true;
        }

        /// <summary>Multiplayer client: adds, changes or removes one entry of the replica. A client only knows TopLeft and Mode.</summary>
        public static void SetReplicaEntry(Point chest, bool present, ChestConveyorMode mode)
        {
            if (!present)
            {
                entries.Remove(chest);
                return;
            }

            if (entries.TryGetValue(chest, out ChestConveyorEntry entry))
            {
                entry.Mode = mode;
            }
            else
            {
                entries[chest] = new ChestConveyorEntry { TopLeft = chest, Mode = mode };
            }
        }

        /// <summary>Multiplayer client, join sync: replaces the whole replica.</summary>
        public static void ReplaceReplica(List<KeyValuePair<Point, ChestConveyorMode>> all)
        {
            entries.Clear();

            foreach (KeyValuePair<Point, ChestConveyorMode> pair in all)
            {
                entries[pair.Key] = new ChestConveyorEntry { TopLeft = pair.Key, Mode = pair.Value };
            }
        }

        #endregion

        #region Tick

        public override void PostUpdateWorld()
        {
            if (!Enabled || Main.netMode == NetmodeID.MultiplayerClient)
            {
                return;
            }

            ProcessPending();

            if (entries.Count == 0)
            {
                return;
            }

            toRemove.Clear();

            foreach (KeyValuePair<Point, ChestConveyorEntry> pair in entries)
            {
                ChestConveyorEntry entry = pair.Value;

                if (!ValidateEntry(entry))
                {
                    toRemove.Add(pair.Key);
                    continue;
                }

                RunEntry(entry);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                entries.Remove(toRemove[i]);
                ChestConveyorNet.BroadcastEntry(toRemove[i], false, ChestConveyorMode.InputOutput);
            }

            toRemove.Clear();
        }

        private static void ProcessPending()
        {
            if (pending.Count == 0)
            {
                return;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                Probe probe = pending[i];

                int chestIndex = FindChestContaining(probe.Tile);
                if (chestIndex < 0)
                {
                    // Nothing there yet (a destroyed chest's entry is cleaned up by ValidateEntry). On a server the chest may still be
                    // on its way, so keep the probe for a while.
                    if (Main.netMode == NetmodeID.Server && probe.Age < ProbeRetryTicks)
                    {
                        probe.Age++;
                        retry.Add(probe);
                    }

                    continue;
                }

                Chest chest = Main.chest[chestIndex];
                Point key = new Point(chest.x, chest.y);

                if (entries.TryGetValue(key, out ChestConveyorEntry entry))
                {
                    entry.ChestIndex = chestIndex;

                    if (!RefreshRoles(entry, chest))
                    {
                        entries.Remove(key); // no conveyor under it any more
                        ChestConveyorNet.BroadcastEntry(key, false, ChestConveyorMode.InputOutput);
                    }
                }
                else if (probe.AllowAdd)
                {
                    entry = new ChestConveyorEntry { TopLeft = key, ChestIndex = chestIndex };

                    if (RefreshRoles(entry, chest))
                    {
                        entries[key] = entry;
                        ChestConveyorNet.BroadcastEntry(key, true, entry.Mode);
                    }
                }
            }

            pending.Clear();
            pending.AddRange(retry);
            retry.Clear();
        }

        // Finds the chest (any index) whose 2x2 footprint contains this tile, by testing the four possible top-left corners.
        private static int FindChestContaining(Point tile)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                for (int dy = 0; dy <= 1; dy++)
                {
                    int index = Chest.FindChest(tile.X - dx, tile.Y - dy);

                    if (index >= 0)
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        // Makes sure the chest is still where the entry says. Entries loaded from the save start with ChestIndex = -1 and get resolved here.
        private static bool ValidateEntry(ChestConveyorEntry entry)
        {
            Chest chest = null;

            if (entry.ChestIndex >= 0 && entry.ChestIndex < Main.maxChests)
            {
                chest = Main.chest[entry.ChestIndex];
            }

            if (chest != null && chest.x == entry.TopLeft.X && chest.y == entry.TopLeft.Y)
            {
                return true;
            }

            int index = Chest.FindChest(entry.TopLeft.X, entry.TopLeft.Y);
            if (index < 0)
            {
                return false; // chest was destroyed
            }

            entry.ChestIndex = index;
            return RefreshRoles(entry, Main.chest[index]);
        }

        // Recomputes the cached side roles. Returns false when this is not an unlocked-capable 2x2 chest or has no conveyor under it.
        private static bool RefreshRoles(ChestConveyorEntry entry, Chest chest)
        {
            entry.Left = ChestSideRole.None;
            entry.Right = ChestSideRole.None;

            if (!WorldGen.InWorld(chest.x, chest.y, 4))
            {
                return false;
            }

            Tile tile = Main.tile[chest.x, chest.y];
            if (!tile.HasTile || !TileID.Sets.BasicChest[tile.TileType])
            {
                return false;
            }

            TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);
            if (data == null || data.Width != 2 || data.Height != 2)
            {
                return false;
            }

            int floorY = chest.y + 2;

            if (VirtualItemSystem.IsConveyorTile(chest.x, floorY, out bool leftClockwise, out _))
            {
                entry.Left = leftClockwise ? ChestSideRole.Input : ChestSideRole.Output;
            }

            if (VirtualItemSystem.IsConveyorTile(chest.x + 1, floorY, out bool rightClockwise, out _))
            {
                entry.Right = rightClockwise ? ChestSideRole.Output : ChestSideRole.Input;
            }

            return entry.Left != ChestSideRole.None || entry.Right != ChestSideRole.None;
        }

        private static void RunEntry(ChestConveyorEntry entry)
        {
            Chest chest = Main.chest[entry.ChestIndex];

            if (Chest.IsLocked(chest.x, chest.y))
            {
                return;
            }

            RunSide(entry, chest, chest.x, entry.Left);
            RunSide(entry, chest, chest.x + 1, entry.Right);
        }

        private static void RunSide(ChestConveyorEntry entry, Chest chest, int tileX, ChestSideRole role)
        {
            if (role == ChestSideRole.None)
            {
                return;
            }

            if (role == ChestSideRole.Output && entry.Mode == ChestConveyorMode.Input)
            {
                return;
            }

            if (role == ChestSideRole.Input && entry.Mode == ChestConveyorMode.Output)
            {
                return;
            }

            int bottomY = chest.y + 1;
            int floorY = chest.y + 2;

            // Priority conveyors with a filter restrict both directions (the query returns false for non-priority belts)
            bool hasFilter = VirtualItemSystem.TryGetConveyorFilter(tileX, floorY, out int filterItemId);

            if (role == ChestSideRole.Output)
            {
                TryOutput(entry.ChestIndex, chest, tileX, bottomY, hasFilter, filterItemId);
            }
            else
            {
                TryInput(entry.ChestIndex, chest, tileX, bottomY, hasFilter, filterItemId);
            }
        }

        #endregion

        #region Moving items

        private static void TryOutput(int chestIndex, Chest chest, int tileX, int bottomY, bool hasFilter, int filterItemId)
        {
            if (VirtualItemSystem.IsTileOccupied(tileX, bottomY))
            {
                return;
            }

            for (int slot = 0; slot < Chest.maxItems; slot++)
            {
                Item item = chest.item[slot];

                if (item == null || item.IsAir || item.stack <= 0)
                {
                    continue;
                }

                if (hasFilter && item.type != filterItemId)
                {
                    continue;
                }

                if (!VirtualItemSystem.IsValidItemID(item.type))
                {
                    continue;
                }

                // The vItem owns the clone (prefix, scroll pool and other mod data travel with it); the slot is emptied below
                VirtualItem spawned = VirtualItemSystem.SpawnVirtualItem(item.Clone(), tileX, bottomY);
                if (spawned == null)
                {
                    return;
                }

                chest.item[slot].TurnToAir();
                SyncSlot(chestIndex, slot);
                return; // one stack per tile; the tile is occupied now
            }
        }

        private static void TryInput(int chestIndex, Chest chest, int tileX, int bottomY, bool hasFilter, int filterItemId)
        {
            VirtualItem vItem = VirtualItemSystem.GetVirtualItemAtTile(tileX, bottomY);

            if (vItem == null || !vItem.active || vItem.stackSize <= 0)
            {
                return;
            }

            // Only take it once it is actually standing on the tile (not still travelling toward it)
            if (vItem.currentTileX != tileX || vItem.currentTileY != bottomY || vItem.targetTileX != tileX || vItem.targetTileY != bottomY)
            {
                return;
            }

            if (hasFilter && vItem.itemType != filterItemId)
            {
                return;
            }

            // Coins merge up a tier inside the chest (100 copper -> 1 silver, ...), like the vanilla coin slots.
            // Merging first frees slots when the chest is full of 100-stacks; merging after catches what was just added.
            bool isCoin = IsMergeableCoin(vItem.itemType);

            if (isCoin)
            {
                MergeCoins(chestIndex, chest);
            }

            // Tops up compatible stacks first (same type, prefix and mod data), then fills empty slots.
            // Whatever does not fit stays on the vItem (and on the belt).
            VItemStacking.Insert(chest.item, 0, Chest.maxItems, vItem.Payload, null, slot => SyncSlot(chestIndex, slot));

            if (isCoin)
            {
                MergeCoins(chestIndex, chest);
            }

            if (vItem.stackSize <= 0)
            {
                vItem.Remove(); // VirtualItemSystem drops it from its list on its next pass
            }
        }

        private static bool IsMergeableCoin(int type)
        {
            return type == ItemID.CopperCoin || type == ItemID.SilverCoin || type == ItemID.GoldCoin;
        }

        /// <summary>
        /// Converts every full 100 of copper/silver/gold coins in the chest into one coin of the next tier (copper -> silver ->
        /// gold -> platinum, cascading). Runs where the input runs (server / single-player); changed slots are synced.
        /// If the next tier cannot fit anywhere, the coins are put back untouched.
        /// </summary>
        private static void MergeCoins(int chestIndex, Chest chest)
        {
            for (int coinType = ItemID.CopperCoin; coinType <= ItemID.GoldCoin; coinType++)
            {
                int total = 0;

                for (int slot = 0; slot < Chest.maxItems; slot++)
                {
                    Item item = chest.item[slot];

                    if (item != null && !item.IsAir && item.type == coinType)
                    {
                        total += item.stack;
                    }
                }

                int upgrades = total / 100;

                if (upgrades <= 0)
                {
                    continue;
                }

                // Take the 100s out of the chest (frees their slots), then try to place the next-tier coins.
                RemoveCoins(chestIndex, chest, coinType, upgrades * 100);

                Item upgraded = new Item();
                upgraded.SetDefaults(coinType + 1);
                upgraded.stack = upgrades;
                VItemStacking.Insert(chest.item, 0, Chest.maxItems, upgraded, null, slot => SyncSlot(chestIndex, slot));

                if (!upgraded.IsAir && upgraded.stack > 0)
                {
                    // No room for the higher coins (practically only with a full chest of maxed platinum): give the lower ones back.
                    Item leftover = new Item();
                    leftover.SetDefaults(coinType);
                    leftover.stack = upgraded.stack * 100;
                    VItemStacking.Insert(chest.item, 0, Chest.maxItems, leftover, null, slot => SyncSlot(chestIndex, slot));
                }
            }
        }

        private static void RemoveCoins(int chestIndex, Chest chest, int coinType, int amount)
        {
            for (int slot = 0; slot < Chest.maxItems && amount > 0; slot++)
            {
                Item item = chest.item[slot];

                if (item == null || item.IsAir || item.type != coinType)
                {
                    continue;
                }

                int taken = Math.Min(item.stack, amount);
                item.stack -= taken;
                amount -= taken;

                if (item.stack <= 0)
                {
                    item.TurnToAir();
                }

                SyncSlot(chestIndex, slot);
            }
        }

        private static void SyncSlot(int chestIndex, int slot)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, chestIndex, slot);
            }
        }

        #endregion

        #region Save / load

        public override void SaveWorldData(TagCompound tag)
        {
            if (entries.Count == 0)
            {
                return;
            }

            List<TagCompound> saved = new List<TagCompound>();

            foreach (ChestConveyorEntry entry in entries.Values)
            {
                saved.Add(new TagCompound
                {
                    ["x"] = entry.TopLeft.X,
                    ["y"] = entry.TopLeft.Y,
                    ["mode"] = (int)entry.Mode
                });
            }

            tag["chests"] = saved;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            // Do not clear in OnWorldLoad: it can run after this. Entries load with ChestIndex = -1 and are resolved/validated on the first tick.
            entries.Clear();
            pending.Clear();

            if (!tag.ContainsKey("chests"))
            {
                return;
            }

            foreach (TagCompound saved in tag.GetList<TagCompound>("chests"))
            {
                Point key = new Point(saved.GetInt("x"), saved.GetInt("y"));
                int mode = saved.GetInt("mode");

                if (mode < 0 || mode > 2)
                {
                    mode = 0;
                }

                entries[key] = new ChestConveyorEntry { TopLeft = key, Mode = (ChestConveyorMode)mode };
            }
        }

        public override void OnWorldUnload()
        {
            entries.Clear();
            pending.Clear();
            toRemove.Clear();
        }

        public override void Unload()
        {
            entries.Clear();
            pending.Clear();
            toRemove.Clear();
        }

        #endregion
    }
}