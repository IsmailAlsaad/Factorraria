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
        public static bool ProtectPrefixedItems = true; // vItems can't carry a prefix, so prefixed items stay in the chest instead of losing it.

        private struct Probe
        {
            public Point Tile;
            public bool AllowAdd;
        }

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

        public static void CycleMode(ChestConveyorEntry entry)
        {
            entry.Mode = (ChestConveyorMode)(((int)entry.Mode + 1) % 3);
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
                    continue; // nothing there (a destroyed chest's entry is cleaned up by ValidateEntry)
                }

                Chest chest = Main.chest[chestIndex];
                Point key = new Point(chest.x, chest.y);

                if (entries.TryGetValue(key, out ChestConveyorEntry entry))
                {
                    entry.ChestIndex = chestIndex;

                    if (!RefreshRoles(entry, chest))
                    {
                        entries.Remove(key); // no conveyor under it any more
                    }
                }
                else if (probe.AllowAdd)
                {
                    entry = new ChestConveyorEntry { TopLeft = key, ChestIndex = chestIndex };

                    if (RefreshRoles(entry, chest))
                    {
                        entries[key] = entry;
                    }
                }
            }

            pending.Clear();
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

                if (ProtectPrefixedItems && item.prefix != 0)
                {
                    continue;
                }

                if (!VirtualItemSystem.IsValidItemID(item.type))
                {
                    continue;
                }

                VirtualItem spawned = VirtualItemSystem.SpawnVirtualItem(item.type, item.stack, tileX, bottomY);
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

            int maxStack = ContentSamples.ItemsByType[vItem.itemType].maxStack;

            // 1. Top up slots that already hold this item
            for (int slot = 0; slot < Chest.maxItems && vItem.stackSize > 0; slot++)
            {
                Item item = chest.item[slot];

                if (item == null || item.IsAir || item.type != vItem.itemType || item.prefix != 0)
                {
                    continue;
                }

                int space = maxStack - item.stack;
                if (space <= 0)
                {
                    continue;
                }

                int amount = Math.Min(space, vItem.stackSize);
                item.stack += amount;
                vItem.stackSize -= amount;
                SyncSlot(chestIndex, slot);
            }

            // 2. Then use empty slots
            for (int slot = 0; slot < Chest.maxItems && vItem.stackSize > 0; slot++)
            {
                Item item = chest.item[slot];

                if (item != null && !item.IsAir)
                {
                    continue;
                }

                int amount = Math.Min(maxStack, vItem.stackSize);
                Item newItem = new Item();
                newItem.SetDefaults(vItem.itemType);
                newItem.stack = amount;
                chest.item[slot] = newItem;
                vItem.stackSize -= amount;
                SyncSlot(chestIndex, slot);
            }

            if (vItem.stackSize <= 0)
            {
                vItem.Remove(); // VirtualItemSystem drops it from its list on its next pass
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