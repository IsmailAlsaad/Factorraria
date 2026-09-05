using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Networks
{
    // Records one non-pipe tile the network touches, and which pipe tile it's next to.
    // Machine == null means this is a bare world-liquid attachment (a pool, not a machine).
    public struct PipeAttachment
    {
        public Point Position;      // where the THING BEING ATTACHED TO actually is (the machine or world-liquid tile)
        public Point PipePosition;  // which PIPE tile is sitting next to it
        public Direction MouthDirection; // which way, from the pipe, this attachment sits
        public BaseMachine Machine; // the machine object, if this is a machine attachment — null if it's a world-liquid tile instead
    }
    public struct MotorAttachment
    {
        public Point Position;
        public MotorTileEntityBase Motor;
    }

    public class LiquidNetwork
    {
        public HashSet<Point> PipeTiles = new();
        public List<PipeAttachment> MachineAttachments = new();
        public List<PipeAttachment> WorldLiquidAttachments = new();
        public List<MotorAttachment> Motors = new();
        public Dictionary<Point, (Direction Direction, float Magnitude)> ResolvedFlow = new();

        // Per-world-tile fractional carry — world liquid is a byte (whole units only), but
        // flow rates are fractional (PumpStrength/3600 per tick). Without this, any rate
        // below 1.0/tick truncates to zero forever via (int) casts and nothing ever moves.
        // Banking the remainder here lets sub-1 rates accumulate into real whole-unit
        // changes over several ticks instead of vanishing every tick.
        Dictionary<Point, float> worldWithdrawRemainder = new();
        Dictionary<Point, float> worldDepositRemainder = new();

        // Narrowed down to the WEAKEST pipe tier found while scanning — the whole
        // network can never move faster than its slowest link.
        public float MaxFlowRate = float.MaxValue;

        const float TicksPerMinute = 3600f; // 60 ticks/sec * 60 sec/min — PumpStrength is "per minute"
        const float MachineLiquidCapacity = 100f; // TEMP flat cap per slot, tune later / move to BaseMachine
        const int InfiniteSourceThreshold = 300; // bodies this size or bigger never deplete — matches vanilla's "infinite water source" idea

        // Reused across ticks — cleared, never reallocated, so a tick costs no heap
        // allocation in steady state.
        List<LiquidEndpoint> sourceBuffer = new();
        List<LiquidEndpoint> sinkBuffer = new();
        List<(LiquidEndpoint Endpoint, LiquidStack Slot, float Cap)> resolvedSources = new();
        List<(LiquidEndpoint Endpoint, LiquidStack Slot, float Cap)> resolvedSinks = new();
        HashSet<Point> floodVisited = new();
        Queue<Point> floodQueue = new();
        static readonly Point[] FloodOffsets = { new(0, -1), new(0, 1), new(-1, 0), new(1, 0) };

        public void Tick()
        {
            var config = ModContent.GetInstance<FurnaceOffsetConfig>();
            bool logThisTick = config.EnableDebugs && Main.GameUpdateCount % 60 == 0;

            if (ResolvedFlow.Count == 0)
            {
                if (logThisTick) Main.NewText("[LiquidDebug] Tick: ResolvedFlow empty, nothing to do", Color.Gray);
                return;
            }

            sourceBuffer.Clear();
            sinkBuffer.Clear();

            foreach (var att in MachineAttachments)
            {
                if (!ResolvedFlow.TryGetValue(att.PipePosition, out var flow)) continue;
                float rateThisTick = flow.Magnitude / TicksPerMinute;
                if (rateThisTick <= 0f) continue;

                if (flow.Direction == att.MouthDirection)
                    sinkBuffer.Add(LiquidEndpoint.ForMachine(att.Machine.InputLiquids, rateThisTick));
                else if (flow.Direction == att.MouthDirection.Opposite())
                    sourceBuffer.Add(LiquidEndpoint.ForMachine(att.Machine.OutputLiquids, rateThisTick));
            }

            foreach (var att in WorldLiquidAttachments)
            {
                if (!ResolvedFlow.TryGetValue(att.PipePosition, out var flow))
                {
                    if (logThisTick) Main.NewText($"[LiquidDebug] World attachment {att.Position} has NO resolved flow at pipe {att.PipePosition}", Color.OrangeRed);
                    continue;
                }

                float rateThisTick = flow.Magnitude / TicksPerMinute;
                if (rateThisTick <= 0f) continue;

                if (flow.Direction == att.MouthDirection)
                {
                    sinkBuffer.Add(LiquidEndpoint.ForWorld(att.Position, rateThisTick));
                    if (logThisTick) Main.NewText($"[LiquidDebug] World {att.Position} classified as SINK, rate={rateThisTick:F3}", Color.SkyBlue);
                }
                else if (flow.Direction == att.MouthDirection.Opposite())
                {
                    sourceBuffer.Add(LiquidEndpoint.ForWorld(att.Position, rateThisTick));
                    if (logThisTick) Main.NewText($"[LiquidDebug] World {att.Position} classified as SOURCE, rate={rateThisTick:F3}", Color.SkyBlue);
                }
                else if (logThisTick)
                {
                    Main.NewText($"[LiquidDebug] World {att.Position} mouth={att.MouthDirection} matches NEITHER flow.Direction={flow.Direction} nor its opposite", Color.OrangeRed);
                }
            }

            if (logThisTick)
                Main.NewText($"[LiquidDebug] Tick: sources={sourceBuffer.Count} sinks={sinkBuffer.Count}", Color.Gray);

            if (sourceBuffer.Count > 0 && sinkBuffer.Count > 0)
                TransferBetween(sourceBuffer, sinkBuffer);
        }

        // Gather -> check -> commit: never mutates a real slot/tile until the exact
        // transferable amount is known, so a tick can't pull more than sources actually
        // have or push more than sinks can actually hold. A network carries exactly one
        // liquid type per tick — the first source found fixes `type` for everything after.
        // Each endpoint contributes AT MOST one slot/tile — first valid match only.
        void TransferBetween(List<LiquidEndpoint> sources, List<LiquidEndpoint> sinks)
        {
            var config = ModContent.GetInstance<FurnaceOffsetConfig>();
            bool logThisTick = config.EnableDebugs && Main.GameUpdateCount % 60 == 0;

            resolvedSources.Clear();
            int type = -1;
            float totalAvailable = 0f;

            foreach (var source in sources)
            {
                LiquidStack slot = null;
                float amountHere;

                if (source.IsWorld)
                {
                    Tile tile = Main.tile[source.WorldPos.X, source.WorldPos.Y];
                    if (tile.LiquidAmount <= 0)
                    {
                        if (logThisTick) Main.NewText($"[LiquidDebug] Source world tile {source.WorldPos} is EMPTY", Color.OrangeRed);
                        continue;
                    }

                    int worldType = FromTileLiquidId((byte)tile.LiquidType);
                    if (type != -1 && worldType != type) continue;

                    amountHere = Math.Min(source.RateThisTick, tile.LiquidAmount);
                    if (amountHere <= 0f) continue;

                    type = worldType;
                }
                else
                {
                    slot = FindSourceSlot(source.MachineSlots, type);
                    if (slot == null) continue;

                    type = slot.LiquidType;
                    amountHere = Math.Min(source.RateThisTick, slot.Amount);
                    if (amountHere <= 0f) continue;
                }

                resolvedSources.Add((source, slot, amountHere));
                totalAvailable += amountHere;
            }

            if (type == -1 || totalAvailable <= 0f)
            {
                if (logThisTick) Main.NewText("[LiquidDebug] No usable source this tick — aborting transfer", Color.OrangeRed);
                return;
            }

            resolvedSinks.Clear();
            float totalAccepted = 0f;

            foreach (var sink in sinks)
            {
                LiquidStack slot = null;
                float capHere;

                if (sink.IsWorld)
                {
                    Tile tile = Main.tile[sink.WorldPos.X, sink.WorldPos.Y];

                    if (tile.HasTile && Main.tileSolid[tile.TileType])
                    {
                        if (logThisTick) Main.NewText($"[LiquidDebug] Sink world tile {sink.WorldPos} blocked by solid tile", Color.OrangeRed);
                        continue;
                    }
                    if (tile.LiquidAmount > 0 && FromTileLiquidId((byte)tile.LiquidType) != type)
                    {
                        if (logThisTick) Main.NewText($"[LiquidDebug] Sink world tile {sink.WorldPos} has MISMATCHED liquid type", Color.OrangeRed);
                        continue;
                    }

                    float space = 255f - tile.LiquidAmount;
                    capHere = Math.Min(sink.RateThisTick, space);
                    if (capHere <= 0f) continue;
                }
                else
                {
                    slot = FindSinkSlot(sink.MachineSlots, type);
                    if (slot == null) continue;

                    float space = MachineLiquidCapacity - slot.Amount;
                    capHere = Math.Min(sink.RateThisTick, space);
                    if (capHere <= 0f) continue;
                }

                resolvedSinks.Add((sink, slot, capHere));
                totalAccepted += capHere;
            }

            if (totalAccepted <= 0f)
            {
                if (logThisTick) Main.NewText("[LiquidDebug] No usable sink this tick — aborting transfer", Color.OrangeRed);
                return;
            }

            float amountToMove = Math.Min(totalAvailable, totalAccepted);
            float withdrawn = WithdrawFromResolved(amountToMove);
            DepositToResolved(type, withdrawn);

            if (logThisTick)
                Main.NewText($"[LiquidDebug] COMMITTED transfer: type={type} amount={withdrawn:F3}", Color.Lime);
        }

        static LiquidStack FindSourceSlot(LiquidStack[] slots, int requiredType)
        {
            foreach (var slot in slots)
            {
                if (slot.IsEmpty) continue;
                if (requiredType != -1 && slot.LiquidType != requiredType) continue;
                return slot; // first valid slot only — rest of this attachment's slots ignored this tick
            }
            return null;
        }

        static LiquidStack FindSinkSlot(LiquidStack[] slots, int requiredType)
        {
            foreach (var slot in slots)
            {
                if (slot.IsEmpty || slot.LiquidType == requiredType)
                    return slot; // first valid slot only
            }
            return null;
        }

        float WithdrawFromResolved(float amount)
        {
            float withdrawn = 0f;
            foreach (var (endpoint, slot, cap) in resolvedSources)
            {
                if (withdrawn >= amount) break;
                float take = Math.Min(amount - withdrawn, cap);
                if (take <= 0f) continue;

                take = endpoint.IsWorld ? WithdrawFromWorldTile(endpoint.WorldPos, take) : WithdrawFromSlot(slot, take);
                withdrawn += take;
            }
            return withdrawn;
        }

        static float WithdrawFromSlot(LiquidStack slot, float amount)
        {
            float take = Math.Min(amount, slot.Amount);
            slot.Amount -= take;
            if (slot.Amount <= 0f) slot.LiquidType = -1;
            return take;
        }

        float WithdrawFromWorldTile(Point pos, float amount)
        {
            Tile tile = Main.tile[pos.X, pos.Y];
            if (tile.LiquidAmount <= 0)
            {
                worldWithdrawRemainder.Remove(pos);
                return 0f;
            }

            if (IsInfiniteSource(pos))
                return amount; // bottomless — no quantization concern, tile is never touched anyway

            float pool = worldWithdrawRemainder.GetValueOrDefault(pos) + amount;
            int wholeUnits = (int)pool;
            float leftover = pool - wholeUnits;

            int actualDrain = Math.Min(wholeUnits, tile.LiquidAmount);

            if (actualDrain > 0)
            {
                tile.LiquidAmount -= (byte)actualDrain;
                if (tile.LiquidAmount <= 0)
                    tile.ClearTile();
                WorldGen.SquareTileFrame(pos.X, pos.Y);
            }

            if (actualDrain < wholeUnits)
            {
                // Tile ran dry mid-bank — the surplus we thought we had doesn't really
                // exist, so drop the banked fraction instead of carrying phantom liquid.
                worldWithdrawRemainder[pos] = 0f;
                return actualDrain;
            }

            worldWithdrawRemainder[pos] = leftover;
            return amount; // whole part physically drained now, fraction banked for next time
        }

        void DepositToResolved(int type, float amount)
        {
            float deposited = 0f;
            foreach (var (endpoint, slot, cap) in resolvedSinks)
            {
                if (deposited >= amount) break;
                float give = Math.Min(amount - deposited, cap);
                if (give <= 0f) continue;

                give = endpoint.IsWorld ? DepositToWorldTile(endpoint.WorldPos, type, give) : DepositToSlot(slot, type, give);
                deposited += give;
            }
        }

        static float DepositToSlot(LiquidStack slot, int type, float amount)
        {
            slot.LiquidType = type;
            slot.Amount += amount;
            return amount;
        }

        float DepositToWorldTile(Point pos, int liquidType, float amount) // becomes instance method, see note below
        {
            Tile tile = Main.tile[pos.X, pos.Y];

            float pool = worldDepositRemainder.GetValueOrDefault(pos) + amount;
            int wholeUnits = (int)pool;
            float leftover = pool - wholeUnits;

            float space = 255f - tile.LiquidAmount;
            int actualGive = (int)Math.Min(wholeUnits, space);

            if (actualGive > 0)
            {
                if (tile.LiquidAmount <= 0)
                    tile.LiquidType = ToTileLiquidId(liquidType);

                tile.LiquidAmount += (byte)actualGive;
                WorldGen.SquareTileFrame(pos.X, pos.Y);
            }

            if (actualGive < wholeUnits)
            {
                worldDepositRemainder[pos] = 0f; // sink is full — drop the excess banked fraction
                return actualGive;
            }

            worldDepositRemainder[pos] = leftover;
            return amount;
        }

        static byte ToTileLiquidId(int liquidType) => liquidType == LiquidTypeRegistry.Lava ? (byte)LiquidID.Lava : (byte)LiquidID.Water;
        static int FromTileLiquidId(byte tileLiquidId) => tileLiquidId == LiquidID.Lava ? LiquidTypeRegistry.Lava : LiquidTypeRegistry.Water;

        // Bounded flood-fill capped at InfiniteSourceThreshold — big bodies (oceans, lava
        // lakes) exit almost immediately once the cap is hit, so cost stays flat regardless
        // of real size. Small, finite ponds pay for a full scan, but those are cheap by
        // definition. NOT cached — if profiling later shows this hot (many small ponds
        // being actively drained at once), add a periodic-invalidation cache here.
        bool IsInfiniteSource(Point start)
        {
            floodVisited.Clear();
            floodQueue.Clear();

            byte liquidType = (byte)Main.tile[start.X, start.Y].LiquidType;
            floodVisited.Add(start);
            floodQueue.Enqueue(start);

            while (floodQueue.Count > 0)
            {
                if (floodVisited.Count >= InfiniteSourceThreshold) return true;

                Point current = floodQueue.Dequeue();
                foreach (Point offset in FloodOffsets)
                {
                    Point neighbor = new Point(current.X + offset.X, current.Y + offset.Y);
                    if (!WorldGen.InWorld(neighbor.X, neighbor.Y) || floodVisited.Contains(neighbor)) continue;

                    Tile t = Main.tile[neighbor.X, neighbor.Y];
                    if (t.LiquidAmount <= 0 || t.LiquidType != liquidType) continue;

                    floodVisited.Add(neighbor);
                    floodQueue.Enqueue(neighbor);
                }
            }

            return false;
        }
    }

    // One "end" of a transfer this tick — either a machine's liquid slots or a world tile
    // position. Discriminated by IsWorld so both share the same gather/check/commit path
    // in TransferBetween without allocating wrapper objects per tick.
    struct LiquidEndpoint
    {
        public bool IsWorld;
        public LiquidStack[] MachineSlots;
        public Point WorldPos;
        public float RateThisTick;

        public static LiquidEndpoint ForMachine(LiquidStack[] slots, float rate) =>
            new LiquidEndpoint { IsWorld = false, MachineSlots = slots, RateThisTick = rate };

        public static LiquidEndpoint ForWorld(Point pos, float rate) =>
            new LiquidEndpoint { IsWorld = true, WorldPos = pos, RateThisTick = rate };
    }
}