using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Machine light updates (roadmap patch 5.2, findings M3 channel 2, M7, M8).
    //
    // Authority: the server owns every machine's state; a client replica is read-only. TileEntity.Update does not run on clients, so
    // after the 5.1 snapshot a replica would stay frozen. This system keeps the replica's VISIBLE state current: isOn, isWorking, work
    // progress, crafts counter, useFraction (electric machines), the running recipe (a content fingerprint, so the progress and the fuel
    // burn fraction can be derived), every fuel module's Remaining/Capacity and the type + quantized amount of every liquid tank. Slot
    // contents are NOT sent (patch 5.3 sends them to players with the UI open). Animation, light and dust are draw-driven on clients and
    // follow the flags with no change (M8); isOn and useFraction are written by PowerNetwork on the server (M7) and travel here.
    //
    // Who sends: the server, from PostUpdateEverything, every ScanIntervalTicks ticks. For each machine with at least one active player
    // inside the near rectangle it builds the light payload from QUANTIZED values (progress rounded down to ProgressStepTicks, tank
    // amounts in 1/32 of capacity, use fraction in 1/255), compares the bytes with the previous payload and bumps a version when they
    // differ. Every near player whose last-sent version is older gets the entry. So a packet exists only for a changed machine and only for
    // players near it; a player who walks into range later gets the current state on the next scan; a player far away costs nothing.
    // Several machines go into one packet per player (at most MaxPacketBytes of entries). Entries are length-prefixed, so a client that has
    // no entity for an entry (section not received yet) skips it without shifting the stream.
    //
    // Near test (works on a dedicated server): the server's own copy of each active player's position (Main.player[i].Center, kept by
    // vanilla player sync) against a rectangle of RangeHalfWidth x RangeHalfHeight around the machine centre. Nothing client-only is read.
    //
    // Who receives: HandleLightUpdate on a multiplayer client writes the values into the existing replica IN PLACE (BaseMachine.
    // ApplyLightState). It never replaces the entity, never calls MachinePlacement.Share and never sends anything back.
    //
    // Interaction with 5.1: the snapshot (NetSend/NetReceive, tile section data and message 86) is the full state and still the only way
    // slots arrive. A re-share after the biome report replaces the replica with a fresh full state; light packets that follow only update
    // what changed. The per-player version marks are not reset by a share: a share is always at least as new as the last light packet
    // (same TCP stream, same order), and a stale replica can only come from a player the server never sent to, which has mark 0.
    // Single-player: the system does nothing (netMode is not Server), the entity IS the real state.
    public class MachineLightSyncSystem : ModSystem
    {
        // Tuning knobs. Instance fields on purpose, so hot reload can change them.
        public int ScanIntervalTicks = 6;        // how often changed machines are looked for (6 = at most 10 packets per second per player)
        public int ProgressStepTicks = 10;       // work progress is sent rounded down to this many ticks
        public float RangeHalfWidth = 1440f;     // near rectangle around a machine, half width in pixels (90 tiles; screen half width is 960)
        public float RangeHalfHeight = 880f;     // half height in pixels (55 tiles; screen half height is 540)
        public int MaxPacketBytes = 1200;        // entries per packet stop at about this size
        public int PruneIntervalTicks = 600;

        sealed class Record
        {
            public byte[] Last;                                  // payload last built for this machine
            public uint Version;                                 // bumped whenever the payload changes
            public readonly uint[] SentTo = new uint[Main.maxPlayers];   // per player: the version that player has (0 = never sent)
            public CustomRecipe RecipeRef;                       // fingerprint cache
            public uint RecipeFingerprint;
        }

        readonly Dictionary<int, Record> records = new Dictionary<int, Record>();
        readonly bool[] wasActive = new bool[Main.maxPlayers];
        readonly List<byte[]>[] pending = new List<byte[]>[Main.maxPlayers];
        readonly List<int> near = new List<int>();

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.MachineLightUpdate, PacketDirection.ToClient, MachineLightSync.HandleLightUpdate);
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            uint interval = (uint)Math.Max(1, ScanIntervalTicks);
            if (Main.GameUpdateCount % interval != 0)
                return;

            if (PruneIntervalTicks > 0 && Main.GameUpdateCount % (uint)PruneIntervalTicks < interval)
                Prune();

            bool anyone = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                bool active = Main.player[i].active;
                if (wasActive[i] && !active)
                    ForgetPlayer(i);   // a slot that is reused later starts from nothing
                wasActive[i] = active;
                anyone |= active;
                pending[i]?.Clear();
            }

            if (!anyone)
                return;

            foreach (TileEntity entity in TileEntity.ByID.Values)
            {
                if (!(entity is BaseMachine machine))
                    continue;

                Vector2 center = machine.MachineInitialized
                    ? machine.MachineCenter
                    : new Vector2(machine.Position.X * 16f + 8f, machine.Position.Y * 16f + 8f);

                near.Clear();
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (!wasActive[i])
                        continue;

                    Vector2 playerCenter = Main.player[i].Center;
                    if (MathF.Abs(playerCenter.X - center.X) <= RangeHalfWidth && MathF.Abs(playerCenter.Y - center.Y) <= RangeHalfHeight)
                        near.Add(i);
                }

                if (near.Count == 0)
                    continue;   // nobody is looking: no work, no record, no traffic

                if (!records.TryGetValue(machine.ID, out Record record))
                    records[machine.ID] = record = new Record();

                CustomRecipe selected = machine.SelectedRecipe;
                uint fingerprint = 0;
                if (selected != null)
                {
                    if (!ReferenceEquals(selected, record.RecipeRef))
                    {
                        record.RecipeRef = selected;
                        record.RecipeFingerprint = MachineLightSync.FingerprintOrZero(selected);
                    }
                    fingerprint = record.RecipeFingerprint;
                }

                byte[] payload = machine.BuildLightState(ProgressStepTicks, fingerprint);
                if (record.Last == null || !payload.AsSpan().SequenceEqual(record.Last))
                {
                    record.Last = payload;
                    record.Version++;
                }

                byte[] entry = null;
                foreach (int playerIndex in near)
                {
                    if (record.SentTo[playerIndex] == record.Version)
                        continue;

                    record.SentTo[playerIndex] = record.Version;
                    entry ??= BuildEntry(machine, record.Last);
                    (pending[playerIndex] ??= new List<byte[]>()).Add(entry);
                }
            }

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                List<byte[]> list = pending[i];
                if (list != null && list.Count > 0)
                    Send(i, list);
            }
        }

        static byte[] BuildEntry(BaseMachine machine, byte[] payload)
        {
            using var stream = new MemoryStream(payload.Length + 8);
            using (var w = new BinaryWriter(stream))
            {
                w.Write((short)machine.Position.X);
                w.Write((short)machine.Position.Y);
                w.Write7BitEncodedInt(payload.Length);
                w.Write(payload);
            }
            return stream.ToArray();
        }

        void Send(int player, List<byte[]> entries)
        {
            int index = 0;
            while (index < entries.Count)
            {
                int start = index;
                int size = 0;
                while (index < entries.Count && (index == start || size + entries[index].Length <= MaxPacketBytes))
                    size += entries[index++].Length;

                int from = start;
                int to = index;
                FactorrariaNet.SendToClient(player, FactorrariaPacket.MachineLightUpdate, w =>
                {
                    w.Write((byte)1);                     // light packet format version
                    w.Write7BitEncodedInt(to - from);
                    for (int k = from; k < to; k++)
                        w.Write(entries[k]);
                });

                Interlocked.Increment(ref MachineLightSync.PacketsSent);
                Interlocked.Add(ref MachineLightSync.EntriesSent, to - from);
                Interlocked.Add(ref MachineLightSync.BytesSent, size);
            }
        }

        void ForgetPlayer(int index)
        {
            foreach (Record record in records.Values)
                record.SentTo[index] = 0;
        }

        void Prune()
        {
            List<int> dead = null;
            foreach (int id in records.Keys)
            {
                if (!TileEntity.ByID.ContainsKey(id))
                    (dead ??= new List<int>()).Add(id);
            }

            if (dead != null)
            {
                foreach (int id in dead)
                    records.Remove(id);
            }
        }

        public override void OnWorldUnload()
        {
            ResetState();
        }

        public override void Unload()
        {
            ResetState();
        }

        void ResetState()
        {
            MachineLightSync.ClearCaches();
            records.Clear();
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                wasActive[i] = false;
                pending[i]?.Clear();
            }
        }
    }

    // Codec and client handler for the light packet (see the system above for the design).
    public static class MachineLightSync
    {
        // Temporary test counters for /fsnap (remove in Phase 10 with the command).
        public static int PacketsSent;
        public static int EntriesSent;
        public static long BytesSent;
        public static int EntriesReceived;
        public static int Applied;
        public static int Missed;    // entries for a position where this client has no machine (yet)
        public static int Failed;

        // A liquid tank amount travels as a number of steps out of TankSteps (1 to TankSteps; an empty tank sends no amount).
        public static int TankSteps = 32;

        // ---------------------------------------------------------------- client handler

        public static void HandleLightUpdate(BinaryReader reader, int sender)
        {
            int version = reader.ReadByte();
            int count = reader.Read7BitEncodedInt();
            if (count < 0 || count > 4000)
                return;

            for (int n = 0; n < count; n++)
            {
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                int length = reader.Read7BitEncodedInt();
                byte[] bytes = length > 0 ? reader.ReadBytes(length) : Array.Empty<byte>();
                Interlocked.Increment(ref EntriesReceived);

                if (version != 1)
                    continue;

                if (!TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TileEntity entity) || !(entity is BaseMachine machine))
                {
                    Interlocked.Increment(ref Missed);
                    continue;
                }

                try
                {
                    using var stream = new MemoryStream(bytes);
                    using var payloadReader = new BinaryReader(stream);
                    machine.ApplyLightState(payloadReader);
                    Interlocked.Increment(ref Applied);
                }
                catch (Exception e)
                {
                    Interlocked.Increment(ref Failed);
                    FactorrariaNet.Log("[Machines] light update for " + machine.GetType().Name + " at (" + x + ", " + y + ") could not be read: " + e.Message);
                }
            }
        }

        // ---------------------------------------------------------------- quantization

        public static byte QuantizeUse(float useFraction)
        {
            if (float.IsNaN(useFraction)) return 0;
            return (byte)MathF.Round(Math.Clamp(useFraction, 0f, 1f) * 255f);
        }

        public static int QuantizeAmount(LiquidStack tank)
        {
            float capacity = Math.Max(1f, tank.Capacity);
            int steps = Math.Max(1, TankSteps);
            return Math.Clamp((int)MathF.Round(tank.Amount / capacity * steps), 1, steps);
        }

        public static void WriteTanks(LiquidStack[] tanks, BinaryWriter w)
        {
            w.Write7BitEncodedInt(tanks.Length);
            foreach (LiquidStack tank in tanks)
            {
                int type = tank.IsEmpty ? -1 : tank.LiquidType;
                w.Write7BitEncodedInt(type + 1);
                if (type >= 0)
                    w.Write((byte)QuantizeAmount(tank));
            }
        }

        // Writes into the existing tank objects (their pipe filters stay). A tank whose type and quantized amount already match is left
        // alone, so the exact amount from the 5.1 snapshot survives until the liquid really changes.
        public static void ReadTanks(LiquidStack[] tanks, BinaryReader r)
        {
            int count = r.Read7BitEncodedInt();
            int steps = Math.Max(1, TankSteps);
            for (int i = 0; i < count; i++)
            {
                int type = r.Read7BitEncodedInt() - 1;
                int quantized = type >= 0 ? r.ReadByte() : 0;
                if (i >= tanks.Length)
                    continue;

                LiquidStack tank = tanks[i];
                bool known = type >= 0 && type < LiquidTypeRegistry.definitions.Count;
                if (known)
                {
                    if (!tank.IsEmpty && tank.LiquidType == type && QuantizeAmount(tank) == quantized)
                        continue;

                    tank.LiquidType = type;
                    tank.Amount = Math.Min(quantized, steps) / (float)steps * Math.Max(1f, tank.Capacity);
                }
                else if (!tank.IsEmpty)
                {
                    tank.LiquidType = -1;
                    tank.Amount = 0f;
                }
            }
        }

        // ---------------------------------------------------------------- recipes by fingerprint

        sealed class FingerprintMap
        {
            public int Count;
            public readonly Dictionary<uint, CustomRecipe> ByFingerprint = new Dictionary<uint, CustomRecipe>();
        }

        static readonly Dictionary<RecipeBook, FingerprintMap> maps = new Dictionary<RecipeBook, FingerprintMap>();
        static readonly object mapLock = new object();

        // The same content fingerprint the 5.1 snapshot uses, but 0 is kept for "no recipe".
        public static uint FingerprintOrZero(CustomRecipe recipe)
        {
            if (recipe == null) return 0;
            uint fingerprint = MachineSnapshot.Fingerprint(recipe);
            return fingerprint == 0 ? 1u : fingerprint;
        }

        // Client side: the recipe of this machine's book with that fingerprint (two recipes with identical content give the first one).
        public static CustomRecipe FindRecipe(RecipeBook book, uint fingerprint)
        {
            if (book == null || fingerprint == 0)
                return null;

            lock (mapLock)
            {
                if (!maps.TryGetValue(book, out FingerprintMap map) || map.Count != book.All.Count)
                {
                    map = new FingerprintMap { Count = book.All.Count };
                    foreach (CustomRecipe recipe in book.All)
                        map.ByFingerprint.TryAdd(FingerprintOrZero(recipe), recipe);
                    maps[book] = map;
                }

                return map.ByFingerprint.TryGetValue(fingerprint, out CustomRecipe found) ? found : null;
            }
        }

        public static void ClearCaches()
        {
            lock (mapLock)
                maps.Clear();
        }
    }
}