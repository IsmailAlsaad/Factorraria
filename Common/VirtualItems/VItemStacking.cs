using System;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Content.VirtualItems
{
    /// <summary>
    /// The one shared "can these two items stack" rule for everything that moves items in or out of vItems
    /// (belts, machines, carts, chests). Do not re-implement it elsewhere.
    /// </summary>
    public static class VItemStacking
    {
        /// <summary>
        /// Same type, same prefix, and ItemLoader.CanStack agrees in both directions (CanStack is called on the destination,
        /// so it can be asymmetric). Does not look at stack counts or max stack.
        /// </summary>
        public static bool CanMerge(Item destination, Item source)
        {
            if (destination == null || source == null || destination.IsAir || source.IsAir)
            {
                return false;
            }

            if (destination.type != source.type || destination.prefix != source.prefix)
            {
                return false;
            }

            return ItemLoader.CanStack(destination, source) && ItemLoader.CanStack(source, destination);
        }

        /// <summary>
        /// Moves as much of source.stack as fits into slots[first .. first+count): tops up compatible slots first, then fills empty
        /// slots with a clone of source. Returns the amount moved. source.stack is reduced by that amount, and source is turned to air
        /// when it is fully moved. slotFilter (optional) is asked (slotIndex, source) before a slot is used.
        /// onSlotChanged (optional) is called with the index of every slot that was modified, e.g. to sync a chest slot.
        /// </summary>
        public static int Insert(Item[] slots, int first, int count, Item source, Func<int, Item, bool> slotFilter = null, Action<int> onSlotChanged = null)
        {
            if (slots == null || source == null || source.IsAir)
            {
                return 0;
            }

            int start = Math.Max(first, 0);
            int end = Math.Min(slots.Length, first + count);
            int movedTotal = 0;

            // 1. Top up compatible stacks
            for (int i = start; i < end; i++)
            {
                Item slot = slots[i];
                if (slot == null || slot.IsAir || slot.stack >= slot.maxStack || !CanMerge(slot, source))
                {
                    continue;
                }

                if (slotFilter != null && !slotFilter(i, source))
                {
                    continue;
                }

                if (ItemLoader.TryStackItems(slot, source, out int moved))
                {
                    movedTotal += moved;
                    onSlotChanged?.Invoke(i);
                }

                if (source.stack <= 0)
                {
                    source.TurnToAir();
                    return movedTotal;
                }
            }

            // 2. Fill empty slots
            for (int i = start; i < end; i++)
            {
                if (slots[i] != null && !slots[i].IsAir)
                {
                    continue;
                }

                if (slotFilter != null && !slotFilter(i, source))
                {
                    continue;
                }

                Item placed = source.Clone();
                placed.stack = Math.Min(source.stack, source.maxStack);
                slots[i] = placed;
                onSlotChanged?.Invoke(i);

                movedTotal += placed.stack;
                source.stack -= placed.stack;

                if (source.stack <= 0)
                {
                    source.TurnToAir();
                    return movedTotal;
                }
            }

            return movedTotal;
        }
    }
}