using Factorraria.Common.Knowledge;
using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Net
{
    // Machine snapshot codec (roadmap patch 5.1, findings M3, M7, M8, M9, U3 sync half).
    //
    // Authority: the server (or single-player) owns every machine's state; a client replica is a read-only copy. This patch adds no packet
    // and no request. tModLoader calls BaseMachine.NetSend on the server in two places and BaseMachine.NetReceive on the client (see the
    // roadmap, Phase 5): the tile section data (a client joins or walks into a section it has not received yet) and vanilla's
    // tile-entity sharing message 86 (MachinePlacement.Share, the placement and bootstrap routes of patches 4.2 and 4.5). Both use the same
    // bytes. TileEntity.Update does not run on clients, so a replica only changes when a new snapshot replaces it (5.2 adds live updates).
    //
    // Wire format (BaseMachine.BuildSnapshot / ApplySnapshot): length-prefixed payload, so a short or unreadable payload can never shift
    // the rest of the section stream. Recipes travel as catalog keys (RecipeCatalog.KeyFor) plus a content fingerprint, liquids as
    // LiquidTypeRegistry ids (the registry is append-only), items the way vanilla syncs them (ItemIO). Geometry is derived, not sent.
    //
    // NetReceive can run on the network thread, so nothing here touches UI, textures or the world.
    public static class MachineSnapshot
    {
        // Temporary test counters for /fsnap (remove in Phase 10 with the command).
        public static int Sent;
        public static long SentBytes;
        public static int Received;
        public static long ReceivedBytes;
        public static int Failed;

        // ---------------------------------------------------------------- items and liquids

        public static void WriteItems(Item[] items, BinaryWriter w)
        {
            w.Write7BitEncodedInt(items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                bool present = item != null && !item.IsAir && item.stack > 0;
                w.Write(present);
                if (present)
                    ItemIO.Send(item, w, writeStack: true);
            }
        }

        // Always consumes everything that was sent; slots the receiver does not have are read and dropped.
        public static void ReadItems(Item[] items, BinaryReader r)
        {
            int count = r.Read7BitEncodedInt();
            for (int i = 0; i < count; i++)
            {
                Item item = r.ReadBoolean() ? ItemIO.Receive(r, readStack: true) : new Item();
                if (i < items.Length)
                    items[i] = item;
            }
        }

        public static void WriteLiquids(LiquidStack[] tanks, BinaryWriter w)
        {
            w.Write7BitEncodedInt(tanks.Length);
            for (int i = 0; i < tanks.Length; i++)
            {
                w.Write7BitEncodedInt(tanks[i].LiquidType + 1);   // -1 (empty) travels as 0
                w.Write(tanks[i].Amount);
            }
        }

        // The tank objects stay (their pipe filters are set when they are created); only type and amount are overwritten.
        public static void ReadLiquids(LiquidStack[] tanks, BinaryReader r)
        {
            int count = r.Read7BitEncodedInt();
            for (int i = 0; i < count; i++)
            {
                int type = r.Read7BitEncodedInt() - 1;
                float amount = r.ReadSingle();
                if (i >= tanks.Length)
                    continue;

                bool typeKnown = type >= -1 && type < LiquidTypeRegistry.definitions.Count;
                tanks[i].LiquidType = typeKnown ? type : -1;
                tanks[i].Amount = typeKnown && !float.IsNaN(amount) && amount > 0f ? amount : 0f;
            }
        }

        // ---------------------------------------------------------------- recipes

        sealed class GroupMap
        {
            public int Count;
            public readonly Dictionary<string, RecipeOutputGroup> ByKey = new Dictionary<string, RecipeOutputGroup>();
        }

        // One lookup table per recipe book: catalog key -> group. Rebuilt when the book's group count changes. Guarded by a lock because
        // NetReceive may run on the network thread while the main thread builds or reads it (the /fsnap command).
        static readonly Dictionary<RecipeBook, GroupMap> groupMaps = new Dictionary<RecipeBook, GroupMap>();
        static readonly object groupMapLock = new object();

        static Dictionary<string, RecipeOutputGroup> GroupMapFor(RecipeBook book, string machineName)
        {
            lock (groupMapLock)
            {
                if (groupMaps.TryGetValue(book, out GroupMap map) && map.Count == book.Groups.Count)
                    return map.ByKey;

                map = new GroupMap { Count = book.Groups.Count };
                foreach (RecipeOutputGroup group in book.Groups)
                    map.ByKey[RecipeCatalog.KeyFor(machineName, group)] = group;
                groupMaps[book] = map;
                return map.ByKey;
            }
        }

        // Manual pick and running recipe. A group is named by its catalog key. A concrete recipe inside a group is named by its position
        // as a hint plus a content fingerprint, and the receiver trusts the fingerprint, so a differently ordered list still resolves.
        public static void WriteRecipes(BaseMachine machine, BinaryWriter w)
        {
            string manualKey = "";
            string selectedKey = "";
            int index = 0;
            uint fingerprint = 0;

            RecipeBook book = machine.Recipes;
            string name = RecipeCatalog.NameOf(book);
            if (book != null && name != null)
            {
                if (machine.ManualGroup != null)
                    manualKey = RecipeCatalog.KeyFor(name, machine.ManualGroup);

                CustomRecipe selected = machine.SelectedRecipe;
                if (selected != null && selected.TryGetPrimaryOutput(out RecipeOutputKey outputKey))
                {
                    string key = RecipeCatalog.KeyFor(name, outputKey);
                    if (GroupMapFor(book, name).TryGetValue(key, out RecipeOutputGroup group))
                    {
                        int found = group.Recipes.IndexOf(selected);
                        if (found >= 0)
                        {
                            selectedKey = key;
                            index = found;
                            fingerprint = Fingerprint(selected);
                        }
                    }
                }
            }

            w.Write(manualKey);
            w.Write(selectedKey);
            if (selectedKey.Length > 0)
            {
                w.Write7BitEncodedInt(index);
                w.Write(fingerprint);
            }
        }

        public static void ReadRecipes(BaseMachine machine, BinaryReader r)
        {
            string manualKey = r.ReadString();
            string selectedKey = r.ReadString();
            int index = 0;
            uint fingerprint = 0;
            if (selectedKey.Length > 0)
            {
                index = r.Read7BitEncodedInt();
                fingerprint = r.ReadUInt32();
            }

            RecipeBook book = machine.Recipes;
            string name = RecipeCatalog.NameOf(book);
            if (book == null || name == null)
                return;

            Dictionary<string, RecipeOutputGroup> map = GroupMapFor(book, name);

            // SetManualGroup also clears SelectedRecipe, so it comes first.
            RecipeOutputGroup manual = null;
            if (manualKey.Length > 0)
                map.TryGetValue(manualKey, out manual);
            machine.SetManualGroup(manual);

            if (selectedKey.Length == 0 || !map.TryGetValue(selectedKey, out RecipeOutputGroup selectedGroup))
                return;

            CustomRecipe resolved = null;
            if (index >= 0 && index < selectedGroup.Recipes.Count && Fingerprint(selectedGroup.Recipes[index]) == fingerprint)
                resolved = selectedGroup.Recipes[index];
            else
            {
                foreach (CustomRecipe candidate in selectedGroup.Recipes)
                {
                    if (Fingerprint(candidate) != fingerprint)
                        continue;
                    resolved = candidate;
                    break;
                }
            }
            machine.SelectedRecipe = resolved;
        }

        // FNV-1a over the recipe's inputs and outputs (item internal names and stacks, liquid ids and amounts). string.GetHashCode is
        // randomised per process, so it cannot be used between a server and a client. Duration is left out on purpose: it is balance data,
        // not identity.
        public static uint Fingerprint(CustomRecipe recipe)
        {
            uint h = 2166136261;
            foreach (RecipeIngredient input in recipe.Inputs) { h = Mix(h, SafeName(input.Type)); h = Mix(h, input.Stack); }
            h = Mix(h, ">");
            foreach (RecipeIngredient output in recipe.Outputs) { h = Mix(h, SafeName(output.Type)); h = Mix(h, output.Stack); }
            h = Mix(h, "|");
            foreach (LiquidIngredient input in recipe.LiquidInputs) { h = Mix(h, input.LiquidType); h = Mix(h, BitConverter.SingleToInt32Bits(input.Amount)); }
            h = Mix(h, ">");
            foreach (LiquidIngredient output in recipe.LiquidOutputs) { h = Mix(h, output.LiquidType); h = Mix(h, BitConverter.SingleToInt32Bits(output.Amount)); }
            return h;
        }

        static uint Mix(uint h, int value)
        {
            unchecked
            {
                for (int b = 0; b < 4; b++)
                {
                    h ^= (uint)((value >> (8 * b)) & 0xFF);
                    h *= 16777619;
                }
            }
            return h;
        }

        static uint Mix(uint h, string text)
        {
            unchecked
            {
                foreach (char c in text)
                {
                    h ^= c;
                    h *= 16777619;
                }
                h ^= 0xFF;
                h *= 16777619;
            }
            return h;
        }

        public static string SafeName(int itemType)
        {
            try { return ItemID.Search.GetName(itemType); }
            catch (Exception) { return "#" + itemType; }
        }

        // ---------------------------------------------------------------- test aid helpers (temporary, used by /fsnap)

        public static uint Digest(byte[] data)
        {
            uint h = 2166136261;
            unchecked
            {
                foreach (byte b in data)
                {
                    h ^= b;
                    h *= 16777619;
                }
            }
            return h;
        }

        public static string DescribeItems(Item[] items)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(i).Append(':');
                Item item = items[i];
                if (item == null || item.IsAir) sb.Append('-');
                else sb.Append(item.stack).Append('x').Append(SafeName(item.type));
            }
            return sb.ToString();
        }

        public static string DescribeLiquids(LiquidStack[] tanks)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < tanks.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(i).Append(':');
                int type = tanks[i].LiquidType;
                if (type < 0 || type >= LiquidTypeRegistry.definitions.Count) sb.Append('-');
                else sb.Append(LiquidTypeRegistry.Get(type).Name).Append(' ').Append(tanks[i].Amount.ToString("0.#"));
            }
            return sb.ToString();
        }

        public static string DescribeRecipe(BaseMachine machine)
        {
            string name = RecipeCatalog.NameOf(machine.Recipes);
            if (name == null)
                return "-";

            string manual = machine.ManualGroup == null ? "auto" : RecipeCatalog.KeyFor(name, machine.ManualGroup);
            string running = "-";
            if (machine.SelectedRecipe != null && machine.SelectedRecipe.TryGetPrimaryOutput(out RecipeOutputKey key))
                running = RecipeCatalog.KeyFor(name, key) + "#" + Fingerprint(machine.SelectedRecipe).ToString("X8");
            return "manual=" + manual + " running=" + running;
        }

        public static void CountSent(int bytes)
        {
            Interlocked.Increment(ref Sent);
            Interlocked.Add(ref SentBytes, bytes);
        }

        public static void CountReceived(int bytes)
        {
            Interlocked.Increment(ref Received);
            Interlocked.Add(ref ReceivedBytes, bytes);
        }

        public static void CountFailed() => Interlocked.Increment(ref Failed);
    }
}