using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Networks
{
    // Records one non-pipe tile the network touches, and which pipe tile it's next to.
    // Machine == null means this is a bare world-liquid attachment (a pool, not a machine).
    // PipePosition is kept for later — mouth-direction checks (per your rule: BFS doesn't
    // care about facing, only the drain/source step does) need to know which specific
    // pipe tile is adjacent, not just that a connection exists somewhere in the network.
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

        // Narrowed down to the WEAKEST pipe tier found while scanning — the whole
        // network can never move faster than its slowest link.
        public float MaxFlowRate = float.MaxValue;

        // LiquidNetwork.cs
        const float TicksPerMinute = 3600f; // 60 ticks/sec * 60 sec/min — PumpStrength is "per minute"
        const float MachineLiquidCapacity = 100f; // TEMP flat cap per slot, tune later / move to BaseMachine

        public void Tick()
        {
            if (ResolvedFlow.Count == 0) return; // no motor driving this network right now

            foreach (var att in MachineAttachments)
            {
                if (!ResolvedFlow.TryGetValue(att.PipePosition, out var flow)) continue;
                float amountThisTick = flow.Magnitude / TicksPerMinute;

                if (flow.Direction == att.MouthDirection)
                    PushInto(att.Machine.InputLiquids, amountThisTick);
                else if (flow.Direction == att.MouthDirection.Opposite())
                    PullFrom(att.Machine.OutputLiquids, amountThisTick);
            }

            foreach (var att in WorldLiquidAttachments)
            {
                if (!ResolvedFlow.TryGetValue(att.PipePosition, out var flow)) continue;
                if (flow.Direction != att.MouthDirection.Opposite()) continue; // only draining FROM world, bare minimum

                float amountThisTick = flow.Magnitude / TicksPerMinute;
                DrainWorldTile(att.Position, amountThisTick);
                // NOTE: dumping INTO the world at an open pipe end is not implemented yet —
                // that's item #4 from the earlier gap list, deliberately skipped for this pass.
            }
        }

        void PushInto(LiquidStack[] slots, float amount)
        {
            foreach (var slot in slots)
            {
                if (slot.IsEmpty || slot.LiquidType == pendingLiquidType) // see note below
                {
                    float space = MachineLiquidCapacity - slot.Amount;
                    float actual = Math.Min(amount, space);
                    slot.LiquidType = pendingLiquidType;
                    slot.Amount += actual;
                    return;
                }
            }
        }

        void PullFrom(LiquidStack[] slots, float amount)
        {
            foreach (var slot in slots)
            {
                if (slot.IsEmpty) continue;
                float actual = Math.Min(amount, slot.Amount);
                slot.Amount -= actual;
                pendingLiquidType = slot.LiquidType; // network now knows what it's carrying
                if (slot.Amount <= 0) slot.LiquidType = -1;
                return;
            }
        }

        void DrainWorldTile(Point pos, float amount)
        {
            Tile tile = Main.tile[pos.X, pos.Y]; // this is already a reference into the real tile data
            if (tile.LiquidAmount <= 0) return;

            pendingLiquidType = tile.LiquidType == LiquidID.Lava ? LiquidTypeRegistry.Lava : LiquidTypeRegistry.Water;

            int drained = (int)Math.Min(amount, tile.LiquidAmount);
            tile.LiquidAmount -= (byte)drained; // mutates the world directly, no write-back needed

            if (tile.LiquidAmount <= 0)
                tile.ClearTile(); // fully drained — clear liquid type/flags cleanly instead of leaving a 0-amount ghost

            WorldGen.SquareTileFrame(pos.X, pos.Y); // still needed so neighboring liquid tiles reframe/settle correctly
        }

        // TEMP: single shared "what's currently flowing" field — works for the one-source,
        // one-sink bare-minimum case, but is NOT correct once a network has multiple
        // simultaneous different-typed attachments. Flagging clearly, not fixing now.
        int pendingLiquidType = -1;
    }
}