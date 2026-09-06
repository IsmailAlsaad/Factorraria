using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Networks
{
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

        public float MaxFlowRate = float.MaxValue;

        const float TicksPerMinute = 3600f;
        const float MachineLiquidCapacity = 100f;
        const int InfiniteSourceThreshold = 300;

        // A world tile only ever moves in whole-tile chunks — matches vanilla liquid
        // amounts (byte, 0-255) and avoids the flicker/visual weirdness of draining a
        // fraction of a unit every tick. Machines don't need this (LiquidStack is a
        // float, fractional amounts are fine there).
        const float FullTileAmount = 255f;

        List<LiquidEndpoint> sourceBuffer = new();
        List<LiquidEndpoint> sinkBuffer = new();
        List<(LiquidEndpoint Endpoint, LiquidStack Slot, float Cap)> resolvedSources = new();
        List<(LiquidEndpoint Endpoint, LiquidStack Slot, float Cap)> resolvedSinks = new();
        HashSet<Point> floodVisited = new();
        Queue<Point> floodQueue = new();
        static readonly Point[] FloodOffsets = { new(0, -1), new(0, 1), new(-1, 0), new(1, 0) };

        // Per-world-tile "how close to a full tile have we banked" trackers. A world
        // source/sink doesn't act every tick — it silently accumulates its tiny per-tick
        // rate here until the bank crosses FullTileAmount, then one whole-tile transfer
        // fires at once. Reset (fully or partially) whenever an actual withdrawal/deposit
        // consumes some of the bank.
        Dictionary<Point, float> worldWithdrawBank = new();
        Dictionary<Point, float> worldDepositBank = new();

        public void Tick()
        {
            if (ResolvedFlow.Count == 0) return;

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
                if (!ResolvedFlow.TryGetValue(att.PipePosition, out var flow)) continue;
                float rateThisTick = flow.Magnitude / TicksPerMinute;
                if (rateThisTick <= 0f) continue;

                if (flow.Direction == att.MouthDirection)
                    sinkBuffer.Add(LiquidEndpoint.ForWorld(att.Position, rateThisTick));
                else if (flow.Direction == att.MouthDirection.Opposite())
                    sourceBuffer.Add(LiquidEndpoint.ForWorld(att.Position, rateThisTick));
            }

            if (sourceBuffer.Count > 0 && sinkBuffer.Count > 0)
                TransferBetween(sourceBuffer, sinkBuffer);
        }

        void TransferBetween(List<LiquidEndpoint> sources, List<LiquidEndpoint> sinks)
        {
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
                        worldWithdrawBank.Remove(source.WorldPos);
                        continue;
                    }

                    // Bank this tick's contribution unconditionally, whether or not this
                    // source ends up participating — otherwise banking would stall
                    // whenever it's rejected below (type lock, not ready yet, etc).
                    bool infinite = IsInfiniteSource(source.WorldPos);
                    float bank;
                    if (infinite)
                    {
                        bank = FullTileAmount;
                    }
                    else
                    {
                        bank = worldWithdrawBank.GetValueOrDefault(source.WorldPos) + source.RateThisTick;
                        worldWithdrawBank[source.WorldPos] = bank;
                    }

                    int worldType = (int)LiquidTypeRegistry.FromTileLiquidId((byte)tile.LiquidType);
                    if (type != -1 && worldType != type) continue;

                    if (bank < FullTileAmount) continue; // still banking toward a full tile

                    amountHere = infinite ? FullTileAmount : Math.Min(FullTileAmount, tile.LiquidAmount);
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

            if (type == -1 || totalAvailable <= 0f) return;

            resolvedSinks.Clear();
            float totalAccepted = 0f;

            foreach (var sink in sinks)
            {
                LiquidStack slot = null;
                float capHere;

                if (sink.IsWorld)
                {
                    Tile tile = Main.tile[sink.WorldPos.X, sink.WorldPos.Y];

                    if (tile.HasTile && Main.tileSolid[tile.TileType]) continue;

                    float bank = worldDepositBank.GetValueOrDefault(sink.WorldPos) + sink.RateThisTick;
                    worldDepositBank[sink.WorldPos] = bank;

                    if (tile.LiquidAmount > 0 && LiquidTypeRegistry.FromTileLiquidId((byte)tile.LiquidType) != type) continue; // mismatch — item #6

                    if (bank < FullTileAmount) continue; // still banking toward a full tile

                    float space = 255f - tile.LiquidAmount;
                    capHere = Math.Min(FullTileAmount, space);
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

            if (totalAccepted <= 0f) return;

            float amountToMove = Math.Min(totalAvailable, totalAccepted);
            float withdrawn = WithdrawFromResolved(amountToMove);
            DepositToResolved(type, withdrawn);
        }

        static LiquidStack FindSourceSlot(LiquidStack[] slots, int requiredType)
        {
            foreach (var slot in slots)
            {
                if (slot.IsEmpty) continue;
                if (requiredType != -1 && slot.LiquidType != requiredType) continue;
                return slot;
            }
            return null;
        }

        static LiquidStack FindSinkSlot(LiquidStack[] slots, int requiredType)
        {
            foreach (var slot in slots)
            {
                if (slot.IsEmpty || slot.LiquidType == requiredType)
                    return slot;
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
                worldWithdrawBank.Remove(pos);
                return 0f;
            }

            if (IsInfiniteSource(pos))
                return amount; // bottomless — tile untouched, no bank to spend

            int actualDrain = (int)Math.Min(amount, tile.LiquidAmount);
            if (actualDrain <= 0) return 0f;

            tile.LiquidAmount -= (byte)actualDrain;
            if (tile.LiquidAmount <= 0)
                tile.ClearTile();

            WorldGen.SquareTileFrame(pos.X, pos.Y);

            // Spend the bank by what was actually withdrawn — normally zeroes it out (a
            // full tile just moved); if a sink capped the transfer lower, the unspent
            // remainder stays banked so the next full-tile threshold arrives sooner.
            float remaining = worldWithdrawBank.GetValueOrDefault(pos) - actualDrain;
            worldWithdrawBank[pos] = Math.Max(0f, remaining);

            return actualDrain;
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

        float DepositToWorldTile(Point pos, int liquidType, float amount)
        {
            Tile tile = Main.tile[pos.X, pos.Y];

            float space = 255f - tile.LiquidAmount;
            int give = (int)Math.Min(amount, space);
            if (give <= 0) return 0f;

            if (tile.LiquidAmount <= 0)
                tile.LiquidType = (int)LiquidTypeRegistry.ToTileLiquidId(liquidType);

            tile.LiquidAmount += (byte)give;
            WorldGen.SquareTileFrame(pos.X, pos.Y);

            float remaining = worldDepositBank.GetValueOrDefault(pos) - give;
            worldDepositBank[pos] = Math.Max(0f, remaining);

            return give;
        }

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