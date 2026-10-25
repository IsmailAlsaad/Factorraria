using Factorraria.Common.Liquids;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;

namespace Factorraria.Common.Networks
{
    // Liquid overhaul, step 3: the "banks" of world liquid cells.
    //
    // A Terraria liquid cell holds a whole number 0-255 and moving it in tiny fractions flickers, but the flow solver produces
    // fractional amounts every tick. So a world cell never changes by a fraction: what the solver moves is banked here, and the
    // tile only changes when a whole chunk has been reached (the full cell, or the rest of the cell).
    //   withdraw bank: liquid the network already took from this cell (it was delivered) but the tile has not lost yet.
    //   deposit bank:  liquid the network already took from its source but the tile has not received yet (and its type).
    // The offer / space the solver is told subtracts the bank, so the same unit is never promised twice, and the totals are
    // conserved over time. One instance is shared by every network (two networks may touch the same cell).
    //
    // Multiplayer: server / single-player only. Every tile change calls LiquidBroadcastSystem.Mark (patch 8.1), which tells the clients.
    // Nothing here is saved: FlushAll() applies the whole units on every topology rebuild, only the part under one unit is dropped.
    public sealed class LiquidWorldBanks
    {
        struct DepositBank
        {
            public float Amount;
            public int Type;
        }

        readonly Dictionary<Point, float> withdraw = new();
        readonly Dictionary<Point, DepositBank> deposit = new();

        float fullTile = 255f;      // vanilla liquid is a byte per cell
        float epsilon = 0.001f;     // float slack when a bank is compared with a whole number

        public int WithdrawBankCount => withdraw.Count;
        public int DepositBankCount => deposit.Count;

        // How much the cell can still give: what it holds minus what was already taken out of it and not yet removed.
        public float WorldSourceOffer(Point cell)
        {
            Tile tile = Main.tile[cell.X, cell.Y];
            if (tile.LiquidAmount <= 0)
            {
                withdraw.Remove(cell);
                return 0f;
            }

            return Math.Max(0f, tile.LiquidAmount - withdraw.GetValueOrDefault(cell));
        }

        // How much of that liquid the cell can still take: solid rock, a different liquid (in the cell or already banked) and
        // liquids without a vanilla cell id (oil, ...) take nothing.
        public float WorldSinkSpace(Point cell, int liquidType)
        {
            Tile tile = Main.tile[cell.X, cell.Y];
            if (tile.HasTile && Main.tileSolid[tile.TileType]) return 0f;

            byte? tileLiquidId = LiquidTypeRegistry.ToTileLiquidId(liquidType);
            if (tileLiquidId == null) return 0f;

            deposit.TryGetValue(cell, out DepositBank bank);
            if (bank.Amount > 0f && bank.Type != liquidType) return 0f;
            if (tile.LiquidAmount > 0 && (byte)tile.LiquidType != tileLiquidId.Value) return 0f;

            return Math.Max(0f, fullTile - tile.LiquidAmount - bank.Amount);
        }

        // The network took `amount` out of this cell. Whole chunks are removed from the tile now, the rest stays banked.
        public void AddWithdraw(Point cell, float amount)
        {
            if (amount <= 0f) return;

            Tile tile = Main.tile[cell.X, cell.Y];
            if (tile.LiquidAmount <= 0)
            {
                withdraw.Remove(cell);
                return;
            }

            float bank = withdraw.GetValueOrDefault(cell) + amount;
            float chunk = Math.Min(fullTile, tile.LiquidAmount);   // a whole cell, or what is left of it

            if (bank + epsilon >= chunk)
            {
                int take = (int)Math.Min(Math.Floor(bank + epsilon), tile.LiquidAmount);
                RemoveFromTile(cell, take);
                bank -= take;
            }

            if (bank <= epsilon) withdraw.Remove(cell);
            else withdraw[cell] = bank;
        }

        // The network put `amount` of that liquid into this cell. Whole chunks are added to the tile now, the rest stays banked.
        public void AddDeposit(Point cell, int liquidType, float amount)
        {
            if (amount <= 0f) return;

            Tile tile = Main.tile[cell.X, cell.Y];
            deposit.TryGetValue(cell, out DepositBank bank);
            if (bank.Amount > 0f && bank.Type != liquidType) return;   // WorldSinkSpace refuses this beforehand

            bank.Type = liquidType;
            bank.Amount += amount;

            float target = fullTile - tile.LiquidAmount;   // the rest of the cell
            if (target <= 0f)
            {
                deposit.Remove(cell);
                return;
            }

            if (bank.Amount + epsilon >= target)
            {
                int give = (int)Math.Min(Math.Floor(bank.Amount + epsilon), target);
                if (!AddToTile(cell, liquidType, give))
                {
                    deposit.Remove(cell);   // the cell changed under the bank (other liquid, rock): the bank is dropped
                    return;
                }
                bank.Amount -= give;
            }

            if (bank.Amount <= epsilon) deposit.Remove(cell);
            else deposit[cell] = bank;
        }

        // Applies every whole unit still banked and forgets the rest. Called before the networks are rebuilt.
        public void FlushAll()
        {
            foreach (KeyValuePair<Point, float> entry in withdraw.ToArray())
                RemoveFromTile(entry.Key, (int)Math.Floor(entry.Value + epsilon));

            foreach (KeyValuePair<Point, DepositBank> entry in deposit.ToArray())
                AddToTile(entry.Key, entry.Value.Type, (int)Math.Floor(entry.Value.Amount + epsilon));

            Clear();
        }

        public void Clear()
        {
            withdraw.Clear();
            deposit.Clear();
        }

        void RemoveFromTile(Point cell, int units)
        {
            Tile tile = Main.tile[cell.X, cell.Y];
            int drain = Math.Min(units, tile.LiquidAmount);
            if (drain <= 0) return;

            tile.LiquidAmount -= (byte)drain;
            global::Factorraria.Common.Net.LiquidBroadcastSystem.Mark(cell.X, cell.Y);   // patch 8.1: the server tells the clients
            if (tile.LiquidAmount <= 0 && !tile.HasTile)
                tile.ClearTile();

            WorldGen.SquareTileFrame(cell.X, cell.Y);
        }

        bool AddToTile(Point cell, int liquidType, int units)
        {
            byte? tileLiquidId = LiquidTypeRegistry.ToTileLiquidId(liquidType);
            if (tileLiquidId == null) return false;

            Tile tile = Main.tile[cell.X, cell.Y];
            if (tile.HasTile && Main.tileSolid[tile.TileType]) return false;
            if (tile.LiquidAmount > 0 && (byte)tile.LiquidType != tileLiquidId.Value) return false;

            int give = Math.Min(units, (int)fullTile - tile.LiquidAmount);
            if (give <= 0) return true;

            if (tile.LiquidAmount <= 0)
                tile.LiquidType = tileLiquidId.Value;

            tile.LiquidAmount += (byte)give;
            global::Factorraria.Common.Net.LiquidBroadcastSystem.Mark(cell.X, cell.Y);   // patch 8.1: the server tells the clients
            WorldGen.SquareTileFrame(cell.X, cell.Y);
            return true;
        }
    }
}